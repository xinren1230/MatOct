function h = fill (x, y, c, varargin)
  % FILL  Filled 2-D polygons.
  hh = patch (x, y, c, varargin{:});
  if nargout > 0, h = hh; end
end
