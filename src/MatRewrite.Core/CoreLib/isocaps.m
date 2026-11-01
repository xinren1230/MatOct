## MatRewrite CoreLib: isocaps -- end caps built by running isosurface on a
## two-layer slab (padding layer + boundary layer) for each requested face.
function varargout = isocaps (varargin)
  if (nargin < 1 || nargin > 8) print_usage (); endif
  caps = "above"; plane = "all"; verb = ""; ns = 0;
  for k = nargin:-1:max (nargin-2, 1)
    a = varargin{k};
    if (! ischar (a)) break; endif
    switch (lower (a))
      case {"v", "verbose"} verb = "verbose";
      case {"all", "xmin", "xmax", "ymin", "ymax", "zmin", "zmax"} plane = lower (a);
      case {"above", "a", "below", "b"} caps = lower (a);
      otherwise error ("isocaps: parameter '%s' not supported", a);
    endswitch
    ns++;
  endfor
  x = y = z = []; iso = [];
  switch (nargin - ns)
    case 1, v = varargin{1};
    case 2, [v, iso] = varargin{1:2};
    case 4, [x, y, z, v] = varargin{1:4};
    case 5, [x, y, z, v, iso] = varargin{1:5};
    otherwise error ("isocaps: incorrect number of input arguments");
  endswitch
  sz = size (v);
  if (ndims (v) != 3 || any (sz < 2))
    error ("isocaps: V must be a non-singleton 3-dimensional matrix");
  endif
  if (isempty (x)) x = 1:sz(2); endif
  if (isempty (y)) y = 1:sz(1); endif
  if (isempty (z)) z = 1:sz(3); endif
  if (isvector (x) && numel (x) == sz(2)) x = repmat (x(:).', [sz(1) 1 sz(3)]);
  elseif (! size_equal (v, x)) error ("isocaps: X must match the size of V"); endif
  if (isvector (y) && numel (y) == sz(1)) y = repmat (y(:), [1 sz(2) sz(3)]);
  elseif (! size_equal (v, y)) error ("isocaps: Y must match the size of V"); endif
  if (isvector (z) && numel (z) == sz(3)) z = repmat (reshape (z(:), 1, 1, []), [sz(1) sz(2) 1]);
  elseif (! size_equal (v, z)) error ("isocaps: Z must match the size of V"); endif
  if (isempty (iso)) iso = __calc_isovalue_from_data__ (v); endif
  if (! isscalar (iso) || ! isnumeric (iso)) error ("isocaps: ISOVAL must be a scalar number"); endif
  if (any (strcmp (caps, {"a", "above"}))) pad = min ([v(:); iso]) - 1;
  else pad = max ([v(:); iso]) + 1; endif
  if (strcmp (plane, "all")) planes = {"xmin","xmax","ymin","ymax","zmin","zmax"};
  else planes = {plane}; endif
  F = zeros (0, 3); V = zeros (0, 3);
  for i = 1:numel (planes)
    [f, vv] = cap1 (x, y, z, v, iso, pad, planes{i}, verb);
    F = [F; f + rows(V)]; V = [V; vv];
  endfor
  C = [];
  if (! isempty (V)) C = interp3 (x, y, z, v, V(:,1), V(:,2), V(:,3)); endif
  switch (nargout)
    case 0
      patch ("Faces", F, "Vertices", V, "FaceVertexCData", C, "FaceColor", "interp", "EdgeColor", "none");
    case 1
      s.vertices = V; s.faces = F; s.facevertexcdata = C; varargout = {s};
    otherwise
      varargout = {F, V, C};
  endswitch
endfunction

function [f, vv] = cap1 (x, y, z, v, iso, pad, pl, verb)
  sz = size (v); lo = strcmp (pl(2:end), "min");
  switch (pl(1))
    case "x", d = 2; c = 1;
    case "y", d = 1; c = 2;
    case "z", d = 3; c = 3;
  endswitch
  if (lo) k = 1; else k = sz(d); endif
  idx = {":", ":", ":"}; idx{d} = k;
  cs = sz; cs(d) = 2;
  cd = repmat (pad, cs);
  j2 = {":", ":", ":"}; j2{d} = 2;
  cd(j2{:}) = v(idx{:});
  r = {1:cs(1), 1:cs(2), 1:cs(3)};
  G = {x, y, z};
  cval = G{c}(idx{:}); cval = cval(1);
  [f, vv] = isosurface (x(r{:}), y(r{:}), z(r{:}), cd, iso, verb);
  if (! isempty (vv)) vv(:,c) = cval; endif
endfunction
