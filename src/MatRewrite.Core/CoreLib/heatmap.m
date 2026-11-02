function h = heatmap (varargin)
  % HEATMAP  Heat map of a matrix with value labels.
  C = varargin{end};
  if nargin >= 3 && ~ischar (varargin{1}), C = varargin{3}; end
  hh = imagesc (C); colorbar;
  for i = 1:rows (C)
    for j = 1:columns (C)
      text (j, i, sprintf ('%g', C(i,j)), 'HorizontalAlignment', 'center');
    end
  end
  if nargout > 0, h = hh; end
end
