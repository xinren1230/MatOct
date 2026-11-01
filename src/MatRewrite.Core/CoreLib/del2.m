## MatRewrite - GPL-3.0-or-later
function L = del2 (M, varargin)
  if (nargin < 1)
    print_usage ();
  endif
  nd = ndims (M); sz = size (M);
  dx = cell (1, nd);
  if (nargin == 1 || (nargin == 2 && isscalar (varargin{1})))
    h = 1; if (nargin == 2), h = varargin{1}; endif
    for i = 1:nd, dx{i} = h * ones (sz(i), 1); end
  elseif (numel (varargin) <= nd)
    sp = varargin; sp(end+1:nd) = {1};
    sp([1 2]) = sp([2 1]);
    for i = 1:nd
      s = sp{i};
      if (isscalar (s))
        dx{i} = s * ones (sz(i), 1);
      elseif (isvector (s))
        if (numel (s) != sz(i))
          error ("del2: number of elements in spacing vector %d does not match dimension %d of M", i, i);
        endif
        dx{i} = diff (s(:));
      else
        error ("del2: spacing element %d must be a scalar or vector", i);
      endif
    endfor
  else
    print_usage ();
  endif
  L = zeros (sz);
  for i = 1:nd
    n = sz(i);
    if (n < 3), continue; endif
    perm = [i, 1:i-1, i+1:nd];
    P = reshape (permute (M, perm), n, []);
    h = dx{i}(:);
    h1 = h(1:n-2); h2 = h(2:n-1);
    D = zeros (size (P));
    D(2:n-1,:) = ((P(1:n-2,:) - P(2:n-1,:)) ./ h1 + (P(3:n,:) - P(2:n-1,:)) ./ h2) ./ (h1 + h2);
    if (n == 3)
      D(1,:) = D(2,:); D(3,:) = D(2,:);
    else
      D(1,:) = (h(1)+h(2))/h(2) * D(2,:) - h(1)/h(2) * D(3,:);
      D(n,:) = (h(n-1)+h(n-2))/h(n-2) * D(n-1,:) - h(n-1)/h(n-2) * D(n-2,:);
    endif
    psz = sz(perm);
    L += ipermute (reshape (D, psz), perm);
  endfor
  L ./= nd;
endfunction
