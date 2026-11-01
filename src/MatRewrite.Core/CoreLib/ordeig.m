## MatRewrite CoreLib: ordeig -- eigenvalues of a (quasi-)triangular matrix in diagonal order.
function lambda = ordeig (A, B)
  if (nargin < 1) print_usage (); endif
  if (! isnumeric (A) || ! issquare (A)) error ("ordeig: A must be a square matrix"); endif
  n = rows (A);
  if (nargin == 1)
    B = eye (n);
    if (isreal (A))
      if (! isquasitri (A)) error ("ordeig: A must be quasi-triangular (i.e., upper block triangular with 1x1 or 2x2 blocks on the diagonal)"); endif
    elseif (! istriu (A))
      error ("ordeig: A must be upper-triangular when it is complex");
    endif
  else
    if (! isnumeric (B) || ! issquare (B)) error ("ordeig: B must be a square matrix"); endif
    if (rows (B) != n) error ("ordeig: A and B must be the same size"); endif
    if (isreal (A) && isreal (B))
      if (! isquasitri (A) || ! isquasitri (B))
        error ("ordeig: A and B must be quasi-triangular (i.e., upper block triangular with 1x1 or 2x2 blocks on the diagonal)");
      endif
    elseif (! istriu (A) || ! istriu (B))
      error ("ordeig: A and B must both be upper-triangular if either is complex");
    endif
  endif
  lambda = zeros (n, 1);
  i = 1;
  while (i <= n)
    if (i < n && (A(i+1,i) != 0 || B(i+1,i) != 0))
      lambda(i:i+1) = eig (A(i:i+1,i:i+1), B(i:i+1,i:i+1));
      i += 2;
    else
      lambda(i) = A(i,i) / B(i,i);
      i += 1;
    endif
  endwhile
endfunction

function tf = isquasitri (A)
  tf = isempty (A) || (all (all (tril (A, -2) == 0)) && ! any (diag (A, -1)(1:end-1) & diag (A, -1)(2:end)));
endfunction
