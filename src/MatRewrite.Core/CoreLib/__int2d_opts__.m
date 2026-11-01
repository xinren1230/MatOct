## MatRewrite - GPL-3.0-or-later
## Shared option parsing/validation for integral2, integral3 and quad2d.
function o = __int2d_opts__ (fn, f, lims, names, args, extra)
  if (! is_function_handle (f))
    error ("%s: F must be a function handle", fn);
  endif
  for k = 1:numel (lims)
    v = lims{k};
    if (k <= 2)
      if (! (isnumeric (v) && isscalar (v) && isreal (v)))
        error ("%s: %s must be a real scalar", fn, names{k});
      endif
    elseif (! is_function_handle (v) && ! (isnumeric (v) && isscalar (v) && isreal (v)))
      error ("%s: %s must be a real scalar or a function", fn, names{k});
    endif
  endfor
  o = struct ("abstol", 1e-10, "reltol", 1e-6, "method", "auto", "vectorized", true, ...
              "maxfunevals", 2000, "singular", true, "failureplot", false);
  if (any (cellfun (@(x) isa (x, "single"), lims)))
    o.abstol = 1e-5; o.reltol = 1e-4;
  endif
  for k = 1:2:numel (args)
    p = args{k};
    if (! ischar (p))
      error ("%s: property PROP must be a string", fn);
    endif
    v = args{k+1};
    switch (tolower (p))
      case {"abstol", "reltol"}
        nm = ifelse3_ (strcmpi (p, "abstol"), "AbsTol", "RelTol");
        if (! (isnumeric (v) && isreal (v)))
          error ("%s: %s value must be a numeric value", fn, nm);
        elseif (! isscalar (v))
          error ("%s: %s value must be a numeric scalar", fn, nm);
        elseif (v < 0)
          error ("%s: %s value must be >= 0", fn, nm);
        endif
        o.(tolower (p)) = v;
      case "method"
        if (! any (strcmpi (extra, "method")))
          error ("%s: unknown property '%s'", fn, p);
        endif
        if (! any (strcmpi (v, {"auto", "tiled", "iterated"})))
          error ("%s: unrecognized method '%s'", fn, v);
        endif
        o.method = tolower (v);
      case {"vectorized", "singular", "failureplot"}
        if (strcmpi (p, "singular") || strcmpi (p, "failureplot"))
          if (! any (strcmpi (extra, p))), error ("%s: unknown property '%s'", fn, p); endif
        endif
        nm = {"Vectorized", "Singular", "FailurePlot"}{find (strcmpi (p, {"vectorized", "singular", "failureplot"}))};
        if (! (isscalar (v) && (islogical (v) || isnumeric (v))))
          error ("%s: %s must be a logical value", fn, nm);
        endif
        o.(tolower (p)) = logical (v);
      case "maxfunevals"
        if (! any (strcmpi (extra, p))), error ("%s: unknown property '%s'", fn, p); endif
        if (! (isnumeric (v) && isscalar (v) && v == fix (v)))
          error ("%s: MaxFunEvals value must be a scalar integer", fn);
        elseif (v < 1)
          error ("%s: MaxFunEvals value must be a scalar integer >= 1", fn);
        endif
        o.maxfunevals = v;
      otherwise
        error ("%s: unknown property '%s'", fn, p);
    endswitch
  endfor
endfunction

function r = ifelse3_ (c, a, b)
  if (c), r = a; else, r = b; endif
endfunction
