function s = spconvert (m)
  ## Convert a [i, j, v] (or [i, j, re, im]) triplet matrix to a sparse matrix.
  if (nargin != 1) print_usage (); endif
  if (issparse (m))
    s = m;
  else
    sz = columns (m);
    if (sz != 3 && sz != 4)
      error ("spconvert: argument must be sparse or real matrix with 3 or 4 columns");
    endif
    if (sz == 3)
      s = sparse (m(:,1), m(:,2), m(:,3));
    else
      s = sparse (m(:,1), m(:,2), m(:,3) + 1i * m(:,4));
    endif
  endif
endfunction
