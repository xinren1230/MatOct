## MatRewrite CoreLib: quiver
function h = quiver (varargin)
  if (nargin < 2) print_usage (); endif
  hg = __quiv__ ("quiver", false, varargin{:});
  if (nargout > 0) h = hg; endif
endfunction
