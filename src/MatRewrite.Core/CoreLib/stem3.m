## MatRewrite - GPL-3.0-or-later
function h = stem3 (varargin)
  if (nargin < 1)
    print_usage ();
  endif
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("stem3", varargin{:});
  k = 0;
  while (k < numel (varargin) && ! ischar (varargin{k+1}))
    k++;
  endwhile
  if (k < 3)
    if (k >= 1 && k < numel (varargin))
      p = varargin{k+1};
      if (! __is_ls3__ (p) && k + 1 == numel (varargin))
        error ('stem3: No value specified for property "%s"', p);
      endif
    endif
    error ("stem3: must define X, Y, and Z");
  endif
  x = varargin{1}; y = varargin{2}; z = varargin{3};
  if (! (isnumeric (x) && isnumeric (y) && isnumeric (z)))
    error ("stem3: X, Y, and Z must be numeric");
  endif
  if (! (isequal (size (x), size (y)) && isequal (size (y), size (z))))
    error ("stem3: inconsistent sizes for X, Y, and Z");
  endif
  opts = varargin(4:end);
  ls = "b"; mk = "o";
  if (! isempty (opts) && __is_ls3__ (opts{1}))
    [~, c, m] = __parse_linespec__ (opts{1});
    if (! isempty (c)), ls = c; endif
    if (! isempty (m)), mk = m; endif
    opts(1) = [];
  elseif (mod (numel (opts), 2) == 1)
    error ('stem3: No value specified for property "%s"', opts{end});
  endif
  if (isempty (hax)), hax = gca (); endif
  hold_state = ishold (hax);
  n = numel (x); x = x(:).'; y = y(:).'; z = z(:).';
  xs = [x; x; NaN(1, n)](:); ys = [y; y; NaN(1, n)](:); zs = [zeros(1, n); z; NaN(1, n)](:);
  hl = plot3 (hax, xs, ys, zs, "-", "color", ls, opts{:});
  hold (hax, "on");
  hm = plot3 (hax, x, y, z, "linestyle", "none", "marker", mk, "color", ls, opts{:});
  if (! hold_state), hold (hax, "off"); view (hax, 3); endif
  if (nargout > 0), h = [hl; hm]; endif
endfunction

function t = __is_ls3__ (s)
  t = ischar (s) && numel (s) <= 4 && all (ismember (s, "-:.+o*xsd^v<>phrgbcmykw"));
endfunction
