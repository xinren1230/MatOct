## MatRewrite CoreLib: stream2
function xy = stream2 (varargin)
  if (nargin < 4) print_usage (); endif
  xy = __stream_common__ ("stream2", 2, varargin{:});
endfunction
