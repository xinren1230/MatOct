function y = __itsolve_apply__ (A, x, varargin)
  ## apply a matrix or function handle (with optional extra args)
  if (isempty (A))
    y = x;
  elseif (is_function_handle (A))
    y = A (x, varargin{:});
  else
    if (numel (varargin) > 0 && strcmp (varargin{1}, "transp"))
      y = A' * x;
    else
      y = A * x;
    endif
  endif
endfunction
