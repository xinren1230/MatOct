## MatRewrite - GPL-3.0-or-later
function [q, ier, nfev, err] = quad (f, a, b, tol = [], sing = [])
  if (nargin < 3)
    print_usage ();
  endif
  if (ischar (f)), f = str2func (f); endif
  at = 1e-10; rt = 1e-10;
  if (! isempty (tol)), at = tol(1); if (numel (tol) > 1), rt = tol(2); endif; endif
  [q, err] = quadgk (f, a, b, "AbsTol", at, "RelTol", rt, "Waypoints", sing);
  ier = 0; nfev = 0;
endfunction
