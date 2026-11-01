## MatRewrite CoreLib: quiver3
function h = quiver3 (varargin)
  if (nargin < 2) print_usage (); endif
  hg = __quiv__ ("quiver3", true, varargin{:});
  if (nargout > 0) h = hg; endif
endfunction
