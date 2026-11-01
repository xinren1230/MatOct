function [x, flag, relres, iter, resvec] = gmres (A, b, restart, tol, maxit, M1, M2, x0, varargin)
  ## Restarted GMRES (MatRewrite core implementation). ITER = [outer, inner].
  if (nargin < 2) print_usage (); endif
  if (nargin < 3) restart = []; endif
  if (nargin < 4) tol = []; endif
  if (nargin < 5) maxit = []; endif
  if (nargin < 6) M1 = []; endif
  if (nargin < 7) M2 = []; endif
  if (nargin < 8) x0 = []; endif
  [A, b, tol, ~, M1, M2, x, n] = __itsolve_args__ ("gmres", A, b, tol, 1, M1, M2, x0);
  if (isempty (restart) || restart > n) restart = n; endif
  if (isempty (maxit)) maxit = min (n / restart, 10); endif
  maxit = max (1, floor (maxit));
  pb = __itsolve_prec__ (M1, M2, b);
  nb = norm (pb); flag = 1; iter = [0 0];
  if (norm (b) == 0) x = zeros (n, 1); flag = 0; relres = 0; resvec = 0; return; endif
  r = __itsolve_prec__ (M1, M2, b - __itsolve_apply__ (A, x, varargin{:}));
  resvec = norm (r);
  if (resvec <= tol * nb) flag = 0; relres = resvec / nb; return; endif
  done = false;
  for outer = 1:maxit
    beta = norm (r);
    V = zeros (n, restart + 1); H = zeros (restart + 1, restart);
    V(:,1) = r / beta;
    for k = 1:restart
      w = __itsolve_prec__ (M1, M2, __itsolve_apply__ (A, V(:,k), varargin{:}));
      [w, h] = mgorth (w, V(:,1:k));
      H(1:k+1, k) = h;
      if (h(end) != 0) V(:,k+1) = w; endif
      e1 = zeros (k + 1, 1); e1(1) = beta;
      y = H(1:k+1, 1:k) \ e1;
      res = norm (e1 - H(1:k+1, 1:k) * y);
      resvec(end+1, 1) = res;
      iter = [outer, k];
      if (res <= tol * nb || h(end) == 0)
        x = x + V(:,1:k) * y;
        done = true; break;
      endif
    endfor
    if (done) break; endif
    x = x + V(:,1:restart) * y;
    r = __itsolve_prec__ (M1, M2, b - __itsolve_apply__ (A, x, varargin{:}));
  endfor
  if (done && resvec(end) <= tol * nb) flag = 0; endif
  relres = norm (__itsolve_prec__ (M1, M2, b - __itsolve_apply__ (A, x, varargin{:}))) / nb;
  if (flag == 0 && relres > tol) flag = 3 * 0; endif
endfunction
