## MatRewrite - GPL-3.0-or-later
## zi = griddata (x, y, z, xi, yi, method), [xi, yi, zi] = griddata (...),
## vi = griddata (x, y, z, v, xi, yi, zi, method): interpolate scattered data.
function [rx, ry, rz] = griddata (x, y, z, varargin)
  if (nargin < 5)
    print_usage ();
  endif
  if (nargin > 6)
    if (nargout > 1)
      error ("griddata: only one output argument valid for 3-D interpolation");
    endif
    rx = griddata3 (x, y, z, varargin{:});
    return;
  endif
  xi = varargin{1};  yi = varargin{2};
  if (isvector (x) && isvector (y) && all ([numel(y), numel(x)] == size (z)))
    [x, y] = meshgrid (x, y);
  endif
  if (isvector (x) && isvector (y) && isvector (z))
    if (! isequal (length (x), length (y), length (z)))
      error ("griddata: X, Y, and Z must be vectors of the same length");
    endif
  elseif (! size_equal (x, y, z))
    error ("griddata: lengths of X, Y must match the columns and rows of Z");
  endif
  if ((isrow (xi) && iscolumn (yi)) || (iscolumn (xi) && isrow (yi)))
    [xi, yi] = meshgrid (xi, yi);
  elseif (! size_equal (xi, yi))
    error ("griddata: XI and YI must be vectors or matrices of same size");
  endif
  method = "linear";
  if (nargin == 6)
    method = varargin{3};
    if (isempty (method))
      method = "linear";
    elseif (! ischar (method))
      error ("griddata: METHOD must be a string");
    endif
    method = tolower (method);
    if (any (strcmp (method, {"cubic", "natural"})))
      error ('griddata: "%s" interpolation not yet implemented', method);
    elseif (! any (strcmp (method, {"linear", "nearest", "v4"})))
      error ('griddata: unknown interpolation METHOD: "%s"', method);
    endif
  endif
  x = x(:);  y = y(:);  z = z(:);
  zi = NaN (size (xi));
  switch (method)
    case "linear"
      tri = delaunay (x, y);
      [t, bary] = tsearchn ([x, y], tri, [xi(:), yi(:)]);
      valid = ! isnan (t);
      zi(valid) = sum (reshape (z(tri(t(valid), :)), [], 3) .* bary(valid, :), 2);
    case "nearest"
      idx = dsearchn ([x, y], [xi(:), yi(:)]);
      zi(:) = z(idx);
    case "v4"
      ## biharmonic spline (Sandwell 1987) using the Green function r^2 (log r - 1)
      r = sqrt ((x - x.').^2 + (y - y.').^2);
      D = (r.^2) .* (log (r) - 1);
      D(isnan (D)) = 0;
      w = D \ z;
      ri = sqrt ((xi(:) - x.').^2 + (yi(:) - y.').^2);
      Di = (ri.^2) .* (log (ri) - 1);
      Di(isnan (Di)) = 0;
      zi(:) = Di * w;
  endswitch
  if (nargout > 1)
    rx = xi;  ry = yi;  rz = zi;
  else
    rx = zi;
  endif
endfunction
