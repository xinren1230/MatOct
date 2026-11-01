## MatRewrite - GPL-3.0-or-later
function q = integral3 (f, xa, xb, ya, yb, za, zb, varargin)
  if (nargin < 7 || mod (nargin, 2) == 0)
    print_usage ();
  endif
  o = __int2d_opts__ ("integral3", f, {xa, xb, ya, yb, za, zb}, {"XA", "XB", "YA", "YB", "ZA", "ZB"}, varargin, {"method"});
  if (! (isnumeric (za) && isscalar (za) && isreal (za)) && ! is_function_handle (za))
    error ("integral3: ZA must be a real scalar or a function");
  endif
  if (! (isnumeric (zb) && isscalar (zb) && isreal (zb)) && ! is_function_handle (zb))
    error ("integral3: ZB must be a real scalar or a function");
  endif
  if (! o.vectorized)
    g = f; f = @(x, y, z) arrayfun (@(a, b, c) g (a, b, c), x, y, z);
  endif
  at = o.abstol; rt = o.reltol;
  q = quadgk (@(x) __i3_outer__ (f, x, ya, yb, za, zb, at, rt), double (xa), double (xb), "AbsTol", at, "RelTol", rt);
  if (isa (xa, "single") || isa (xb, "single")), q = single (q); endif
endfunction

function r = __i3_outer__ (f, x, ya, yb, za, zb, at, rt)
  r = zeros (size (x));
  for i = 1:numel (x)
    xi = x(i);
    a = ya; b = yb;
    if (is_function_handle (a)), a = a (xi); endif
    if (is_function_handle (b)), b = b (xi); endif
    r(i) = __qcc__ (@(y) __i3_mid__ (f, xi, y, za, zb, at, rt), a, b, [at, rt]);
  endfor
endfunction

function r = __i3_mid__ (f, xi, y, za, zb, at, rt)
  r = zeros (size (y));
  for j = 1:numel (y)
    yj = y(j);
    a = za; b = zb;
    if (is_function_handle (a)), a = a (xi, yj); endif
    if (is_function_handle (b)), b = b (xi, yj); endif
    r(j) = __qcc__ (@(z) f (xi * ones (size (z)), yj * ones (size (z)), z), a, b, [at, rt]);
  endfor
endfunction
