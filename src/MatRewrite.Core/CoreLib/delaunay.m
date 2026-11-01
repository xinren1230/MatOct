## MatRewrite - GPL-3.0-or-later
## tri = delaunay (x, y), delaunay (x, y, z), delaunay (X), delaunay (..., options)
function tri = delaunay (varargin)
  if (nargin < 1 || nargin > 4)
    print_usage ();
  endif
  [pts, ~] = __geom_args__ ("delaunay", varargin{:});
  tri = delaunayn (pts);
  if (isempty (tri))
    tri = zeros (0, columns (pts) + 1);
  endif
endfunction
