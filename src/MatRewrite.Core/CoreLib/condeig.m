## MatRewrite CoreLib: condeig -- eigenvalue condition numbers.
function [v, lambda, c] = condeig (a)
  if (nargin != 1) print_usage (); endif
  if (! isnumeric (a) || ! issquare (a)) error ("condeig: A must be a square numeric matrix"); endif
  if (issparse (a) && (nargout == 0 || nargout == 1) && exist ("svds", "file"))
    a = full (a);
  endif
  [v, lambda] = eig (a);
  if (isempty (a))
    cv = lambda;
  else
    vl = inv (v)';
    vl = vl ./ repmat (sqrt (sum (abs (vl .^ 2))), rows (vl), 1);
    cv = abs (1 ./ dot (vl, v)');
  endif
  if (nargout == 0 || nargout == 1)
    v = cv;
  else
    c = cv;
  endif
endfunction
