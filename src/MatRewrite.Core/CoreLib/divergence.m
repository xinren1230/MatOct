## MatRewrite CoreLib: divergence (2-D and 3-D)
function div = divergence (varargin)
  switch (nargin)
    case 2
      [u, v] = varargin{:}; x = 1:columns (u); y = 1:rows (u);
      if (! size_equal (u, v)) error ("divergence: size of U and V must match"); endif
      [ux, ~] = gradient (u, x, y); [~, vy] = gradient (v, x, y); div = ux + vy;
    case 3
      [u, v, w] = varargin{:};
      if (! size_equal (u, v, w)) error ("divergence: size of U, V, and W must match"); endif
      [ux, ~, ~] = gradient (u); [~, vy, ~] = gradient (v); [~, ~, wz] = gradient (w); div = ux + vy + wz;
    case 4
      [x, y, u, v] = varargin{:};
      if (! size_equal (u, v)) error ("divergence: size of U and V must match"); endif
      if (! isvector (x)) x = x(1,:); endif
      if (! isvector (y)) y = y(:,1); endif
      [ux, ~] = gradient (u, x, y); [~, vy] = gradient (v, x, y); div = ux + vy;
    case 6
      [x, y, z, u, v, w] = varargin{:};
      if (! size_equal (u, v, w)) error ("divergence: size of U, V, and W must match"); endif
      if (! isvector (x)) x = x(1,:,1); endif
      if (! isvector (y)) y = y(:,1,1); endif
      if (! isvector (z)) z = z(1,1,:)(:); endif
      [ux, ~, ~] = gradient (u, x, y, z); [~, vy, ~] = gradient (v, x, y, z); [~, ~, wz] = gradient (w, x, y, z);
      div = ux + vy + wz;
    otherwise
      print_usage ();
  endswitch
endfunction
