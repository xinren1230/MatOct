function str = formula (fcn)
  s = func2str (fcn);
  str = strtrim (regexprep (s, '^@\([^)]*\)', '', 'once'));
endfunction
