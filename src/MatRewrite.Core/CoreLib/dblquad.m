## MatRewrite - GPL-3.0-or-later
function q = dblquad (f, xa, xb, ya, yb, tol = 1e-6, quadf = @quadcc, varargin)
  if (nargin < 5)
    print_usage ();
  endif
  if (isempty (tol)), tol = 1e-6; endif
  if (isempty (quadf)), quadf = @quadcc; endif
  inner = @(y) __dbl_inner__ (y, f, xa, xb, tol, quadf, varargin{:});
  q = feval (quadf, @(y) inner (y), ya, yb, tol);
endfunction

function q = __dbl_inner__ (y, f, xa, xb, tol, quadf, varargin)
  q = zeros (size (y));
  for i = 1:numel (y)
    q(i) = feval (quadf, @(x) f (x, y(i), varargin{:}), xa, xb, tol);
  endfor
endfunction
