function v = xticks (varargin)
  if nargin == 0, v = get (gca, 'XTick'); else set (gca, 'XTick', varargin{end}); end
end
