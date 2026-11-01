## MatRewrite CoreLib: isonormals
function vn = isonormals (varargin)
  if (nargin < 2 || nargin > 6) print_usage (); endif
  negate = false;
  if (ischar (varargin{end}))
    if (strcmpi (varargin{end}, "negate"))
      negate = true;
    else
      error ("isonormals: Unknown option '%s'", varargin{end});
    endif
    varargin(end) = [];
  endif
  switch (numel (varargin))
    case 2
      val = varargin{1};
      x = 1:columns (val); y = 1:rows (val); z = 1:size (val, 3);
      vp = varargin{2};
    case 5
      [x, y, z, val, vp] = varargin{:};
    otherwise
      print_usage ();
  endswitch
  if (isnumeric (vp) && columns (vp) == 3)
    pa = []; v = vp;
  elseif (isscalar (vp) && ishghandle (vp) && strcmp (get (vp, "type"), "patch"))
    pa = vp; v = get (pa, "vertices");
  else
    error ("isonormals: input must be a list of vertices or a patch handle");
  endif
  n = __interp_cube__ (x, y, z, val, v, "normals");
  if (! negate) n = -n; endif
  if (nargout == 0 && ! isempty (pa))
    set (pa, "VertexNormals", n);
  else
    vn = n;
  endif
endfunction
