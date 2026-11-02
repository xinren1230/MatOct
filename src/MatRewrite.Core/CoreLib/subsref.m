function x = subsref (x, idx)
  if (nargin != 2)
    print_usage ();
  endif
  if (! isstruct (idx) || ! all (isfield (idx, {"type", "subs"})))
    error ("subsref: IDX must be a struct array with fields 'type' and 'subs'");
  endif
  for k = 1:numel (idx)
    t = idx(k).type; s = idx(k).subs;
    if (! iscell (s) && ! strcmp (t, "."))
      s = {s};
    endif
    switch (t)
      case "()"
        x = x(s{:});
      case "{}"
        x = x{s{:}};
      case "."
        x = x.(s);
      otherwise
        error ("subsref: invalid index type '%s'", t);
    endswitch
  endfor
endfunction
