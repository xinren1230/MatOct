## MatRewrite CoreLib: trilinear sampling of V and its gradient at vertex list.
function [vi, g] = __interp_cube__ (x, y, z, val, v, req)
  if (nargin != 6) print_usage (); endif
  if (! (isnumeric (x) && isnumeric (y) && isnumeric (z)) || ! (size_equal (x, y, z) || (isvector (x) && isvector (y) && isvector (z))))
    error ("__interp_cube__: X, Y, Z have unequal dimensions");
  endif
  if (isvector (x) && isvector (y) && isvector (z))
    if (! isequal ([numel(y), numel(x), numel(z)], size (val)))
      error ("__interp_cube__: VAL dimensions must match those of X, Y, and Z");
    endif
    xv = x(:).'; yv = y(:).'; zv = z(:).';
  else
    if (ndims (x) != 3 || ! size_equal (x, val))
      if (! size_equal (x, val) && size_equal (x, y, z) && ndims (x) == 3)
        error ("__interp_cube__: VAL dimensions must match those of X, Y, and Z");
      endif
      error ("__interp_cube__: X, Y, Z have unequal dimensions");
    endif
    xv = squeeze (x(1,:,1)); yv = squeeze (y(:,1,1)).'; zv = squeeze (z(1,1,:)).';
  endif
  val = double (val);
  vi = interp3 (xv, yv, zv, val, v(:,1), v(:,2), v(:,3));
  g = [];
  if (strcmp (req, "normals") || strcmp (req, "both"))
    [gx, gy, gz] = gradient (val, xv, yv, zv);
    g = [interp3(xv, yv, zv, gx, v(:,1), v(:,2), v(:,3)), ...
         interp3(xv, yv, zv, gy, v(:,1), v(:,2), v(:,3)), ...
         interp3(xv, yv, zv, gz, v(:,1), v(:,2), v(:,3))];
    if (strcmp (req, "normals")) vi = g; endif
  endif
endfunction
