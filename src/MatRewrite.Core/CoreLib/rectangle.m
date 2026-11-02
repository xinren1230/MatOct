function h = rectangle (varargin)
  % RECTANGLE  Draw a rectangle (Position [x y w h]).
  p = [0 0 1 1]; fc = 'none'; ec = [0 0 0];
  for k = 1:2:numel (varargin)
    switch lower (varargin{k})
      case 'position', p = varargin{k+1};
      case 'facecolor', fc = varargin{k+1};
      case 'edgecolor', ec = varargin{k+1};
    end
  end
  x = p(1) + [0 p(3) p(3) 0]; y = p(2) + [0 0 p(4) p(4)];
  if ischar (fc) && strcmp (fc, 'none')
    hh = line ([x x(1)], [y y(1)], 'Color', ec);
  else
    hh = patch (x, y, fc);
  end
  if nargout > 0, h = hh; end
end
