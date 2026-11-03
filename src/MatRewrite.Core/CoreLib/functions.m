function s = functions (fh)
  if (! is_function_handle (fh))
    error ("functions: FCN_HANDLE argument must be a valid function handle");
  endif
  str = func2str (fh);
  s.function = str;
  if (str(1) == "@")
    s.type = "anonymous";
    s.file = "";
    s.workspace = {struct()};
    s.within_file_path = "";
  else
    s.type = "simple";
    s.file = "";
  endif
endfunction
