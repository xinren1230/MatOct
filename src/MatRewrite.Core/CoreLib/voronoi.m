## MatRewrite - GPL-3.0-or-later
## voronoi (x, y), [vx, vy] = voronoi (x, y), voronoi (hax, ...): 2-D Voronoi diagram.
function [vx, vy] = voronoi (varargin)
  if (nargin < 1)
    print_usage ();
  endif
  narg = 1;
  hax = NaN;
  if (isscalar (varargin{1}) && ishghandle (varargin{1}) && nargin > 1 && ! isscalar (varargin{2}))
    hax = varargin{1};
    if (! isaxes (hax))
      error ("voronoi: HAX argument must be an axes object");
    endif
    narg = 2;
  elseif (nargin == 3 && isscalar (varargin{1}) && isnumeric (varargin{2}) && numel (varargin{2}) > 1)
    error ("voronoi: HAX argument must be an axes object");
  endif
  if (nargin < narg + 1)
    print_usage ();
  endif
  x = varargin{narg++};
  y = varargin{narg++};
  if (narg <= nargin && isnumeric (varargin{narg}))
    narg++;    # triangulation is accepted but ignored
  endif
  linespec = {"b"};
  if (narg <= nargin && ischar (varargin{narg}))
    linespec = varargin(narg++);
  endif
  if (narg <= nargin)
    print_usage ();
  endif
  if (! isvector (x) || ! isvector (y) || numel (x) != numel (y))
    error ("voronoi: X and Y must be vectors of the same length");
  elseif (numel (x) < 2)
    error ("voronoi: minimum of 2 points required");
  endif
  x = x(:);  y = y(:);
  xmin = min (x);  xmax = max (x);  ymin = min (y);  ymax = max (y);
  ## surround the points with a box to bound the outer cells
  xd = xmax - xmin;  yd = ymax - ymin;  s = 2;
  xbox = [xmin - s*xd; xmin - s*xd; xmax + s*xd; xmax + s*xd];
  ybox = [ymin - s*yd; ymax + s*yd; ymax + s*yd; ymin - s*yd];
  [p, c, infi] = __voronoi__ ("voronoi", [[x; xbox], [y; ybox]]);
  c = c(! infi).';
  edges = zeros (2, 0);
  for i = 1:numel (c)
    f = c{i};
    if (! isempty (f))
      edges = [edges, [f; [f(end), f(1:end-1)]]];
    endif
  endfor
  edges = sortrows (sort (edges).').';
  edges = edges(:, [any(diff(edges, 1, 2)), true]);
  if (numel (x) > 2)
    ctr = [(xmax + xmin)/2, (ymax + ymin)/2];
    radius = 1.1 * sumsq ([xmin, ymin] - ctr);
    dist = sumsq (p - ctr, 2);
    inside = find (dist < radius);
    edges = edges(:, any (ismember (edges, inside), 1));
  else
    for k = 1:columns (edges)
      e = edges(:,k);
      if (det ([[1;1], p(e,1:2); 1, x(1), y(1)]) * det ([[1;1], p(e,1:2); 1, x(2), y(2)]) < 0)
        edges = e;
        break;
      endif
    endfor
    xd = yd = max (xd, yd);
  endif
  Vx = reshape (p(edges, 1), size (edges));
  Vy = reshape (p(edges, 2), size (edges));
  if (nargout < 2)
    if (isnan (hax))
      hax = gca ();
    endif
    h = plot (hax, Vx, Vy, linespec{:}, x, y, "+");
    axis (hax, [xmin, xmax, ymin, ymax] + 0.1 * [-xd, xd, -yd, yd]);
    if (nargout == 1)
      vx = h;
    endif
  else
    vx = Vx;  vy = Vy;
  endif
endfunction
