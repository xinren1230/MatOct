function [y, h] = mgorth (x, v)
  ## Modified Gram-Schmidt: orthogonalize X against the columns of V.
  if (nargin != 2) print_usage (); endif
  k = columns (v);
  h = zeros (k + 1, 1);
  y = x;
  for j = 1:k
    h(j) = v(:,j)' * y;
    y = y - h(j) * v(:,j);
  endfor
  h(k+1) = norm (y);
  if (h(k+1) != 0) y = y / h(k+1); endif
endfunction
