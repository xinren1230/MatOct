## MatRewrite - GPL-3.0-or-later
function [X, Y] = fplot (varargin)
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("fplot", varargin{:});
  if (nargs < 1 || nargs > 5)
    print_usage ();
  endif
  fcn = varargin{1};
  if (isa (fcn, "inline"))
    fcn = vectorize (fcn); nam = formula (fcn); fcn = str2func (["@(x) " nam]);
  elseif (is_function_handle (fcn))
    nam = func2str (fcn);
    nam = regexprep (nam, '^@\([^)]*\)\s*', '');
  elseif (ischar (fcn) && ! isempty (fcn))
    nam = fcn;
    toks = regexp (fcn, '[a-zA-Z_]\w*(\s*\()?', "match");
    toks = toks(cellfun (@(t) t(end) != "(", toks));
    vars = setdiff (unique (toks), {"pi", "e", "i", "j", "Inf", "NaN", "eps"});
    if (isempty (vars)), vars = {"x"}; endif
    fcn = str2func (["@(" vars{1} ") " fcn]);
  else
    error ("fplot: FCN must be a function handle, inline function, or string");
  endif
  limits = [-5, 5]; tol = 2e-3; n = 5; fmt = {};
  have_limits = false;
  for k = 2:numel (varargin)
    a = varargin{k};
    if (isnumeric (a) && ! have_limits && (k == 2 || ! isscalar (a)))
      if (iscomplex (a) || ! (numel (a) == 2 || numel (a) == 4))
        if (iscomplex (a))
          error ("fplot: LIMITS must be a real vector with 2 or 4 elements");
        endif
        error ("fplot: LIMITS must be a real vector with 2 or 4 elements");
      endif
      limits = a; have_limits = true;
    elseif (isnumeric (a) && isscalar (a))
      if (a < 1), tol = a; else, n = a; endif
    elseif (ischar (a))
      fmt = varargin(k:end);
      break;
    else
      error ("fplot: bad input in position %d", k);
    endif
  endfor
  if (numel (fmt) == 1 || (numel (fmt) > 1 && mod (numel (fmt), 2) == 1 && __is_linespec_like__ (fmt{1})))
    ## linespec possibly followed by prop/value pairs
  elseif (numel (fmt) > 0 && mod (numel (fmt), 2) == 1)
    error ("fplot: bad input in position %d", 2);
  endif
  if (numel (fmt) == 1 && ! __is_linespec_like__ (fmt{1}))
    error ("fplot: bad input in position %d", nargs);
  endif

  x0 = linspace (limits(1), limits(2), max (n, 5)).';
  y0 = __fplot_eval__ (fcn, x0);
  ## refine until the interpolated curve agrees with the sampled one
  x = x0; y = y0;
  for iter = 1:8
    xm = (x(1:end-1) + x(2:end)) / 2;
    ym = __fplot_eval__ (fcn, xm);
    yi = (y(1:end-1,:) + y(2:end,:)) / 2;
    xx = zeros (2*numel (x) - 1, 1); xx(1:2:end) = x; xx(2:2:end) = xm;
    yy = zeros (rows (xx), columns (y)); yy(1:2:end,:) = y; yy(2:2:end,:) = ym;
    x = xx; y = yy;
    sc = max (abs (y(isfinite (y))(:))); if (isempty (sc) || sc == 0), sc = 1; endif
    d = abs (ym - yi); d(! isfinite (d)) = 0;
    if (max (d(:)) <= tol * sc && numel (x) > 100)
      break;
    endif
  endfor

  if (nargout == 0)
    if (isempty (hax)), hax = gca (); endif
    hasdn = any (strcmpi (fmt, "displayname")) || (numel (fmt) >= 1 && any (fmt{1} == ";"));
    if (numel (fmt) >= 1 && any (fmt{1} == ";"))
      lab = regexp (fmt{1}, ';([^;]*);', "tokens", "once");
      fmt{1} = regexprep (fmt{1}, ';[^;]*;', '');
      if (isempty (fmt{1})), fmt(1) = []; endif
      if (! isempty (lab)), fmt(end+1:end+2) = {"displayname", lab{1}}; endif
    endif
    h = plot (hax, x, y, fmt{:});
    if (numel (limits) == 4)
      set (hax, "xlim", limits(1:2), "ylim", limits(3:4));
    else
      set (hax, "xlim", limits(1:2));
    endif
    if (! hasdn)
      if (columns (y) == 1)
        set (h, "displayname", nam);
      else
        for c = 1:columns (y)
          set (h(c), "displayname", sprintf ("%s(:,%d)", nam, c));
        endfor
      endif
    endif
  else
    X = x; Y = y;
  endif
endfunction

function y = __fplot_eval__ (fcn, x)
  y = fcn (x);
  if (numel (y) == 1 && numel (x) > 1)
    warning ("fplot: FCN is not a vectorized function which reduces performance");
    y = arrayfun (@(t) fcn (t), x);
  elseif (isvector (y) && numel (y) == numel (x))
    y = y(:);
  elseif (rows (y) != numel (x))
    if (columns (y) == numel (x))
      y = y.';
    else
      error ("fplot: invalid function FCN (# of outputs not equal to inputs)");
    endif
  endif
endfunction

function t = __is_linespec_like__ (s)
  t = ischar (s) && (any (s == ";") || (numel (s) <= 4 && all (ismember (s, "-:.+o*xsd^v<>phrgbcmykw"))));
endfunction
