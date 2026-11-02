function A = __accumdim_sum__ (subs, vals, dim, n)
  sz = size (vals);
  if (nargin < 3 || isempty (dim))
    dim = find (sz != 1, 1);
    if (isempty (dim)) dim = 1; endif
  endif
  if (nargin < 4 || isempty (n) || n == 0)
    n = max (subs(:));
  endif
  sz(end+1:dim) = 1;
  osz = sz; osz(dim) = n;
  A = zeros (osz, class (vals));
  idx = repmat ({':'}, 1, numel (sz));
  jdx = idx;
  for k = 1:numel (subs)
    idx{dim} = subs(k); jdx{dim} = k;
    A(idx{:}) += vals(jdx{:});
  endfor
endfunction
