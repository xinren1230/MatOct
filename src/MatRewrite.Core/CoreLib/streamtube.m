## MatRewrite CoreLib: streamtube (basic tubes drawn as surfaces)
function h = streamtube (varargin)
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("streamtube", varargin{:});
  opts = []; xyz = []; div = []; dia = [];
  switch (nargs)
    case {2, 3}
      xyz = varargin{1};
      if (iscell (varargin{2}) || (nargs == 2 && numel (varargin{2}) == 1)) dia = varargin{2};
      else div = varargin{2}; [x, y, z] = meshgrid (1:columns (div), 1:rows (div), 1:size (div, 3)); endif
      if (nargs == 3) opts = varargin{3}; endif
    case 5
      [xyz, x, y, z, div] = varargin{:};
    case 6
      if (iscell (varargin{1})) [xyz, x, y, z, div, opts] = varargin{:};
      else [u, v, w, sx, sy, sz] = varargin{:}; [x, y, z] = meshgrid (1:columns (u), 1:rows (u), 1:size (u, 3)); endif
    case 7
      [u, v, w, sx, sy, sz, opts] = varargin{:}; [x, y, z] = meshgrid (1:columns (u), 1:rows (u), 1:size (u, 3));
    case {9, 10}
      [x, y, z, u, v, w, sx, sy, sz] = varargin{1:9}; if (nargs == 10) opts = varargin{10}; endif
    otherwise
      print_usage ();
  endswitch
  scale = 1; nc = 20;
  if (! isempty (opts))
    if (numel (opts) > 2) error ("streamtube: OPTIONS must be a 1- or 2-element vector"); endif
    scale = opts(1);
    if (! isreal (scale) || scale <= 0) error ("streamtube: SCALE must be a real scalar > 0"); endif
    if (numel (opts) == 2)
      nc = opts(2);
      if (! isreal (nc) || nc < 3) error ("streamtube: number of tube vertices N must be greater than 2"); endif
      nc = fix (nc);
    endif
  endif
  if (isempty (xyz)) xyz = stream3 (x, y, z, u, v, w, sx, sy, sz, 0.2); endif
  if (! iscell (xyz)) xyz = {xyz}; endif
  if (iscell (dia))
    for i = 1:numel (xyz)
      if (rows (dia{i}) != rows (xyz{i})) error ("streamtube: DIA must have same length then XYZ"); endif
    endfor
  endif
  if (isempty (hax)) hax = gca (); else hax = hax(1); endif
  hh = [];
  t = linspace (0, 2*pi, nc + 1).';
  for i = 1:numel (xyz)
    P = xyz{i};
    if (rows (P) < 2) continue; endif
    n = rows (P);
    if (iscell (dia)) r = dia{i}(:) / 2;
    elseif (! isempty (dia)) r = repmat (dia / 2, n, 1);
    else r = repmat (0.5, n, 1); endif
    r = scale * r;
    T = [diff(P); P(end,:) - P(end-1,:)];
    X = zeros (nc + 1, n); Y = X; Z = X;
    for k = 1:n
      tk = T(k,:) / max (norm (T(k,:)), eps);
      a = [1 0 0]; if (abs (tk(1)) > 0.9) a = [0 1 0]; endif
      n1 = cross (tk, a); n1 /= norm (n1); n2 = cross (tk, n1);
      C = P(k,:) + r(k) * (cos (t) * n1 + sin (t) * n2);
      X(:,k) = C(:,1); Y(:,k) = C(:,2); Z(:,k) = C(:,3);
    endfor
    hh(end+1,1) = surface (X, Y, Z, "parent", hax, "edgecolor", "none");
  endfor
  if (nargout > 0) h = hh; endif
endfunction
