## MatRewrite - GPL-3.0-or-later
## [idx, d] = dsearchn (x, tri, xi, outval): index of the closest point of X to each row of XI.
## With OUTVAL, points outside the triangulation TRI get OUTVAL.
function [idx, d] = dsearchn (x, tri, xi, outval)
  if (nargin < 2)
    print_usage ();
  endif
  if (nargin == 2)
    xi = tri;  tri = [];
  endif
  nxi = rows (xi);
  idx = zeros (nxi, 1);  d = zeros (nxi, 1);
  for i = 1:nxi
    [d(i), idx(i)] = min (sumsq (x - xi(i,:), 2));
  endfor
  d = sqrt (d);
  if (nargin == 4)
    out = isnan (tsearchn (x, tri, xi));
    idx(out) = outval;
    d(out) = outval;
  endif
endfunction
