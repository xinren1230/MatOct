function [G, varargout] = findgroups (varargin)
  % FINDGROUPS  Group numbers for one or more grouping variables.
  n = numel (varargin{1}); keys = zeros (n, nargin); ids = cell (1, nargin);
  for k = 1:nargin
    x = varargin{k};
    if iscategorical (x), x = cellstr (x); end
    [u, ~, j] = unique (x(:)); keys(:, k) = j(:); ids{k} = u;
  end
  [uk, ~, G] = unique (keys, 'rows');
  for k = 1:nargin
    u = ids{k}; varargout{k} = u(uk(:, k));
  end
  if isrow (varargin{1}), G = G.'; end
end
