function [L, U, P] = ilu (A, opts = struct ())
  ## Incomplete LU factorization (MatRewrite core implementation).
  if (nargin < 1) print_usage (); endif
  if (! (issparse (A) && issquare (A)))
    error ("ilu: A must be a sparse square matrix");
  endif
  if (! isstruct (opts)) error ("ilu: OPTS must be a structure"); endif
  type = "nofill"; droptol = 0; milu = "off"; udiag = 0; thresh = 1;
  if (isfield (opts, "type"))
    type = tolower (opts.type);
    if (! any (strcmp (type, {"nofill", "crout", "ilutp"}))) error ("ilu: invalid TYPE specified"); endif
  endif
  if (isfield (opts, "droptol"))
    droptol = opts.droptol;
    if (! (isreal (droptol) && isscalar (droptol) && droptol >= 0)) error ("ilu: DROPTOL must be a non-negative real scalar"); endif
  endif
  if (isfield (opts, "milu"))
    milu = tolower (opts.milu);
    if (! any (strcmp (milu, {"off", "col", "row"}))) error ('ilu: MILU must be one of "off", "col", or "row"'); endif
  endif
  if (isfield (opts, "udiag"))
    udiag = opts.udiag;
    if (! (isscalar (udiag) && (udiag == 0 || udiag == 1))) error ("ilu: UDIAG must be 0 or 1"); endif
  endif
  if (isfield (opts, "thresh"))
    thresh = opts.thresh;
    if (! (isscalar (thresh) && thresh >= 0 && thresh <= 1)) error ("ilu: THRESH must be a scalar in the range [0, 1]"); endif
  endif
  n = rows (A);
  if (n == 0) L = A; U = A; P = A; return; endif
  switch (type)
    case "nofill"
      [L, U] = __ilu0__ (A, milu);
    case "crout"
      [L, U] = __iluc__ (A, droptol, milu);
    otherwise
      [L, U, P] = __ilutp__ (A, droptol, thresh, milu, udiag);
  endswitch
  if (nargout <= 1)
    L = L + U - speye (n);
  endif
  if (nargout < 3) P = speye (n); endif
endfunction
