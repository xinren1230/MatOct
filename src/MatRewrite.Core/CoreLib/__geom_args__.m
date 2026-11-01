## MatRewrite - GPL-3.0-or-later
## Shared argument parsing for delaunay/convhull: returns the point matrix and options.
function [pts, opts] = __geom_args__ (caller, varargin)
  n = numel (varargin);
  opts = {};
  if (n == 1 || (n == 2 && ! isnumeric (varargin{2})))
    pts = varargin{1};
    if (columns (pts) != 2 && columns (pts) != 3)
      error ("%s: X must be a matrix with 2 or 3 columns", caller);
    endif
    if (n == 2)
      opts = varargin{2};
    endif
  else
    nc = n;
    if (n >= 3 && ! isnumeric (varargin{n}))
      opts = varargin{n};  nc = n - 1;
    elseif (n == 4)
      opts = varargin{4};  nc = 3;
    endif
    x = varargin{1};  y = varargin{2};
    if (nc == 3 && ! size_equal (x, y, varargin{3}))
      error ("%s: X, Y, and Z must be the same size", caller);
    elseif (! size_equal (x, y))
      error ("%s: X and Y must be the same size", caller);
    endif
    if (nc == 3)
      z = varargin{3};
      pts = [x(:), y(:), z(:)];
    else
      pts = [x(:), y(:)];
    endif
  endif
  if (! (ischar (opts) || iscellstr (opts) || (isnumeric (opts) && isempty (opts))))
    error ("%s: OPTIONS must be a string or cell array of strings", caller);
  endif
endfunction
