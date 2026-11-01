## MatRewrite - GPL-3.0-or-later
## yi = griddatan (x, y, xi, method): interpolate scattered N-D data (linear, nearest, v4 in 2-D).
function yi = griddatan (x, y, xi, method = "linear", varargin)
  if (nargin < 3)
    print_usage ();
  endif
  if (ischar (method))
    method = tolower (method);
  endif
  [m, n] = size (x);
  [mi, ni] = size (xi);
  if (m < n + 1)
    error ("griddatan: number of points in X is too small");
  elseif (! iscolumn (y))
    error ("griddatan: Y must be a column vector");
  elseif (rows (y) != m)
    error ("griddatan: Y must contain the same number of points (rows) as X");
  elseif (n != ni)
    error ("griddatan: dimension of query data XI (columns) must match X");
  elseif (! ischar (method))
    error ("griddatan: METHOD must be a string");
  endif
  if (strcmp (method, "v4"))
    if (n != 2)
      error ('griddatan: "v4" METHOD is available for 2-D inputs by using "griddata"');
    endif
    yi = griddata (x(:,1), x(:,2), y, xi(:,1), xi(:,2), "v4");
    return;
  elseif (any (strcmp (method, {"cubic", "natural"})))
    error ('griddatan: "%s" interpolation not yet implemented', method);
  elseif (! any (strcmp (method, {"linear", "nearest"})))
    error ('griddatan: unknown interpolation METHOD: "%s"', method);
  endif
  yi = NaN (mi, 1);
  tri = delaunayn (x);
  if (strcmp (method, "nearest"))
    idx = dsearchn (x, tri, xi);
    valid = ! isnan (idx);
    yi(valid) = y(idx(valid));
  else
    [tri_list, bary] = tsearchn (x, tri, xi);
    valid = ! isnan (tri_list);
    tri = tri(tri_list(valid), :);
    yi(valid) = sum (reshape (y(tri), size (tri)) .* bary(valid,:), 2);
  endif
endfunction
