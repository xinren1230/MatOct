function exportgraphics (obj, file, varargin)
  % EXPORTGRAPHICS  Save a figure or axes to an image file.
  if isfigure (obj), f = obj; else f = gcf (); end
  res = 150;
  for k = 1:2:numel (varargin)
    if strcmpi (varargin{k}, 'Resolution'), res = varargin{k+1}; end
  end
  print (f, file, sprintf ('-r%d', res));
end
