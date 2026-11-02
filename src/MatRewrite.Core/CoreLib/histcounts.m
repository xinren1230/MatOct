function [n, edges, bin] = histcounts (x, varargin)
  % HISTCOUNTS  Histogram bin counts.
  x = x(:); xf = x(isfinite (x));
  edges = []; nb = []; bw = []; norm = 'count';
  k = 1;
  if numel (varargin) >= 1 && isnumeric (varargin{1})
    if isscalar (varargin{1}), nb = varargin{1}; else edges = varargin{1}(:)'; end
    k = 2;
  end
  while k < numel (varargin)
    switch lower (varargin{k})
      case 'binwidth', bw = varargin{k+1};
      case 'numbins', nb = varargin{k+1};
      case 'binedges', edges = varargin{k+1}(:)';
      case 'normalization', norm = lower (varargin{k+1});
      case 'binlimits', lim = varargin{k+1}; xf = xf(xf >= lim(1) & xf <= lim(2));
    end
    k = k + 2;
  end
  if isempty (edges)
    if isempty (xf), lo = 0; hi = 1; else lo = min (xf); hi = max (xf); end
    if isempty (bw)
      if ~isempty (nb)
        if hi == lo, hi = lo + 1; end
        edges = linspace (lo, hi, nb + 1);
      else
        s = std (xf); m = numel (xf);
        if m < 2 || s == 0, bw = 1; else bw = 3.5 * s / m^(1/3); end
        p = 10^floor (log10 (bw)); f = bw / p;
        if f <= 1, bw = p; elseif f <= 2, bw = 2*p; elseif f <= 5, bw = 5*p; else bw = 10*p; end
      end
    end
    if isempty (edges)
      a = floor (lo / bw) * bw; b = ceil (hi / bw) * bw;
      if b <= a, b = a + bw; end
      edges = a:bw:(b + bw/2);
      if edges(end) < hi, edges(end+1) = edges(end) + bw; end
    end
  end
  nE = numel (edges);
  bin = zeros (size (x));
  for i = 1:numel (x)
    v = x(i);
    if v < edges(1) || v > edges(end) || isnan (v), continue; end
    j = find (v >= edges(1:end-1), 1, 'last');
    if v == edges(end), j = nE - 1; end
    bin(i) = j;
  end
  n = zeros (1, nE - 1);
  for j = 1:nE-1, n(j) = sum (bin == j); end
  w = diff (edges); tot = sum (n);
  switch norm
    case 'probability', n = n / tot;
    case 'pdf', n = n ./ (tot * w);
    case 'countdensity', n = n ./ w;
    case 'cumcount', n = cumsum (n);
    case 'cdf', n = cumsum (n) / tot;
  end
end
