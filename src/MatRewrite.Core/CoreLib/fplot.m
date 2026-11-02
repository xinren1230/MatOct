function h = fplot (f, lim, varargin)
  % FPLOT  Plot a function over an interval.
  if nargin < 1, print_usage (); end
  if nargin < 2 || isempty (lim), lim = [-5 5]; end
  if ischar (f), f = str2func (['@(x) ' f]); end
  x = linspace (lim(1), lim(2), 200);
  y = arrayfun (f, x);
  hh = plot (x, y, varargin{:});
  if nargout > 0, h = hh; end
end
