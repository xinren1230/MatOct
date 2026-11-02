function h = area (varargin)
  % AREA  Filled area plot (stacked for matrix input).
  if nargin < 1, print_usage (); end
  if nargin > 3, print_usage (); end
  if nargin >= 2 && isnumeric (varargin{2}), x = varargin{1}; y = varargin{2}; else y = varargin{1}; x = []; end
  if isvector (y), y = y(:); end
  if isempty (x), x = (1:rows (y))'; end
  x = x(:);
  co = [0 0.447 0.741;0.85 0.325 0.098;0.929 0.694 0.125;0.494 0.184 0.556;0.466 0.674 0.188;0.301 0.745 0.933];
  wasHeld = ishold (); hold on;
  base = zeros (size (x)); hs = [];
  for j = 1:columns (y)
    top = base + y(:, j);
    hs(end+1) = patch ([x; flipud(x)], [top; flipud(base)], co(mod (j-1, 6) + 1, :));
    base = top;
  end
  if ~wasHeld, hold off; end
  if nargout > 0, h = hs; end
end
