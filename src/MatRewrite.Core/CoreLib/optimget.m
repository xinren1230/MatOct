## MatOct - GPL-3.0-or-later
function v = optimget (s, name, default)
  ## OPTIMGET  Get an option from an optimization options structure.
  if (nargin < 3), default = []; end
  v = default;
  if (isstruct (s))
    f = fieldnames (s);
    i = find (strcmpi (f, name), 1);
    if (! isempty (i) && ! isempty (s.(f{i}))), v = s.(f{i}); end
  end
end
