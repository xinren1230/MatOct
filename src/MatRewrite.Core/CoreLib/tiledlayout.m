function t = tiledlayout (varargin)
  % TILEDLAYOUT  Create a tiled chart layout in the current figure.
  r = 0; c = 0; k = 1;
  if nargin > 0 && ischar (varargin{1})
    r = 0; c = 0; k = 2;      % 'flow'
  elseif nargin >= 2
    r = varargin{1}; c = varargin{2}; k = 3;
  end
  f = gcf (); clf (f);
  setappdata (f, '__tiled__', [r c 0]);
  if nargout > 0, t = f; end
end
