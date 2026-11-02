function h = sgtitle (varargin)
  % SGTITLE  Title for a grid of subplots.
  if nargin >= 2 && isnumeric (varargin{1}) && ~ischar (varargin{1}), varargin(1) = []; end
  txt = varargin{1};
  if iscell (txt), txt = strjoin (txt, ' '); end
  a = annotation ('textbox', [0 0.94 1 0.06], 'String', txt, 'HorizontalAlignment', 'center', 'LineStyle', 'none', 'FontSize', 12);
  if nargout > 0, h = a; end
end
