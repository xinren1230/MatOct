function args = argnames (fcn)
  s = func2str (fcn);
  t = regexp (s, '^@\(([^)]*)\)', 'tokens', 'once');
  if (isempty (t) || isempty (strtrim (t{1})))
    args = cell (0, 1);
  else
    args = strtrim (strsplit (t{1}, ","))(:);
  endif
endfunction
