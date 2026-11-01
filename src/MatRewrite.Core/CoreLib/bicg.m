function [x, flag, relres, iter, resvec] = bicg (A, b, tol, maxit, M1, M2, x0, varargin)
  ## Biconjugate gradient (MatRewrite core implementation).
  if (nargin < 2) print_usage (); endif
  if (nargin < 3) tol = []; endif
  if (nargin < 4) maxit = []; endif
  if (nargin < 5) M1 = []; endif
  if (nargin < 6) M2 = []; endif
  if (nargin < 7) x0 = []; endif
  [A, b, tol, maxit, M1, M2, x, n] = __itsolve_args__ ("bicg", A, b, tol, maxit, M1, M2, x0);
  nb = norm (b); flag = 1; iter = 0;
  if (nb == 0) x = zeros (n, 1); flag = 0; relres = 0; resvec = 0; return; endif
  r = b - __at__ (A, x, false, varargin{:}); rt = r;
  resvec = norm (r); xmin = x; imin = 0; rmin = resvec;
  if (resvec <= tol * nb) flag = 0; relres = resvec / nb; return; endif
  rho_old = 1;
  for it = 1:maxit
    z = __bp__ (M2, __bp__ (M1, r, false), false);
    zt = __bp__ (M1, __bp__ (M2, rt, true), true);
    rho = z' * rt;
    if (rho == 0) flag = 4; break; endif
    if (it == 1) p = z; pt = zt; else beta = rho / rho_old; p = z + beta * p; pt = zt + conj (beta) * pt; endif
    q = __at__ (A, p, false, varargin{:});
    qt = __at__ (A, pt, true, varargin{:});
    alpha = rho / (pt' * q);
    x = x + alpha * p; r = r - alpha * q; rt = rt - conj (alpha) * qt;
    iter = it; resvec(end+1, 1) = norm (r);
    if (resvec(end) < rmin) rmin = resvec(end); xmin = x; imin = it; endif
    if (resvec(end) <= tol * nb) flag = 0; break; endif
    rho_old = rho;
  endfor
  if (flag != 0) x = xmin; iter = imin; endif
  relres = norm (b - __at__ (A, x, false, varargin{:})) / nb;
endfunction
function y = __at__ (A, x, transp, varargin)
  if (is_function_handle (A))
    if (transp) y = A (x, "transp", varargin{:}); else y = A (x, "notransp", varargin{:}); endif
  elseif (transp)
    y = A' * x;
  else
    y = A * x;
  endif
endfunction
function y = __bp__ (M, x, transp)
  if (isempty (M))
    y = x;
  elseif (is_function_handle (M))
    if (transp) y = M (x, "transp"); else y = M (x, "notransp"); endif
  elseif (transp)
    y = M' \ x;
  else
    y = M \ x;
  endif
endfunction
