## MatOct - GPL-3.0-or-later
function name = __optim_canon__ (caller, name)
  opts = __optim_names__ ();
  idx = strncmpi (opts, name, length (name));
  exact = strcmpi (opts, name);
  if (any (exact))
    name = opts{exact};
  elseif (sum (idx) == 1)
    name = opts{idx};
  elseif (sum (idx) == 0)
    warning ("%s: unrecognized option: '%s'", caller, name);
  else
    error ("%s: ambiguous option: '%s' (%s)", caller, name, strjoin (opts(idx).', ", "));
  end
end
