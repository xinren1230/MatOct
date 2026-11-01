function obj = onCleanup (fcn)
  ## Minimal onCleanup: stores the cleanup function (run on 'delete').
  if (nargin != 1) print_usage (); endif
  if (! is_function_handle (fcn))
    error ("onCleanup: argument must be a function handle");
  endif
  obj = struct ("task", fcn);
endfunction
