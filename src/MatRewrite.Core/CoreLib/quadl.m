## MatRewrite - GPL-3.0-or-later
function [q, nfev] = quadl (f, a, b, tol = [], trace = false, varargin)
  if (nargin < 3)
    print_usage ();
  endif
  if (ischar (f)), f = str2func (f); endif
  if (isempty (tol))
    tol = [1e-6, 1e-10]; if (isa (a, "single") || isa (b, "single")), tol = [1e-4 1e-5]; endif
  elseif (! isscalar (tol) && numel (tol) != 2)
    error ("quadl: TOL must be a scalar or 2-element vector");
  elseif (any (tol < 0))
    error ("quadl: TOL must be a scalar >=0");
  endif
  if (isscalar (tol)), tol = [tol, 1e-10]; endif
  g = @(x) f (x, varargin{:});
  cnt = 0;
  ed = linspace (double (a), double (b), 6); q = 0;
  for k = 1:5
    [qk, cnt] = __quadl_rec__ (g, ed(k), ed(k+1), tol(1)/5, 0, cnt);
    q += qk;
  endfor
  nfev = cnt;
  if (isa (a, "single") || isa (b, "single")), q = single (q); endif
endfunction

function [q, cnt] = __quadl_rec__ (g, a, b, tol, depth, cnt)
  ## Gauss-Lobatto 4-point with Kronrod 7-point extension (Gander & Gautschi)
  m = (a+b)/2; h = (b-a)/2;
  alpha = sqrt (2/3); beta = 1/sqrt (5);
  x = [a, m-alpha*h, m-beta*h, m, m+beta*h, m+alpha*h, b];
  y = g (x); cnt += 7;
  y(! isfinite (y)) = 0;
  i2 = (h/6) * (y(1) + y(7) + 5*(y(3) + y(5)));
  i1 = (h/1470) * (77*(y(1)+y(7)) + 432*(y(2)+y(6)) + 625*(y(3)+y(5)) + 672*y(4));
  if (abs (i1 - i2) <= tol || depth > 40 || abs (b - a) < 1e-14 * max (1, abs (m)))
    q = i1;
  else
    [q1, cnt] = __quadl_rec__ (g, a, m, tol/1.5, depth+1, cnt);
    [q2, cnt] = __quadl_rec__ (g, m, b, tol/1.5, depth+1, cnt);
    q = q1 + q2;
  endif
endfunction
