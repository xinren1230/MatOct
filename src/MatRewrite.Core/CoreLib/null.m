## MatRewrite CoreLib: null -- orthonormal null-space basis from the SVD.
function retval = null (A, tol)
  if (nargin < 1) print_usage (); endif
  if (nargin == 2 && ischar (tol))
    if (strcmpi (tol, "r")) error ("null: option \"r\" for rational not yet implemented"); endif
    error ("null: TOL must be a numeric scalar");
  endif
  if (! isnumeric (A) && ! islogical (A)) error ("null: A must be a numeric matrix"); endif
  cls = "double"; if (isa (A, "single")) cls = "single"; endif
  if (isempty (A))
    if (columns (A) == 0 || ! isfloat (A)) retval = zeros (0, 0, cls);
    else retval = eye (columns (A), cls); endif
    return;
  endif
  A = full (A);
  if (! isfloat (A)) A = double (A); endif
  [~, S, V] = svd (A);
  s = diag (S); if (isempty (s)) s = 0; endif
  if (nargin < 2) tol = max (size (A)) * s(1) * eps (cls); endif
  rank = sum (s > tol);
  n = columns (A);
  retval = V(:, rank+1:n);
  retval(abs (retval) < eps (cls)) = 0;
  retval = cast (retval, cls);
endfunction
