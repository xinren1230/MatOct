function v = xticklabels (varargin)
  if nargin == 0, v = get (gca, 'XTickLabel'); else set (gca, 'XTickLabel', varargin{end}); end
end
