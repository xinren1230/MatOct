function [x, flag, relres, iter, resvec, eigest] = pcg (A, b, tol, maxit, M1, M2, x0, varargin)
  ## Preconditioned conjugate gradient (MatRewrite core implementation).
  if (nargin < 2) print_usage (); endif
  if (nargin < 3) tol = []; endif
  if (nargin < 4) maxit = []; endif
  if (nargin < 5) M1 = []; endif
  if (nargin < 6) M2 = []; endif
  if (nargin < 7) x0 = []; endif
  [A, b, tol, maxit, M1, M2, x, n] = __itsolve_args__ ("pcg", A, b, tol, maxit, M1, M2, x0);
  nb = norm (b);
  flag = 1; iter = 0;
  if (nb == 0)
    x = zeros (n, 1); flag = 0; relres = 0; resvec = 0; eigest = [NaN; NaN];
    return;
  endif
  r = b - __itsolve_apply__ (A, x, varargin{:});
  resvec = norm (r);
  xmin = x; imin = 0; rmin = resvec(1);
  if (resvec(1) <= tol * nb)
    flag = 0; relres = resvec(1) / nb; eigest = [NaN; NaN]; return;
  endif
  rho_old = 1; p = zeros (n, 1);
  alphas = []; betas = [];
  for it = 1:maxit
    z = __itsolve_prec__ (M1, M2, r);
    rho = r' * z;
    if (it == 1)
      p = z; beta = 0;
    else
      beta = rho / rho_old; p = z + beta * p;
    endif
    q = __itsolve_apply__ (A, p, varargin{:});
    pq = p' * q;
    if (pq <= 0 || ! isfinite (pq))
      flag = 4 * (pq <= 0) + 2 * (! isfinite (pq)); if (flag > 4) flag = 4; endif
      break;
    endif
    alpha = rho / pq;
    alphas(end+1) = alpha; betas(end+1) = beta;
    x = x + alpha * p;
    r = r - alpha * q;
    iter = it;
    resvec(end+1, 1) = norm (r);
    if (resvec(end) < rmin) rmin = resvec(end); xmin = x; imin = it; endif
    if (resvec(end) <= tol * nb)
      flag = 0; break;
    endif
    if (norm (alpha * p) <= eps * norm (x))
      flag = 3; break;
    endif
    rho_old = rho;
  endfor
  if (flag != 0)
    x = xmin; iter = imin;
  endif
  relres = norm (b - __itsolve_apply__ (A, x, varargin{:})) / nb;
  eigest = [NaN; NaN];
  if (nargout > 5 && numel (alphas) > 1)
    k = numel (alphas);
    T = diag (1 ./ alphas);
    for i = 2:k
      T(i,i) += betas(i) / alphas(i-1);
      T(i,i-1) = -sqrt (betas(i)) / alphas(i-1); T(i-1,i) = T(i,i-1);
    endfor
    e = eig (T); eigest = [min(e); max(e)];
  endif
endfunction
