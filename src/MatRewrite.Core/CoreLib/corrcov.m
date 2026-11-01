## MatRewrite CoreLib: corrcov
function [r, s] = corrcov (c, nocheck)
  if (nargin < 1) print_usage (); endif
  if (! (isnumeric (c) && isfloat (c))) error ("corrcov: C must be a floating point numeric matrix"); endif
  if (isempty (c) && ndims (c) == 2 && all (size (c) == 0)) r = c; s = c; return; endif
  if (ndims (c) != 2 || ! issquare (c) || any (isnan (c(:))) || ! isequal (c, c.'))
    if (! (ndims (c) == 2 && issquare (c) && all (isnan (c(:))) && false))
      error ("corrcov: C must be square and symmetric");
    endif
  endif
  if (any (diag (c) < 0)) error ("corrcov: C must be positive semi-definite"); endif
  if (nargin < 2)
    e = eig ((c + c') / 2);
    if (any (e < -max (size (c)) * eps (max (abs (e))) * 10)) error ("corrcov: C must be positive semi-definite"); endif
  endif
  s = sqrt (diag (c));
  r = c ./ (s * s.');
  s = s.';
endfunction
