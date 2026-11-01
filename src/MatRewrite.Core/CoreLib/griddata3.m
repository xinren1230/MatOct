## MatRewrite - GPL-3.0-or-later
## vi = griddata3 (x, y, z, v, xi, yi, zi, method): scattered 3-D interpolation.
function vi = griddata3 (x, y, z, v, xi, yi, zi, method = "linear", varargin)
  if (nargin < 7)
    print_usage ();
  endif
  if (isvector (x) && isvector (y) && isvector (z) && isvector (v))
    if (! isequal (length (x), length (y), length (z), length (v)))
      error ("griddata: X, Y, Z, and V must be vectors of the same length");
    endif
  elseif (! size_equal (x, y, z, v))
    error ("griddata: X, Y, Z, and V must have equal sizes");
  endif
  if (isvector (xi) && isvector (yi) && isvector (zi))
    [xi, yi, zi] = meshgrid (xi, yi, zi);
  elseif (! size_equal (xi, yi, zi))
    error ("griddata: XI, YI, and ZI must be vectors or matrices of the same size");
  endif
  vi = griddatan ([x(:), y(:), z(:)], v(:), [xi(:), yi(:), zi(:)], method, varargin{:});
  vi = reshape (vi, size (xi));
endfunction
