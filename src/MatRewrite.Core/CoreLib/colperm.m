function p = colperm (s)
  ## Column permutation ordering columns by increasing nonzero count.
  if (nargin != 1) print_usage (); endif
  [~, p] = sort (full (sum (s != 0, 1)));
endfunction
