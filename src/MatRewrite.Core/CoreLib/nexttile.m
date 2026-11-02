function h = nexttile (varargin)
  % NEXTTILE  Create the next axes of a tiled chart layout.
  f = gcf ();
  st = getappdata (f, '__tiled__');
  if isempty (st), st = [0 0 0]; end
  idx = st(3) + 1; span = [1 1];
  if nargin >= 1 && isnumeric (varargin{1}) && numel (varargin{1}) == 1, idx = varargin{1}; end
  if nargin >= 1 && isnumeric (varargin{end}) && numel (varargin{end}) == 2, span = varargin{end}; end
  r = st(1); c = st(2);
  if r == 0
    n = max (idx, 1); c = ceil (sqrt (n)); r = ceil (n / c);
  end
  row = floor ((idx - 1) / c); col = mod (idx - 1, c);
  cells = [];
  for i = 0:span(1)-1
    for j = 0:span(2)-1
      cells(end+1) = (row + i) * c + col + j + 1;
    end
  end
  st(3) = max (st(3), idx + span(2) - 1);
  setappdata (f, '__tiled__', st);
  ax = subplot (r, c, cells);
  if nargout > 0, h = ax; end
end
