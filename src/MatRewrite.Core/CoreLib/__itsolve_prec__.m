function y = __itsolve_prec__ (M1, M2, x, transp)
  ## y = M2 \ (M1 \ x)  (or the transposed variant)
  if (nargin < 4) transp = false; endif
  if (transp)
    y = __solve1__ (M2, x, true); y = __solve1__ (M1, y, true);
  else
    y = __solve1__ (M1, x, false); y = __solve1__ (M2, y, false);
  endif
endfunction
function y = __solve1__ (M, x, transp)
  if (isempty (M))
    y = x;
  elseif (is_function_handle (M))
    if (transp) y = M (x, "transp"); else y = M (x); endif
  elseif (transp)
    y = M' \ x;
  else
    y = M \ x;
  endif
endfunction
