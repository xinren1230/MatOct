## MatRewrite - GPL-3.0-or-later
## Iterated 2-D integral: int_{xa}^{xb} int_{ya(x)}^{yb(x)} f(x,y) dy dx
function [q, err] = __int2d_eval__ (f, xa, xb, ya, yb, at, rt, vec)
  if (! vec)
    f = @(x, y) arrayfun (@(xx, yy) f (xx, yy), x, y);
  endif
  inner = @(x) __int2d_inner__ (f, x, ya, yb, at, rt);
  [q, err] = quadgk (inner, xa, xb, "AbsTol", at, "RelTol", rt);
endfunction

function r = __int2d_inner__ (f, x, ya, yb, at, rt)
  r = zeros (size (x));
  for i = 1:numel (x)
    a = ya; b = yb;
    if (is_function_handle (a)), a = a (x(i)); endif
    if (is_function_handle (b)), b = b (x(i)); endif
    xi = x(i);
    r(i) = __qcc__ (@(y) f (xi * ones (size (y)), y), a, b, [at, rt]);
  endfor
endfunction
