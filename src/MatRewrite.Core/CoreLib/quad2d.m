## MatRewrite - GPL-3.0-or-later
function [q, err, iter] = quad2d (f, xa, xb, ya, yb, varargin)
  if (nargin < 5 || mod (nargin, 2) == 0)
    print_usage ();
  endif
  o = __int2d_opts__ ("quad2d", f, {xa, xb, ya, yb}, {"XA", "XB", "YA", "YB"}, varargin, ...
                      {"maxfunevals", "singular", "failureplot"});
  if (o.maxfunevals < 100)
    warning ("quad2d: Maximum number of sub-tiles reached without convergence");
  endif
  [q, err] = __int2d_eval__ (f, double (xa), double (xb), ya, yb, o.abstol, o.reltol, o.vectorized);
  iter = 1;
  if (isa (xa, "single") || isa (xb, "single")), q = single (q); err = single (err); endif
endfunction
