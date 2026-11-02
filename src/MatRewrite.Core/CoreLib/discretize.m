function [Y, E] = discretize (X, edges, varargin)
  % DISCRETIZE  Bin index of each element of X for the given edges (or number of bins).
  if isscalar (edges)
    lo = min (X(:)); hi = max (X(:)); edges = linspace (lo, hi, edges + 1);
  end
  E = edges; Y = nan (size (X)); ne = numel (edges);
  for k = 1:ne-1
    if k < ne - 1, in = X >= edges(k) & X < edges(k+1); else, in = X >= edges(k) & X <= edges(k+1); end
    Y(in) = k;
  end
  if ~isempty (varargin) && ~ischar (varargin{1})
    v = varargin{1}; Z = nan (size (X)); ok = ~isnan (Y); Z(ok) = v(Y(ok)); Y = Z;
  end
end
