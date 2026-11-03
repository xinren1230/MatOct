function d = dir_in_loadpath (dirname, varargin)
  if (nargin < 1 || ! ischar (dirname))
    error ("dir_in_loadpath: DIR must be a directory name");
  endif
  d = [];
  if (isfolder (dirname))
    d = make_absolute_filename (dirname); return;
  endif
  p = strsplit (path (), pathsep ());
  for k = 1:numel (p)
    c = fullfile (p{k}, dirname);
    if (isfolder (c))
      d = c; return;
    endif
  endfor
endfunction
