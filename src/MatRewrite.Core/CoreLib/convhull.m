## MatRewrite - GPL-3.0-or-later
## [H, V] = convhull (x, y), convhull (x, y, z), convhull (X), convhull (..., options)
## 2-D: closed counter-clockwise index loop; 3-D: outward-oriented triangles.
function [H, V] = convhull (varargin)
  if (nargin < 1 || nargin > 4)
    print_usage ();
  endif
  [pts, ~] = __geom_args__ ("convhull", varargin{:});
  if (columns (pts) == 2)
    [H, V] = __convhull2__ (pts(:,1), pts(:,2));
  else
    [H, V] = convhulln (pts);
  endif
endfunction
