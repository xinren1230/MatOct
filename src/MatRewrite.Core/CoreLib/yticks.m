function v = yticks (varargin)
  if nargin == 0, v = get (gca, 'YTick'); else set (gca, 'YTick', varargin{end}); end
end
