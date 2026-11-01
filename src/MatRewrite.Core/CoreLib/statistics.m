## MatRewrite CoreLib: statistics -- [min q1 median q3 max mean std skewness kurtosis].
function stats = statistics (x, varargin)
  if (nargin < 1) print_usage (); endif
  if (! (isnumeric (x) && ! islogical (x))) error ("statistics: X must be a numeric array"); endif
  omit = false; dim = [];
  for k = 1:numel (varargin)
    a = varargin{k};
    if (ischar (a))
      switch (lower (a))
        case "omitnan", omit = true;
        case "includenan", omit = false;
        case "all", dim = "all";
        otherwise error ("statistics: NANFLAG must be either 'omitnan' or 'includenan'");
      endswitch
    else
      dim = a;
    endif
  endfor
  sz = size (x);
  if (ischar (dim)) x = x(:); sz = size (x); dim = 1; endif
  if (isempty (dim)) dim = find (sz != 1, 1); if (isempty (dim)) dim = 1; endif; endif
  dim = sort (dim(:).');
  sz(end+1:max (dim)) = 1;
  dd = dim(dim <= numel (sz));
  if (prod (sz(dd)) < 2)
    if (all (dim > ndims (x)) || numel (x) == 1)
      if (numel (x) == 1) error ("statistics: dimension of X is too small (<2)"); endif
    endif
    if (all (sz(dd) == 1))
      if (numel (x) == 1) error ("statistics: dimension of X is too small (<2)"); endif
      stats = x; return;
    endif
  endif
  dd = dd(sz(dd) > 1);
  rest = setdiff (1:numel (sz), dd);
  p = [dd, rest];
  a = reshape (permute (x, p), prod (sz(dd)), []);
  m = columns (a); s = zeros (9, m);
  for j = 1:m
    c = a(:,j); if (omit) c = c(! isnan (c)); endif
    q = quantile (c, [0.25; 0.75]);
    s(:,j) = [min(c); q(1); median(c); q(2); max(c); mean(c); std(c); skewness(c); kurtosis(c)];
  endfor
  osz = sz; osz(dd) = 1; osz(dd(1)) = 9;
  stats = zeros (osz);
  % place the 9 statistics along the first reduced dimension
  op = [dd(1), setdiff(1:numel (sz), dd(1))];
  t = reshape (s, [9, osz(op(2:end))]);
  stats = ipermute (t, op);
endfunction
