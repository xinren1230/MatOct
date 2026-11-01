function [x, flag, relres, iter, resvec] = cgs (A, b, tol, maxit, M1, M2, x0, varargin)
  ## Conjugate gradients squared (MatRewrite core implementation).
  if (nargin < 2) print_usage (); endif
  if (nargin < 3) tol = []; endif
  if (nargin < 4) maxit = []; endif
  if (nargin < 5) M1 = []; endif
  if (nargin < 6) M2 = []; endif
  if (nargin < 7) x0 = []; endif
  [A, b, tol, maxit, M1, M2, x, n] = __itsolve_args__ ("cgs", A, b, tol, maxit, M1, M2, x0);
  nb = norm (b); flag = 1; iter = 0;
  if (nb == 0) x = zeros (n, 1); flag = 0; relres = 0; resvec = 0; return; endif
  r = b - __itsolve_apply__ (A, x, varargin{:}); rt = r;
  resvec = norm (r); xmin = x; imin = 0; rmin = resvec;
  if (resvec <= tol * nb) flag = 0; relres = resvec / nb; return; endif
  rho_old = 1;
  for it = 1:maxit
    rho = rt' * r;
    if (rho == 0) flag = 4; break; endif
    if (it == 1) u = r; p = u;
    else beta = rho / rho_old; u = r + beta * q; p = u + beta * (q + beta * p); endif
    ph = __itsolve_prec__ (M1, M2, p);
    vh = __itsolve_apply__ (A, ph, varargin{:});
    alpha = rho / (rt' * vh);
    q = u - alpha * vh;
    uh = __itsolve_prec__ (M1, M2, u + q);
    x = x + alpha * uh;
    r = r - alpha * __itsolve_apply__ (A, uh, varargin{:});
    iter = it; resvec(end+1, 1) = norm (r);
    if (resvec(end) < rmin) rmin = resvec(end); xmin = x; imin = it; endif
    if (resvec(end) <= tol * nb) flag = 0; break; endif
    rho_old = rho;
  endfor
  if (flag != 0) x = xmin; iter = imin; endif
  relres = norm (b - __itsolve_apply__ (A, x, varargin{:})) / nb;
endfunction
