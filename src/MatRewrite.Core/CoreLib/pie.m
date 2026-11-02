function h = pie (x, varargin)
  % PIE  Pie chart.
  if nargin < 1, print_usage (); end
  if ! all (isfinite (x(:))), error ('pie: all data in X must be finite'); end
  x = x(:)'; labels = {};
  if ~isempty (varargin) && iscell (varargin{end}), labels = varargin{end}; end
  tot = sum (x); if tot <= 1, frac = x; else frac = x / tot; end
  cla; hold on;
  co = get (gca, 'ColorOrder');
  if isempty (co), co = [0 0.447 0.741;0.85 0.325 0.098;0.929 0.694 0.125;0.494 0.184 0.556;0.466 0.674 0.188;0.301 0.745 0.933]; end
  a0 = pi/2; hs = [];
  for i = 1:numel (frac)
    a1 = a0 - 2*pi*frac(i);
    t = linspace (a0, a1, max (8, ceil (64*frac(i))));
    hs(end+1) = patch ([0 cos(t) 0], [0 sin(t) 0], co(mod (i-1, rows (co)) + 1, :));
    m = (a0 + a1) / 2;
    if isempty (labels), lb = sprintf ('%g%%', round (frac(i)*100)); else lb = labels{i}; end
    text (1.15*cos (m), 1.15*sin (m), lb, 'HorizontalAlignment', 'center');
    a0 = a1;
  end
  hold off; axis equal; axis ([-1.4 1.4 -1.4 1.4]); axis off;
  if nargout > 0, h = hs; end
end
