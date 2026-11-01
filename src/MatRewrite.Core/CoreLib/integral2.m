## MatRewrite - GPL-3.0-or-later
function [q, err] = integral2 (f, xa, xb, ya, yb, varargin)
  if (nargin < 5 || mod (nargin, 2) == 0)
    print_usage ();
  endif
  o = __int2d_opts__ ("integral2", f, {xa, xb, ya, yb}, {"XA", "XB", "YA", "YB"}, varargin, {"method"});
  if (nargout > 1 && strcmp (o.method, "iterated"))
    warning ("integral2: \"iterated\" method can not return estimated error");
  endif
  [q, err] = __int2d_eval__ (f, double (xa), double (xb), ya, yb, o.abstol, o.reltol, o.vectorized);
  if (strcmp (o.method, "iterated")), err = 0; endif
  if (isa (xa, "single") || isa (xb, "single")), q = single (q); err = single (err); endif
endfunction
