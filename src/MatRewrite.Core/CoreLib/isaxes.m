## MatRewrite CoreLib: isaxes
function tf = isaxes (h)
  if (nargin != 1) print_usage (); endif
  tf = false (size (h));
  if (! (isnumeric (h) || islogical (h))) return; endif
  for i = 1:numel (h)
    tf(i) = ! isnan (h(i)) && ishghandle (h(i)) && strcmp (get (h(i), "type"), "axes");
  endfor
endfunction
