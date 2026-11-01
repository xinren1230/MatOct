## MatRewrite CoreLib: expand/validate X, Y, Z grids for isosurface/isocaps.
function [x, y, z] = __iso_grid__ (fn, x, y, z, v)
  sz = size (v);
  if (ndims (v) != 3 || any (sz(1:3) < 2))
    error ("%s: V must be a non-singleton 3-dimensional matrix", fn);
  endif
  if (isempty (x)) x = 1:sz(2); endif
  if (isempty (y)) y = 1:sz(1); endif
  if (isempty (z)) z = 1:sz(3); endif
  if (isvector (x) && numel (x) == sz(2))
    x = repmat (x(:).', [sz(1), 1, sz(3)]);
  elseif (! size_equal (v, x))
    error ("%s: X must match the size of V", fn);
  endif
  if (isvector (y) && numel (y) == sz(1))
    y = repmat (y(:), [1, sz(2), sz(3)]);
  elseif (! size_equal (v, y))
    error ("%s: Y must match the size of V", fn);
  endif
  if (isvector (z) && numel (z) == sz(3))
    z = repmat (reshape (z(:), 1, 1, []), [sz(1), sz(2), 1]);
  elseif (! size_equal (v, z))
    error ("%s: Z must match the size of V", fn);
  endif
  x = double (x); y = double (y); z = double (z);
endfunction
