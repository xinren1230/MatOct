## MatRewrite CoreLib: shared engine for quiver/quiver3 (hggroup of stem, head, base lines).
function hg = __quiv__ (fn, is3d, varargin)
  [hax, varargin] = __plt_get_axis_arg__ (fn, varargin{:});
  autoscale = 0.9;
  nnum = find (cellfun ("ischar", varargin), 1) - 1;
  if (isempty (nnum)) nnum = numel (varargin); endif
  if (nnum < numel (varargin) && strcmpi (varargin{nnum+1}, "off"))
    varargin{nnum+1} = 0; nnum++;
  endif
  for k = 1:nnum varargin{k} = double (varargin{k}); endfor
  FN = fn;
  if (is3d)
    switch (nnum)
      case {4, 5}
        [z, u, v, w] = varargin{1:4};
        if (isvector (z) && ! isvector (u))
          if (! size_equal (u, v, w)) error ("quiver3: U, V, and W must be the same size"); endif
          if (numel (z) != size (u, 3)) error ("quiver3: Z vector length must equal size of U, V, and W in dim 3"); endif
          [x, y, z] = meshgrid (1:columns (u), 1:rows (u), z);
        else
          if (! size_equal (z, u, v, w)) error ("quiver3: Z, U, V, and W must be the same size"); endif
          [x, y] = meshgrid (1:columns (u), 1:rows (u), 1:size (u, 3));
        endif
      case {6, 7}
        [x, y, z, u, v, w] = varargin{1:6};
        if (isvector (x) && isvector (y) && isvector (z) && ! isvector (u))
          if (! size_equal (u, v, w)) error ("quiver3: U, V, and W must be the same size"); endif
          if (numel (x) != columns (u)) error ("quiver3: X vector length must equal number of columns in U, V, and W"); endif
          if (numel (y) != rows (u)) error ("quiver3: Y vector length must equal number of rows in U, V, and W"); endif
          if (numel (z) != size (u, 3)) error ("quiver3: Z vector length must equal size of U, V, and W in dim 3"); endif
          [x, y, z] = meshgrid (x, y, z);
        elseif (! size_equal (x, y, z, u, v, w))
          error ("quiver3: X, Y, Z, U, V, and W must be the same size");
        endif
      otherwise
        print_usage ("quiver3");
    endswitch
  else
    switch (nnum)
      case {2, 3}
        [u, v] = varargin{1:2};
        if (! size_equal (u, v)) error ("quiver: U and V must be the same size"); endif
        [x, y] = meshgrid (1:columns (u), 1:rows (u));
      case {4, 5}
        [x, y, u, v] = varargin{1:4};
        if (isvector (x) && isvector (y) && (! isvector (u) || ! isvector (v)))
          if (! size_equal (u, v)) error ("quiver: U and V must be the same size"); endif
          if (numel (x) != columns (u)) error ("quiver: X vector length must equal number of columns in U and V"); endif
          if (numel (y) != rows (u)) error ("quiver: Y vector length must equal number of rows in U and V"); endif
          [x, y] = meshgrid (x, y);
        elseif (! size_equal (x, y, u, v))
          error ("quiver: X, Y, U, and V must be the same size");
        endif
      otherwise
        print_usage ("quiver");
    endswitch
    z = w = [];
  endif
  if ((is3d && any (nnum == [5 7])) || (! is3d && any (nnum == [3 5])))
    autoscale = varargin{nnum};
    if (! isscalar (autoscale) || autoscale < 0)
      error ("%s: scaling factor must be a non-negative scalar or 'off'", fn);
    endif
  endif
  filled = false; lc = []; ls = "-"; mk = "none"; props = {};
  k = nnum + 1;
  while (k <= numel (varargin))
    a = varargin{k++};
    if (ischar (a) && strcmpi (a, "filled"))
      filled = true;
    elseif (ischar (a) && isempty (lc) && __is_linespec__ (a))
      [ls1, c1, m1] = __parse_linespec__ (a);
      if (! isempty (ls1) && ! strcmp (ls1, "none")) ls = ls1; endif
      lc = c1; if (! isempty (m1)) mk = m1; endif
      if (isempty (lc)) lc = "__auto__"; endif
    else
      props{end+1} = a;
      if (k <= numel (varargin)) props{end+1} = varargin{k++}; endif
    endif
  endwhile
  uu = u; vv = v; ww = w;
  if (is3d)
    len = max (sqrt (u(:).^2 + v(:).^2 + w(:).^2));
  else
    len = max (sqrt (u(:).^2 + v(:).^2));
  endif
  if (len > 0 && autoscale && numel (u) >= 1)
    if (isvector (x)) nx = ny = sqrt (numel (x)); else [ny, nx] = size (x); endif
    dx = (max (x(:)) - min (x(:))) / nx;
    dy = (max (y(:)) - min (y(:))) / ny;
    if (is3d) dz = (max (z(:)) - min (z(:))) / max (nx, ny); else dz = 0; endif
    sd = sqrt (dx^2 + dy^2 + dz^2) / len;
    if (sd != 0) s = autoscale * sd; else s = autoscale; endif
    uu = s * u; vv = s * v; ww = s * w;
  endif
  if (isempty (hax)) hax = gca (); endif
  if (isempty (lc) || strcmp (lc, "__auto__"))
    co = get (hax, "colororder");
    lc = co(1,:);
  endif
  hg = __go_hggroup__ (hax);
  x = x(:); y = y(:); n = numel (x);
  xe = x + uu(:); ye = y + vv(:);
  as = 1 / 3;
  xt = x + uu(:) * (1 - as); yt = y + vv(:) * (1 - as);
  if (is3d)
    z = z(:); ze = z + ww(:);
    xyd = sqrt (uu(:).^2 + vv(:).^2 + ww(:).^2) ./ (sqrt (uu(:).^2 + vv(:).^2) + eps);
    xa1 = xt + vv(:) .* xyd * as / 4; xa2 = xt - vv(:) .* xyd * as / 4;
    ya1 = yt - uu(:) .* xyd * as / 4; ya2 = yt + uu(:) .* xyd * as / 4;
    za1 = za2 = ze - ww(:) * as;
  else
    z = ze = za1 = za2 = zeros (n, 1);
    xa1 = xt + vv(:) * as / 3; xa2 = xt - vv(:) * as / 3;
    ya1 = yt - uu(:) * as / 3; ya2 = yt + uu(:) * as / 3;
  endif
  NN = NaN (1, n);
  hl = "linestyle";
  h1 = line ([x.'; xe.'; NN](:), [y.'; ye.'; NN](:), [z.'; ze.'; NN](:), hl, ls, "color", lc, "parent", hg);
  h2 = line ([xa1.'; xe.'; xa2.'; NN](:), [ya1.'; ye.'; ya2.'; NN](:), [za1.'; ze.'; za2.'; NN](:), hl, ls, "color", lc, "parent", hg);
  h3 = line (x, y, z, "linestyle", "none", "color", lc, "marker", mk, "parent", hg);
  if (! is3d)
    set ([h1 h2 h3], "zdata", []);
  endif
  if (filled) set (h3, "markerfacecolor", lc); endif
  if (! isempty (props)) set ([h1 h2], props{:}); endif
  addproperty ("xdata", hg, "data", x.'); addproperty ("ydata", hg, "data", y.');
  addproperty ("udata", hg, "data", uu(:).'); addproperty ("vdata", hg, "data", vv(:).');
  if (is3d)
    addproperty ("zdata", hg, "data", z.'); addproperty ("wdata", hg, "data", ww(:).');
  else
    addproperty ("zdata", hg, "data", []); addproperty ("wdata", hg, "data", []);
  endif
  addproperty ("showarrowhead", hg, "radio", "{on}|off", "on");
  addproperty ("color", hg, "color", get (h1, "color"));
  addproperty ("linestyle", hg, "string", get (h1, "linestyle"));
  addproperty ("linewidth", hg, "data", get (h1, "linewidth"));
  addproperty ("marker", hg, "string", get (h3, "marker"));
  addproperty ("markerfacecolor", hg, "string", get (h3, "markerfacecolor"));
  addproperty ("markersize", hg, "data", get (h3, "markersize"));
  if (is3d && ! ishold (hax)) view (hax, 3); endif
endfunction
