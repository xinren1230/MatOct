## MatRewrite CoreLib: split a leading axes handle off an argument list.
function [h, varargin, narg] = __plt_get_axis_arg__ (caller, varargin)
  h = [];
  if (numel (varargin) > 0 && isscalar (varargin{1}) && isnumeric (varargin{1}) && varargin{1} != 0
      && ishghandle (varargin{1}) && strcmp (get (varargin{1}, "type"), "axes"))
    h = varargin{1};
    varargin(1) = [];
  else
    k = find (cellfun (@(s) ischar (s) && strcmpi (s, "parent"), varargin), 1);
    if (! isempty (k) && k < numel (varargin))
      h = varargin{k+1};
      varargin(k:k+1) = [];
    endif
  endif
  narg = numel (varargin);
endfunction
