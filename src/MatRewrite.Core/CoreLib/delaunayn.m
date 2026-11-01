## MatRewrite - GPL-3.0-or-later
## T = delaunayn (pts, options): Delaunay triangulation of N-D points (rows of PTS).
## Simplices are positively oriented; zero-volume simplices are removed.
function T = delaunayn (pts, varargin)
  if (nargin < 1)
    print_usage ();
  endif
  if (! (isnumeric (pts) && ndims (pts) == 2))
    error ("delaunayn: PTS must be a 2-dimensional numeric array");
  endif
  if (any (! isfinite (pts(:))))
    error ("delaunayn: PTS must not contain Inf or NaN values");
  endif
  T = __delaunayn__ (double (pts));
endfunction
