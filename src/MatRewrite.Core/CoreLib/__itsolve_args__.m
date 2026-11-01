function [A, b, tol, maxit, M1, M2, x0, n] = __itsolve_args__ (fn, A, b, tol, maxit, M1, M2, x0)
  if (! (isnumeric (A) || is_function_handle (A)))
    error ("%s: A must be a numeric matrix or a function handle", fn);
  endif
  if (isnumeric (A) && rows (A) != columns (A))
    error ("%s: A must be a square matrix", fn);
  endif
  b = b(:);
  n = rows (b);
  if (isempty (tol)) tol = 1e-6; endif
  if (isempty (maxit)) maxit = min (n, 20); endif
  if (isempty (x0)) x0 = zeros (n, 1); endif
  x0 = x0(:);
  if (isnumeric (M1) && ! isempty (M1) && isempty (M2) && 0) endif
endfunction
