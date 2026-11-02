## MatOct - GPL-3.0-or-later
function s = optimset (varargin)
  ## OPTIMSET  Create or alter an optimization options structure.
  names = __optim_names__ ();
  if (nargin == 0 && nargout == 0)
    printf ("\nAll possible optimization options:\n\n");
    printf ("  %s\n", names{:}); printf ("\n"); return;
  end
  if (nargin == 1 && ischar (varargin{1}))
    fcn = varargin{1};
    try
      s = feval (fcn, "defaults");
    catch
      error ("optimset: no defaults for function '%s'", fcn);
    end
    return;
  end
  s = cell2struct (cell (numel (names), 1), names, 1);
  k = 1;
  while (k <= nargin)
    if (isstruct (varargin{k}))
      o = varargin{k}; f = fieldnames (o);
      for j = 1:numel (f)
        if (! isempty (o.(f{j})) || ! isfield (s, f{j})), s.(f{j}) = o.(f{j}); end
      end
      k = k + 1;
    elseif (ischar (varargin{k}) && k < nargin)
      nm = __optim_canon__ ("optimset", varargin{k});
      s.(nm) = varargin{k+1};
      k = k + 2;
    else
      error ("optimset: invalid input");
    end
  end
end
