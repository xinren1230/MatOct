## MatRewrite CoreLib: interpn (linear / nearest; ndgrid orientation)
function vi = interpn (varargin)
  if (nargin < 1) print_usage (); endif
  method = "linear"; extrap = NA;
  args = varargin;
  if (numel (args) > 1 && ischar (args{end}))
    method = lower (args{end}); args(end) = [];
  elseif (numel (args) > 2 && ischar (args{end-1}))
    method = lower (args{end-1}); extrap = args{end}; args(end-1:end) = [];
  endif
  na = numel (args);
  if (na == 1 || na == 2)
    v = args{1}; nd = ndims (v); n = 1; if (na == 2) n = args{2}; endif
    x = cell (1, nd); y = cell (1, nd);
    for k = 1:nd x{k} = 1:size (v, k); y{k} = 1:2^(-n):size (v, k); endfor
    [y{:}] = ndgrid (y{:});
  else
    if (mod (na, 2) == 1)
      nd = (na - 1) / 2; x = args(1:nd); v = args{nd+1}; y = args(nd+2:end);
    else
      v = args{1}; nd = na - 1; y = args(2:end);
      if (isvector (v) && nd == 1) v = v(:); endif
      x = cell (1, nd); for k = 1:nd x{k} = 1:size (v, k); endfor
    endif
    if (ndims (v) > nd && nd > 1) error ("interpn: dimensional mismatch"); endif
  endif
  for k = 1:nd
    if (! isvector (x{k}))
      idx = num2cell (ones (1, nd)); idx{k} = ":"; x{k} = x{k}(idx{:});
    endif
    x{k} = x{k}(:);
  endfor
  if (all (cellfun (@isvector, y)) && ! size_equal (y{:}))
    [y{:}] = ndgrid (y{:});
  endif
  if (! size_equal (y{:})) error ("interpn: dimensional mismatch"); endif
  switch (method)
    case "linear"
      vi = __lin_interpn__ (x{:}, v, y{:});
    case "nearest"
      sz = size (y{1}); idx = cell (1, nd); out = false (sz);
      for k = 1:nd
        xk = x{k}; yk = y{k}(:);
        ii = interp1 (xk, 1:numel (xk), yk, "nearest");
        out(:) = out(:) | isnan (ii); ii(isnan (ii)) = 1; idx{k} = ii;
      endfor
      vi = v(sub2ind (size (v), idx{:})); vi = reshape (vi, sz); vi(out) = NA;
    otherwise
      vi = __lin_interpn__ (x{:}, v, y{:});
  endswitch
  if (! isna (extrap) || ! isnumeric (extrap))
    vi(isnan (vi)) = extrap;
  endif
endfunction
