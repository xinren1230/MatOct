function L = ichol (A, opts = struct ())
  ## Incomplete Cholesky factorization (MatRewrite core implementation).
  if (nargin < 1) print_usage (); endif
  if (! (issparse (A) && issquare (A)))
    error ("ichol: A must be a sparse square matrix");
  endif
  if (! isstruct (opts)) error ("ichol: OPTS must be a structure"); endif
  type = "nofill"; droptol = 0; michol = "off"; diagcomp = 0; shape = "lower";
  if (isfield (opts, "type"))
    type = tolower (opts.type);
    if (! any (strcmp (type, {"nofill", "ict"}))) error ('ichol: TYPE must be "nofill" or "ict"'); endif
  endif
  if (isfield (opts, "droptol"))
    droptol = opts.droptol;
    if (! (isreal (droptol) && isscalar (droptol) && droptol >= 0)) error ("ichol: DROPTOL must be a non-negative real scalar"); endif
  endif
  if (isfield (opts, "michol"))
    michol = tolower (opts.michol);
    if (! any (strcmp (michol, {"on", "off"}))) error ('ichol: MICHOL must be "on" or "off"'); endif
  endif
  if (isfield (opts, "diagcomp"))
    diagcomp = opts.diagcomp;
    if (! (isreal (diagcomp) && isscalar (diagcomp) && diagcomp >= 0)) error ("ichol: DIAGCOMP must be a non-negative real scalar"); endif
  endif
  if (isfield (opts, "shape"))
    shape = tolower (opts.shape);
    if (! any (strcmp (shape, {"lower", "upper"}))) error ('ichol: SHAPE must be "lower" or "upper"'); endif
  endif
  A_in = A;
  if (diagcomp > 0)
    A_in += diagcomp * diag (diag (A));
  endif
  if (strcmp (type, "nofill"))
    L = __ichol0__ (A_in, michol);
  else
    L = __icholt__ (A_in, droptol, michol);
  endif
  if (strcmp (shape, "upper")) L = L'; endif
endfunction
