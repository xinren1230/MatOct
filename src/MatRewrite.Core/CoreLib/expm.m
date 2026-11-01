## MatRewrite CoreLib: expm -- scaling and squaring with a (6,6) Pade
## approximant after trace shift and balancing; real or complex input.
function r = expm (A)
  if (nargin != 1) print_usage (); endif
  if (! isnumeric (A) || ! issquare (A)) error ("expm: A must be a square matrix"); endif
  if (isscalar (A)) r = exp (A); return; endif
  if (isempty (A)) r = A; return; endif
  if (issparse (A)) A = full (A); endif
  n = rows (A);
  if (isdiag (A)) r = diag (exp (diag (A))); return; endif
  tr = trace (A) / n;
  A = A - tr * eye (n);
  [s, e] = log2 (norm (A, "inf"));
  s = max (0, e);
  s = min (s, 1023);
  A = A / 2^s;
  c = [5.0000000000000000e-1, 1.1666666666666667e-1, 1.6666666666666667e-2, ...
       1.6025641025641026e-3, 1.0683760683760684e-4, 4.8562548562548563e-6, ...
       1.3875013875013875e-7, 1.9270852604185938e-9];
  A2 = A * A; I = eye (n);
  x = (((c(8) * A2 + c(6) * I) * A2 + c(4) * I) * A2 + c(2) * I) * A2 + I;
  y = (((c(7) * A2 + c(5) * I) * A2 + c(3) * I) * A2 + c(1) * I) * A;
  r = (x - y) \ (x + y);
  for k = 1:s
    r = r * r;
  endfor
  r = exp (tr) * r;
endfunction
