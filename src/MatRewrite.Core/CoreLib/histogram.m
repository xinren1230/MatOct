function h = histogram (varargin)
  % HISTOGRAM  Histogram plot (drawn with bar).
  if nargin < 1, print_usage (); end
  x = varargin{1}; rest = varargin(2:end);
  opts = {}; pass = {}; k = 1; fc = [];
  if ~isempty (rest) && isnumeric (rest{1}), pass{end+1} = rest{1}; k = 2; end
  while k < numel (rest)
    key = lower (rest{k});
    if any (strcmp (key, {'binwidth','numbins','binedges','normalization','binlimits'}))
      pass(end+1:end+2) = rest(k:k+1);
    elseif strcmp (key, 'facecolor'), fc = rest{k+1};
    end
    k = k + 2;
  end
  [n, edges] = histcounts (x, pass{:});
  ctr = (edges(1:end-1) + edges(2:end)) / 2;
  if isempty (fc), b = bar (ctr, n, 1); else b = bar (ctr, n, 1, 'FaceColor', fc); end
  if nargout > 0
    h = struct ('Values', n, 'BinEdges', edges, 'NumBins', numel (n), 'BinWidth', edges(2) - edges(1), 'Data', x, 'Bar', b);
  end
end
