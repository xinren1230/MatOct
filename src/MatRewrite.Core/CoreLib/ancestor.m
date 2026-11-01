## MatRewrite CoreLib: ancestor
function p = ancestor (h, type, toplevel)
  if (nargin < 2 || nargin > 3) print_usage (); endif
  if (ischar (type)) type = {type}; endif
  if (! iscellstr (type)) error ("ancestor: TYPE must be a string or cell array of strings"); endif
  top = false;
  if (nargin == 3)
    if (! (ischar (toplevel) && strcmpi (toplevel, "toplevel")))
      error ('ancestor: third argument must be "toplevel"');
    endif
    top = true;
  endif
  type = lower (type);
  if (isempty (h)) p = []; return; endif
  p = cell (numel (h), 1);
  for i = 1:numel (h)
    r = []; c = h(i);
    while (! isempty (c) && c != 0 && ishghandle (c))
      if (any (strcmp (get (c, "type"), type)))
        r = c;
        if (! top) break; endif
      endif
      c = get (c, "parent");
    endwhile
    p{i} = r;
  endfor
  if (numel (h) == 1) p = p{1}; endif
endfunction
