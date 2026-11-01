## MatRewrite - GPL-3.0-or-later
function q = triplequad (f, xa, xb, ya, yb, za, zb, tol = 1e-6, quadf = @quadcc, varargin)
  if (nargin < 7)
    print_usage ();
  endif
  if (isempty (tol)), tol = 1e-6; endif
  if (isempty (quadf)), quadf = @quadcc; endif
  inner = @(z) __tpl_inner__ (z, f, xa, xb, ya, yb, tol, quadf, varargin{:});
  q = feval (quadf, inner, za, zb, tol);
endfunction

function q = __tpl_inner__ (z, f, xa, xb, ya, yb, tol, quadf, varargin)
  q = zeros (size (z));
  for i = 1:numel (z)
    q(i) = dblquad (@(x, y) f (x, y, z(i), varargin{:}), xa, xb, ya, yb, tol, quadf);
  endfor
endfunction
