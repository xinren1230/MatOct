function a = make_absolute_filename (n)
  if (! ischar (n))
    error ("make_absolute_filename: FILE argument must be a filename");
  endif
  if (is_absolute_filename (n)) a = n; else a = fullfile (pwd (), n); endif
  a = strrep (a, [filesep() "." filesep()], filesep ());
endfunction
