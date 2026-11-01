## MatRewrite CoreLib: shared driver for stream2/stream3.
function out = __stream_common__ (fn, d, varargin)
  nin = numel (varargin);
  opts = [];
  if (d == 2)
    switch (nin)
      case {4, 5}
        [u, v, sx, sy] = varargin{1:4}; if (nin == 5) opts = varargin{5}; endif
        [x, y] = meshgrid (1:columns (u), 1:rows (u));
      case {6, 7}
        [x, y, u, v, sx, sy] = varargin{1:6}; if (nin == 7) opts = varargin{7}; endif
      otherwise
        print_usage (fn);
    endswitch
    F = {u, v}; G = {x, y}; S = {sx, sy};
  else
    switch (nin)
      case {6, 7}
        [u, v, w, sx, sy, sz] = varargin{1:6}; if (nin == 7) opts = varargin{7}; endif
        [x, y, z] = meshgrid (1:columns (u), 1:rows (u), 1:size (u, 3));
      case {9, 10}
        [x, y, z, u, v, w, sx, sy, sz] = varargin{1:9}; if (nin == 10) opts = varargin{10}; endif
      otherwise
        print_usage (fn);
    endswitch
    F = {u, v, w}; G = {x, y, z}; S = {sx, sy, sz};
  endif
  h = 0.1; maxv = 10000;
  if (! isempty (opts))
    if (numel (opts) > 2) error ("%s: OPTIONS must be a 1- or 2-element vector", fn); endif
    h = opts(1);
    if (imag (h) != 0 || h == 0) error ("%s: STEPSIZE must be a real scalar != 0", fn); endif
    if (numel (opts) == 2)
      maxv = opts(2);
      if (imag (maxv) != 0 || maxv < 1) error ("%s: MAX_VERTICES must be an integer > 0", fn); endif
      maxv = fix (maxv);
    endif
  endif
  if (! (size_equal (F{:}, G{:}) && size_equal (S{:})))
    error ("%s: matrix dimensions must match", fn);
  endif
  if (any (cellfun (@iscomplex, [F, G, S])))
    error ("%s: all inputs must be real-valued", fn);
  endif
  gx = G{1}(1,:,1); gy = G{2}(:,1,1).';
  g = {gx, gy};
  if (d == 3) g{3} = squeeze (G{3}(1,1,:)).'; endif
  T = cell (1, d); dd = cell (1, d);
  for q = 1:d
    df = diff (g{q});
    if (any (df <= 0) || any (isnan (df)))
      error ("%s: non-monotonically increasing or NaN values found in mesh", fn);
    endif
    T{q} = [1 ./ df, 0]; dd{q} = [df, 0];
  endfor
  out = cell (1, numel (S{1}));
  for k = 1:numel (S{1})
    p = zeros (1, d); ok = true;
    for q = 1:d
      s = S{q}(k); gq = g{q};
      i = find (gq <= s, 1, "last");
      if (isempty (i) || s > gq(end)) ok = false; break; endif
      i = min (i, max (numel (gq) - 1, 1));
      p(q) = (i - 1) + (s - gq(i)) * T{q}(i);
    endfor
    if (! ok) out{k} = []; continue; endif
    C = __streameuler__ (d, F{:}, T{:}, p, h, maxv);
    R = zeros (rows (C), d);
    for q = 1:d
      id = min (floor (C(:,q)), numel (g{q}) - 1);
      R(:,q) = g{q}(id + 1).' + (C(:,q) - id) .* dd{q}(id + 1).';
    endfor
    out{k} = R;
  endfor
endfunction
