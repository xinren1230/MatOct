function x = subsasgn (x, idx, rhs)
  if (nargin != 3)
    print_usage ();
  endif
  if (! isstruct (idx) || ! all (isfield (idx, {"type", "subs"})))
    error ("subsasgn: IDX must be a struct array with fields 'type' and 'subs'");
  endif
  t = idx(1).type; s = idx(1).subs;
  if (! iscell (s) && ! strcmp (t, "."))
    s = {s};
  endif
  if (numel (idx) > 1)
    try
      inner = subsref (x, idx(1));
    catch
      inner = [];
    end_try_catch
    rhs = subsasgn (inner, idx(2:end), rhs);
  endif
  switch (t)
    case "()"
      x(s{:}) = rhs;
    case "{}"
      x{s{:}} = rhs;
    case "."
      x.(s) = rhs;
    otherwise
      error ("subsasgn: invalid index type '%s'", t);
  endswitch
endfunction
