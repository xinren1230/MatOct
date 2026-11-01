## MatRewrite CoreLib: stream3
function xyz = stream3 (varargin)
  if (nargin < 6 || nargin > 10) print_usage (); endif
  xyz = __stream_common__ ("stream3", 3, varargin{:});
endfunction
