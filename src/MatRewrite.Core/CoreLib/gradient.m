## MatRewrite CoreLib: gradient -- one-sided differences at the edges,
## central differences inside, uniform or coordinate-vector spacing.
function varargout = gradient (m, varargin)
  if (nargin < 1) print_usage (); endif
  if (is_function_handle (m))
    if (nargin < 2) print_usage (); endif
    p0 = varargin{1}; psz = size (p0);
    if (isvector (p0)) p0 = p0(:); endif
    dim = columns (p0); np = rows (p0);
    if (nargin > 2) delta = varargin{2}; else delta = 1e-5; endif
    if (isscalar (delta)) delta = repmat (delta, 1, dim); endif
    P = mat2cell (p0, np, ones (1, dim));
    for d = 1:dim
      s = delta(d); Pp = P; Pm = P; Pp{d} += s; Pm{d} -= s;
      g = (m (Pp{:}) - m (Pm{:})) ./ (2*s);
      if (dim == 1) g = reshape (g, psz); endif
      varargout{d} = g;
    endfor
    return;
  endif
  if (! isnumeric (m) && ! islogical (m))
    error ("gradient: first input must be an array or a function");
  endif
  if (! isfloat (m)) error ("gradient: M must be a floating point vector or array"); endif
  sz = size (m); nd = ndims (m);
  if (isvector (m))
    nd = 1; vdim = find (sz > 1, 1); if (isempty (vdim)) vdim = 2; endif
  endif
  nout = max (nargout, 1);
  sp = varargin; ns = numel (sp);
  if (ns == 0) sp = {1}; ns = 1; endif
  if (ns == 1 && nd > 1)
    if (! isscalar (sp{1}))
      error ("gradient: spacing values must be scalars or a vector");
    endif
    sp = repmat (sp, 1, nd);
  elseif (ns != nd && ! (nd == 1 && ns == 1))
    error ("gradient: dimensions and number of spacing values do not match");
  endif
  if (nd == 1)
    dims = vdim;
  else
    dims = [2, 1, 3:nd];
  endif
  for k = 1:min (nout, numel (dims))
    d = dims(k);
    if (nd == 1) h = sp{1}; else
      if (d == 1) h = sp{2}; elseif (d == 2) h = sp{1}; else h = sp{d}; endif
    endif
    n = sz(d);
    if (isscalar (h)) x = (0:n-1).' * h;
    else
      x = h(:);
      if (numel (x) != n) error ("gradient: dimensions and number of spacing values do not match"); endif
    endif
    p = [d, 1:d-1, d+1:max(numel(sz),d)];
    a = permute (m, p); as = size (a); a = reshape (a, n, []);
    g = zeros (size (a), class (a));
    if (n > 1)
      g(1,:) = (a(2,:) - a(1,:)) / (x(2) - x(1));
      g(n,:) = (a(n,:) - a(n-1,:)) / (x(n) - x(n-1));
      if (n > 2)
        g(2:n-1,:) = (a(3:n,:) - a(1:n-2,:)) ./ (x(3:n) - x(1:n-2));
      endif
    endif
    varargout{k} = ipermute (reshape (g, as), p);
  endfor
endfunction
