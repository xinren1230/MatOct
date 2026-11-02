## MatOct - GPL-3.0-or-later
function optval = optimget (options, optname, default)
  ## OPTIMGET  Get an option from an optimization options structure.
  if (nargin < 2 || ! isstruct (options) || ! ischar (optname))
    print_usage ();
  end
  optname = __optim_canon__ ("optimget", optname);
  f = fieldnames (options);
  i = find (strcmpi (f, optname), 1);
  if (! isempty (i) && ! isempty (options.(f{i})))
    optval = options.(f{i});
  elseif (nargin > 2)
    optval = default;
  else
    optval = [];
  end
end
