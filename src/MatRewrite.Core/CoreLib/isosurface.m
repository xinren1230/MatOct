## MatRewrite CoreLib: isosurface via native marching cubes.
function varargout = isosurface (varargin)
  if (nargin < 1 || nargin > 8)
    print_usage ();
  endif
  nout = nargout;
  noshare = false;
  nin = nargin;
  ns = 0;
  for k = nin:-1:max (nin-1, 1)
    s = varargin{k};
    if (! ischar (s))
      break;
    endif
    switch (lower (s))
      case {"v", "verbose", ""}
        ns++;
      case {"n", "noshare"}
        noshare = true; ns++;
      otherwise
        error ("isosurface: parameter '%s' not supported", s);
    endswitch
  endfor
  args = varargin(1:nin-ns);
  x = y = z = isoval = col = [];
  switch (numel (args))
    case 1
      v = args{1};
    case 2
      v = args{1};
      if (isscalar (args{2}) || isempty (args{2})) isoval = args{2}; else col = args{2}; endif
    case 3
      [v, isoval, col] = args{:};
    case 4
      [x, y, z, v] = args{:};
    case 5
      [x, y, z, v] = args{1:4};
      if (isscalar (args{5}) || isempty (args{5})) isoval = args{5}; else col = args{5}; endif
    case 6
      [x, y, z, v, isoval, col] = args{:};
    otherwise
      error ("isosurface: incorrect number of input arguments");
  endswitch
  [x, y, z] = __iso_grid__ ("isosurface", x, y, z, v);
  if (isempty (isoval))
    isoval = __calc_isovalue_from_data__ (v);
  endif
  if (! isscalar (isoval))
    error ("isosurface: ISOVAL must be a scalar");
  endif
  if (! isempty (col))
    if (! size_equal (v, col))
      error ("isosurface: COL must match the size of V");
    endif
    if (nout == 2)
      warning ("isosurface: colors will be calculated, but no output argument to receive it");
    endif
  elseif (nout >= 3)
    error ("isosurface: COL must be passed to return C");
  endif
  if (isempty (col))
    [f, p] = __marching_cube__ (x, y, z, double (v), isoval);
    c = [];
  else
    [f, p, c] = __marching_cube__ (x, y, z, double (v), isoval, double (col));
  endif
  if (isempty (f) || isempty (p))
    warning ("isosurface: triangulation is empty");
  elseif (! noshare)
    [f, p, J] = __unite_shared_vertices__ (f, p);
    if (! isempty (c)) c = c(J); endif
  endif
  fv.faces = f; fv.vertices = p;
  if (! isempty (col)) fv.facevertexcdata = c; endif
  switch (nout)
    case 0
      if (isempty (c)) fc = isoval; else fc = c; endif
      hax = gca ();
      pa = patch ("Faces", f, "Vertices", p, "FaceVertexCData", fc, "FaceColor", "flat", "EdgeColor", "none");
      if (! ishold (hax)) view (hax, [-37.5, 30]); endif
    case 1
      varargout = {fv};
    case 2
      varargout = {f, p};
    otherwise
      varargout = {f, p, c};
  endswitch
endfunction
