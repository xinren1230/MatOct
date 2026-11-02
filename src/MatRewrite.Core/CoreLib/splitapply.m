function varargout = splitapply (fcn, varargin)
  % SPLITAPPLY  Split data into groups (from findgroups) and apply a function.
  G = varargin{end}; data = varargin(1:end-1); ng = max (G);
  out = cell (ng, max (1, nargout));
  for g = 1:ng
    sel = (G(:) == g); args = cell (1, numel (data));
    for k = 1:numel (data)
      d = data{k}; if isvector (d), args{k} = d(sel); else, args{k} = d(sel, :); end
    end
    [out{g, :}] = fcn (args{:});
  end
  for k = 1:max (1, nargout)
    col = out(:, k);
    if all (cellfun (@(x) (isnumeric (x) || islogical (x)) && isscalar (x), col)), varargout{k} = cell2mat (col); else, varargout{k} = col; end
  end
end
