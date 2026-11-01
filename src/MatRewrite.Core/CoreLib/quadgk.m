## MatRewrite - GPL-3.0-or-later
## Adaptive Gauss-Kronrod (7,15) quadrature with endpoint/infinite transforms.
function [q, err] = quadgk (f, a, b, varargin)
  if (nargin < 3)
    print_usage ();
  endif
  if (ischar (f))
    f = str2func (f);
  elseif (isa (f, "inline"))
    f = str2func (formula (f));
  endif
  abstol = []; reltol = []; waypoints = []; maxint = 650; trace = false; arrayvalued = false;
  issingle = isa (a, "single") || isa (b, "single");
  if (numel (varargin) > 0 && ! ischar (varargin{1}))
    if (numel (varargin) > 2)
      error ("quadgk: can not pass additional arguments to user function");
    endif
    abstol = varargin{1};
    if (numel (varargin) > 1 && ! isempty (varargin{2}))
      trace = varargin{2};
    endif
  else
    if (mod (numel (varargin), 2))
      error ("quadgk: property/value options must occur in pairs");
    endif
    for k = 1:2:numel (varargin)
      p = varargin{k}; v = varargin{k+1};
      if (! ischar (p))
        error ("quadgk: property PROP must be a string");
      endif
      switch (tolower (p))
        case "waypoints", waypoints = v(:).'; issingle = issingle || isa (v, "single");
        case "maxintervalcount", maxint = v;
        case "abstol", abstol = v;
        case "reltol", reltol = v;
        case "trace", trace = v;
        case "arrayvalued", arrayvalued = logical (v);
        otherwise
          error ("quadgk: unknown property '%s'", p);
      endswitch
    endfor
  endif
  if (isempty (abstol))
    abstol = ifelse_ (issingle, 1e-5, 1e-10);
  elseif (! isscalar (abstol) || abstol < 0)
    error ("quadgk: ABSTOL must be a scalar >=0");
  endif
  if (isempty (reltol))
    reltol = ifelse_ (issingle, 1e-4, 1e-6);
  elseif (! isscalar (reltol) || reltol < 0)
    error ("quadgk: RELTOL must be a scalar >=0");
  endif

  if (a == b && isempty (waypoints))
    q = zeros (size (f (a)), class (a + b)); q(:) = 0; if (! arrayvalued), q = q(1)*0; endif
    err = 0; if (issingle), q = single (q); endif
    return;
  endif

  ## Build path; finite real path with infinite endpoints handled via transforms
  pts = [a, waypoints, b];
  q = 0; err = 0;
  for s = 1:numel (pts) - 1
    [qs, es] = __qgk_seg__ (f, pts(s), pts(s+1), abstol, reltol, maxint, arrayvalued);
    q = q + qs; err = err + es;
  endfor
  if (err > max (abstol, reltol * max (abs (q(:)))))
    warning ("quadgk: Error tolerance not met.  Estimated error: %g", err);
  endif
  if (issingle)
    q = single (q); err = single (err);
  endif
endfunction

function r = ifelse_ (c, x, y)
  if (c), r = x; else, r = y; endif
endfunction

function [q, err] = __qgk_seg__ (f, a, b, abstol, reltol, maxint, av)
  if (a == b)
    q = 0; err = 0; return;
  endif
  sgn = 1;
  if (isreal (a) && isreal (b) && b < a)
    [a, b] = deal (b, a); sgn = -1;
  endif
  if (isinf (a) && isinf (b))
    g = @(t) f (t ./ (1 - t.^2)) .* (1 + t.^2) ./ (1 - t.^2).^2;
    lo = -1; hi = 1; tr = 1;
  elseif (isinf (b))
    g = @(t) 2 * t .* f (a + (t ./ (1 - t)).^2) ./ (1 - t).^3;  lo = 0; hi = 1; tr = 2;
  elseif (isinf (a))
    g = @(t) -2 * t .* f (b - (t ./ (1 + t)).^2) ./ (1 + t).^3;  lo = -1; hi = 0; tr = 3;
  elseif (isreal (a) && isreal (b))
    ## t in [-1,1] -> x = (b-a)/4 * t (3 - t^2) + (b+a)/2 weakens endpoint singularities
    g = @(t) f ((b-a)/4 * t .* (3 - t.^2) + (b+a)/2) .* (3*(b-a)/4 * (1 - t.^2));
    lo = -1; hi = 1; tr = 0;
  else
    ## complex straight segment
    g = @(t) f (a + (b-a) * t) * (b - a);
    lo = 0; hi = 1; tr = 4;
  endif
  ## initial subdivision into 10 intervals
  ed = linspace (lo, hi, 11);
  ivals = [ed(1:end-1).', ed(2:end).'];
  q = 0; err = 0;
  [qi, ei] = __gk15__ (g, ivals, av);
  for it = 1:200
    qt = sum_ (qi, av); et = sum (ei);
    tol = max (abstol, reltol * max (abs (qt(:))));
    if (et <= tol || rows (ivals) >= maxint)
      break;
    endif
    ## split intervals whose error is large relative to their width
    w = ivals(:,2) - ivals(:,1);
    bad = ei > tol * w / (hi - lo);
    if (! any (bad))
      [~, k] = max (ei); bad(k) = true;
    endif
    mid = sum (ivals(bad,:), 2) / 2;
    if (any (abs (ivals(bad,2) - ivals(bad,1)) < 100*eps*max(1,abs(mid))))
      break;
    endif
    newiv = [ivals(bad,1), mid; mid, ivals(bad,2)];
    [qn, en] = __gk15__ (g, newiv, av);
    ivals = [ivals(! bad,:); newiv];
    if (av)
      qi = [qi(! bad); qn];
    else
      qi = [qi(! bad); qn];
    endif
    ei = [ei(! bad); en];
  endfor
  q = sgn * sum_ (qi, av); err = sum (ei);
endfunction

function s = sum_ (qi, av)
  if (av)
    s = 0; for k = 1:numel (qi), s = s + qi{k}; end
  else
    s = sum (qi);
  endif
endfunction

function [qi, ei] = __gk15__ (g, iv, av)
  persistent xk wk wg
  if (isempty (xk))
    xk = [-0.991455371120812639206854697526329, -0.949107912342758524526189684047851, ...
          -0.864864423359769072789712788640926, -0.741531185599394439863864773280788, ...
          -0.586087235467691130294144845693013, -0.405845151377397166906606412076961, ...
          -0.207784955007898467600689403773245, 0, ...
           0.207784955007898467600689403773245,  0.405845151377397166906606412076961, ...
           0.586087235467691130294144845693013,  0.741531185599394439863864773280788, ...
           0.864864423359769072789712788640926,  0.949107912342758524526189684047851, ...
           0.991455371120812639206854697526329];
    wk = [0.022935322010529224963732008058970, 0.063092092629978553290700663189204, ...
          0.104790010322250183839876322541518, 0.140653259715525918745189590510238, ...
          0.169004726639267902826583426598550, 0.190350578064785409913256402421014, ...
          0.204432940075298892414161999234649, 0.209482141084727828012999174891714, ...
          0.204432940075298892414161999234649, 0.190350578064785409913256402421014, ...
          0.169004726639267902826583426598550, 0.140653259715525918745189590510238, ...
          0.104790010322250183839876322541518, 0.063092092629978553290700663189204, ...
          0.022935322010529224963732008058970];
    wg = [0, 0.129484966168869693270611432679082, 0, 0.279705391489276667901467771423780, ...
          0, 0.381830050505118944950369775488975, 0, 0.417959183673469387755102040816327, ...
          0, 0.381830050505118944950369775488975, 0, 0.279705391489276667901467771423780, ...
          0, 0.129484966168869693270611432679082, 0];
  endif
  n = rows (iv);
  hw = (iv(:,2) - iv(:,1)) / 2; c = (iv(:,2) + iv(:,1)) / 2;
  T = c + hw .* xk;               # n x 15
  if (av)
    qi = cell (n, 1); ei = zeros (n, 1);
    for i = 1:n
      sk = 0; sg = 0;
      for j = 1:15
        y = g (T(i,j));
        y(! isfinite (y)) = 0;
        sk = sk + wk(j) * y; sg = sg + wg(j) * y;
      endfor
      qi{i} = sk * hw(i); ei(i) = max (abs ((sk - sg)(:))) * abs (hw(i));
    endfor
  else
    tt = T(:).';
    y = g (tt);
    if (numel (y) != numel (tt))
      error ("quadgk: integrand F must return a single, real-valued vector of the same size as the input");
    endif
    y = reshape (y, n, 15);
    y(isinf (y) | isnan (y)) = 0;
    qk = (y * wk.') .* hw; qg = (y * wg.') .* hw;
    qi = qk; ei = abs (qk - qg);
  endif
endfunction
