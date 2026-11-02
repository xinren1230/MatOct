function [B, TF] = rmmissing (A, dim)
  % RMMISSING  Remove rows (or columns) that contain missing entries.
  M = ismissing (A);
  if nargin < 2
    if isvector (A) && ~istable (A), dim = find (size (A) ~= 1, 1); if isempty (dim), dim = 1; end; else, dim = 1; end
  end
  if dim == 1, TF = any (M, 2); else, TF = any (M, 1); end
  if isvector (A) && ~istable (A), B = A(~TF); TF = TF(:)'; if iscolumn (A), TF = TF(:); end; return; end
  if dim == 1, B = A(~TF, :); else, B = A(:, ~TF); end
end
