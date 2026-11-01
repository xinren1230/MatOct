## MatRewrite - GPL-3.0-or-later
## quadcc wrapper that also accepts complex-valued integrands.
function q = __qcc__ (g, a, b, tol)
  m = (a + b) / 2; if (! isfinite (m)), m = 0.5; endif
  if (isreal (g (m)))
    q = quadcc (g, a, b, tol);
  else
    q = quadcc (@(t) real (g (t)), a, b, tol) + 1i * quadcc (@(t) imag (g (t)), a, b, tol);
  endif
endfunction
