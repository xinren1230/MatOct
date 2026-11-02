function linkaxes (varargin)
  % LINKAXES  Accepted for compatibility (axes are not linked interactively).
  if nargin < 1, print_usage (); end
  if ! all (ishghandle (varargin{1}(:))), error ('linkaxes: HAX must be a vector of axes handles'); end
end
