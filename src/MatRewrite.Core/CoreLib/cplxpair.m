## MatRewrite - GPL-3.0-or-later
function zs = cplxpair (z, tol, dim)
  if (nargin < 1)
    print_usage ();
  endif
  if (isempty (z))
    zs = zeros (size (z)); return;
  endif
  cls = "double"; if (isa (z, "single")), cls = "single"; endif
  if (nargin < 2 || isempty (tol))
    tol = 100 * eps (cls);
  elseif (! isscalar (tol) || tol < 0 || tol >= 1)
    error ("cplxpair: TOL must be a scalar number in the range 0 <= TOL < 1");
  endif
  nd = ndims (z);
  if (nargin < 3)
    dim = find (size (z) > 1, 1); if (isempty (dim)), dim = 1; endif
  else
    dim = floor (dim);
    if (dim < 1 || dim > nd), error ("cplxpair: invalid dimension DIM"); endif
  endif
  perm = [dim:nd, 1:dim-1];
  z = permute (z, perm); sz = size (z); n = sz(1);
  z = reshape (z, n, []);
  zs = z;
  for j = 1:columns (z)
    c = z(:,j);
    [~, k] = sort (real (c)); c = c(k);
    isr = abs (imag (c)) ./ (abs (c) + realmin (cls)) <= tol;
    c(isr) = real (c(isr));
    cc = c(! isr); rr = c(isr);
    p = numel (cc); out = zeros (n, 1, cls);
    i = 1;
    while (i <= p)
      if (i + 1 > p)
        error ("cplxpair: could not pair all complex numbers");
      endif
      [v, k] = min (abs (cc(i+1:p) - conj (cc(i))));
      if (v >= tol * abs (cc(i)))
        error ("cplxpair: could not pair all complex numbers");
      endif
      if (imag (cc(i)) > 0)
        out([i, i+1]) = [conj(cc(i)), cc(i)];
      else
        out([i, i+1]) = [conj(cc(k+i)), cc(k+i)];
      endif
      cc(k+i) = cc(i+1);
      i += 2;
    endwhile
    out(p+1:end) = rr;
    zs(:,j) = out;
  endfor
  zs = ipermute (reshape (zs, sz), perm);
endfunction
