function v = yticklabels (varargin)
  if nargin == 0, v = get (gca, 'YTickLabel'); else set (gca, 'YTickLabel', varargin{end}); end
end
