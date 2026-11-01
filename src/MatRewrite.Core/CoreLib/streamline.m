## MatRewrite CoreLib: streamline
function h = streamline (varargin)
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("streamline", varargin{:});
  if (nargs == 0) print_usage (); endif
  nd = ndims (varargin{1});
  if (nd > 3) error ("streamline: input data must be 2-D or 3-D"); endif
  if (isempty (hax)) hax = gca (); else hax = hax(1); endif
  hh = [];
  if (nd == 2)
    c = stream2 (varargin{:});
  else
    c = stream3 (varargin{:});
  endif
  for i = 1:numel (c)
    sl = c{i};
    if (isempty (sl)) continue; endif
    if (columns (sl) == 2)
      hh(end+1,1) = line (sl(:,1), sl(:,2), "color", "b", "parent", hax);
    else
      hh(end+1,1) = line (sl(:,1), sl(:,2), sl(:,3), "color", "b", "parent", hax);
    endif
  endfor
  if (nargout > 0) h = hh; endif
endfunction
