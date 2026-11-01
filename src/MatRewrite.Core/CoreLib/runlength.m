## MatRewrite CoreLib: runlength
function [count, value] = runlength (x)
  if (nargin != 1) print_usage (); endif
  if (! (isnumeric (x) || islogical (x))) error ("runlength: X must be a numeric or logical vector"); endif
  if (! isvector (x)) error ("runlength: X must be a numeric or logical vector"); endif
  x = x(:).';
  e = [find(x(1:end-1) != x(2:end)), numel(x)];
  count = diff ([0, e]);
  if (nargout > 1) value = x(e); endif
endfunction
