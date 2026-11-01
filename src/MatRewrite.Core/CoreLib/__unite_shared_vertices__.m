## MatRewrite CoreLib: merge (nearly) coincident vertices and drop degenerate faces.
function [faces, vertices, J] = __unite_shared_vertices__ (faces, vertices)
  nanv = any (isnan (vertices), 2);
  lut = (1:rows (vertices))';
  J = lut;
  for d = 1:3
    [s, ix] = sortrows (vertices, 1 + mod ((0:2) + d, 3));
    keep = [true; any(abs (diff (s, 1, 1)) > eps * abs (s(2:end,:) + s(1:end-1,:)), 2)];
    vertices = s(keep,:);
    J = J(ix(keep));
    l = zeros (numel (ix), 1);
    l(ix) = cumsum (keep);
    lut = l(lut);
  endfor
  [J, ix] = sort (J);
  j = zeros (1, numel (ix));
  j(ix) = 1:numel (ix);
  vertices = vertices(ix,:);
  if (any (nanv))
    j(end+1) = numel (ix) + 1;
    vertices(end+1,:) = NaN;
    lut(nanv) = rows (vertices);
  endif
  faces = reshape (j(lut(faces)), size (faces));
  faces = faces(all (faces - faces(:, [2 3 1]) != 0, 2), :);
endfunction
