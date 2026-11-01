## MatRewrite CoreLib: ranks
function y = ranks (x, dim, rtype = 0)
  if (nargin < 1) print_usage (); endif
  if (! (isnumeric (x) || islogical (x))) error ("ranks: X must be a numeric or logical array"); endif
  sz = size (x); nd = ndims (x);
  if (nargin < 2 || isempty (dim))
    dim = find (sz != 1, 1); if (isempty (dim)) dim = 1; endif
  elseif (! (isscalar (dim) && dim == fix (dim) && dim >= 1))
    error ("ranks: DIM must be an integer and a valid dimension");
  endif
  if (ischar (rtype))
    switch (lower (rtype))
      case "fractional" rt = 0;
      case "competition" rt = 1;
      case "modified" rt = 2;
      case "dense" rt = 3;
      case "ordinal" rt = 4;
      otherwise error ("ranks: unknown RTYPE '%s'", rtype);
    endswitch
  else
    rt = rtype;
    if (! any (rt == 0:4)) error ("ranks: unknown RTYPE '%d'", rt); endif
  endif
  if (dim > nd) sz(end+1:dim) = 1; endif
  p = [dim, 1:dim-1, dim+1:numel(sz)];
  a = reshape (permute (x, p), sz(dim), []);
  y = zeros (size (a));
  n = rows (a);
  for j = 1:columns (a)
    [s, i] = sort (a(:,j));
    r = zeros (n, 1);
    k = 1; d = 0;
    while (k <= n)
      m = k; while (m < n && s(m+1) == s(k)) m++; endwhile
      d++;
      switch (rt)
        case 0, v = (k + m) / 2 * ones (m-k+1, 1);
        case 1, v = k * ones (m-k+1, 1);
        case 2, v = m * ones (m-k+1, 1);
        case 3, v = d * ones (m-k+1, 1);
        case 4, v = (k:m).';
      endswitch
      r(i(k:m)) = v;
      k = m + 1;
    endwhile
    y(:,j) = r;
  endfor
  y = ipermute (reshape (y, sz(p)), p);
endfunction
