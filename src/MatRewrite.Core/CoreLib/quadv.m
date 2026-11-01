## MatRewrite - GPL-3.0-or-later
function [q, nfev] = quadv (f, a, b, tol = [], trace = false, varargin)
  if (nargin < 3)
    print_usage ();
  endif
  if (ischar (f)), f = str2func (f); endif
  if (isempty (tol))
    tol = 1e-6;
  elseif (! isscalar (tol))
    error ("quadv: TOL must be a scalar");
  elseif (tol < 0)
    error ("quadv: TOL must be a scalar >=0");
  endif
  g = @(x) f (x, varargin{:});
  a = double (a); b = double (b);
  ## Initial 5-point split avoids aliasing for periodic integrands
  n = 8; xs = linspace (a, b, 2*n+1);
  q = 0; nfev = 0;
  for k = 1:n
    x0 = xs(2*k-1); x2 = xs(2*k+1); x1 = xs(2*k);
    f0 = g (__qv_e__ (x0, a, b)); f1 = g (x1); f2 = g (__qv_e__ (x2, a, b));
    nfev += 3;
    s = (x2 - x0)/6 * (f0 + 4*f1 + f2);
    [qk, nfev] = __qv_rec__ (g, x0, x1, x2, f0, f1, f2, s, tol/n, nfev, 0);
    q = q + qk;
  endfor
endfunction

function x = __qv_e__ (x, a, b)
  ## nudge exact endpoints to avoid singularities
  if (x == a), x = a + eps (max (1, abs (a))) * 0 + (b-a)*eps; endif
  if (x == b), x = b - (b-a)*eps; endif
endfunction

function [q, nfev] = __qv_rec__ (g, a, c, b, fa, fc, fb, s, tol, nfev, depth)
  d = (a+c)/2; e = (c+b)/2;
  fd = g (d); fe = g (e); nfev += 2;
  sl = (c-a)/6 * (fa + 4*fd + fc); sr = (b-c)/6 * (fc + 4*fe + fb);
  s2 = sl + sr;
  if (depth > 50 || max (abs ((s2 - s)(:))) <= 15*tol || abs (b-a) < 1e-13)
    q = s2 + (s2 - s)/15;
  else
    [q1, nfev] = __qv_rec__ (g, a, d, c, fa, fd, fc, sl, tol/2, nfev, depth+1);
    [q2, nfev] = __qv_rec__ (g, c, e, b, fc, fe, fb, sr, tol/2, nfev, depth+1);
    q = q1 + q2;
  endif
endfunction
