## MatRewrite CoreLib: hggroup ([HAX], PROP, VAL, ...)
function h = hggroup (varargin)
  [hax, varargin] = __plt_get_axis_arg__ ("hggroup", varargin{:});
  if (isempty (hax)) hax = gca (); endif
  h = __go_hggroup__ (hax);
  for k = 1:2:numel (varargin) - 1
    if (! strcmpi (varargin{k}, "__appdata__"))
      try set (h, varargin{k}, varargin{k+1}); end_try_catch
    endif
  endfor
endfunction
