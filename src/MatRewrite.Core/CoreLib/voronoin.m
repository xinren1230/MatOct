## MatRewrite - GPL-3.0-or-later
## [C, F] = voronoin (pts, options): Voronoi vertices C (first row Inf) and cells F.
function [C, F] = voronoin (pts, options)
  if (nargin < 1)
    print_usage ();
  endif
  [np, dims] = size (pts);
  if (np <= dims)
    error ("voronoin: number of points must be greater than their dimension");
  endif
  [C, F] = __voronoi__ ("voronoin", pts);
endfunction
