## MatRewrite CoreLib: run_count -- histogram of upward-run lengths (runs >= N go to bin N).
function retval = run_count (x, n, dim)
  if (nargin != 2 && nargin != 3) print_usage (); endif
  if (! (isnumeric (x) || islogical (x))) error ("run_count: X must be a numeric or logical array"); endif
  if (! (isscalar (n) && n == fix (n) && n > 0)) error ("run_count: N must be a positive integer"); endif
  sz = size (x);
  if (nargin < 3)
    dim = find (sz > 1, 1); if (isempty (dim)) dim = 1; endif
  elseif (! (isscalar (dim) && dim == fix (dim) && dim >= 1 && dim <= numel (sz)))
    error ("run_count: DIM must be an integer and a valid dimension");
  endif
  p = [dim, 1:dim-1, dim+1:numel(sz)];
  a = reshape (permute (x, p), sz(dim), []);
  r = zeros (n, columns (a));
  for j = 1:columns (a)
    c = a(:,j);
    br = [0; find(diff (c) < 0); numel(c)];
    L = min (diff (br), n);
    for k = 1:numel (L) r(L(k), j)++; endfor
  endfor
  osz = sz(p); osz(1) = n;
  retval = ipermute (reshape (r, osz), p);
endfunction
