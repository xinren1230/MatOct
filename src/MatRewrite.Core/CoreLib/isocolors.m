## MatRewrite CoreLib: isocolors
function varargout = isocolors (varargin)
  if (nargin != 2 && nargin != 4 && nargin != 5 && nargin != 7) print_usage (); endif
  vp = varargin{end};
  if (isnumeric (vp) && columns (vp) == 3)
    pa = []; v = vp;
  elseif (isscalar (vp) && ishghandle (vp) && strcmp (get (vp, "type"), "patch"))
    pa = vp; v = get (pa, "vertices");
  else
    error ("isocolors: last argument must be a vertex list or patch handle");
  endif
  switch (nargin)
    case 2
      c = varargin{1}; [x, y, z] = meshgrid (1:columns (c), 1:rows (c), 1:size (c, 3));
      nc = __iso_color__ (x, y, z, c, v);
    case 4
      [r, g, b] = varargin{1:3}; [x, y, z] = meshgrid (1:columns (r), 1:rows (r), 1:size (r, 3));
      nc = [__iso_color__(x,y,z,r,v), __iso_color__(x,y,z,g,v), __iso_color__(x,y,z,b,v)];
    case 5
      [x, y, z, c] = varargin{1:4}; nc = __iso_color__ (x, y, z, c, v);
    case 7
      [x, y, z, r, g, b] = varargin{1:6};
      nc = [__iso_color__(x,y,z,r,v), __iso_color__(x,y,z,g,v), __iso_color__(x,y,z,b,v)];
  endswitch
  if (nargout == 0)
    if (! isempty (pa)) set (pa, "FaceVertexCData", nc); endif
  else
    varargout{1} = nc;
  endif
endfunction

function c = __iso_color__ (x, y, z, v, p)
  c = interp3 (x, y, z, v, p(:,1), p(:,2), p(:,3));
endfunction
