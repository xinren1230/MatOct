## MatRewrite - GPL-3.0-or-later
function [q, err] = integral (f, a, b, varargin)
  if (nargin < 3 || (mod (nargin, 2) == 0))
    print_usage ();
  endif
  if (ischar (f))
    f = str2func (f);
  elseif (isa (f, "inline"))
    f = str2func (formula (f));
  endif
  abstol = []; reltol = []; wp = []; av = false;
  issingle = isa (a, "single") || isa (b, "single");
  for k = 1:2:numel (varargin)
    p = varargin{k};
    if (! ischar (p))
      error ("integral: property PROP must be a string");
    endif
    v = varargin{k+1};
    switch (tolower (p))
      case "abstol", abstol = v;
      case "reltol", reltol = v;
      case "waypoints", wp = v; issingle = issingle || isa (v, "single");
      case "arrayvalued", av = logical (v);
      otherwise, error ("integral: unknown property '%s'", p);
    endswitch
  endfor
  if (isempty (abstol)), abstol = ifelse__ (issingle, 1e-5, 1e-10); endif
  if (isempty (reltol)), reltol = ifelse__ (issingle, 1e-4, 1e-6); endif
  if (! isscalar (abstol) || abstol < 0), error ("integral: AbsTol must be a scalar >= 0"); endif
  if (! isscalar (reltol) || reltol < 0), error ("integral: RelTol must be a scalar >= 0"); endif
    usecc = isempty (wp) && ! av && isreal (a) && isreal (b) && reltol >= 1e-10;
  if (usecc)
    m = (double (a) + double (b)) / 2; if (! isfinite (m)), m = 0.5; endif
    usecc = isreal (f (m)) && isreal (f (m + 0.123));
  endif
  if (usecc)
    [q, err] = quadcc (f, double (a), double (b), [abstol, reltol]);
  else
    args = {"AbsTol", abstol, "RelTol", reltol};
    if (! isempty (wp)), args(end+1:end+2) = {"Waypoints", double(wp)}; endif
    if (av), args(end+1:end+2) = {"ArrayValued", true}; endif
    [q, err] = quadgk (f, double (a), double (b), args{:});
  endif
  if (issingle)
    q = single (q); err = single (err);
  endif
endfunction

function r = ifelse__ (c, x, y)
  if (c), r = x; else, r = y; endif
endfunction
