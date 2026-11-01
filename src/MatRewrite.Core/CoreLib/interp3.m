## MatRewrite CoreLib: interp3 (meshgrid-oriented) built on interpn.
function vi = interp3 (varargin)
  if (nargin < 1) print_usage (); endif
  method = "linear"; extrap = [];
  args = varargin;
  if (numel (args) > 1 && ischar (args{end}))
    method = args{end}; args(end) = [];
  elseif (numel (args) > 2 && ischar (args{end-1}))
    method = args{end-1}; extrap = args{end}; args(end-1:end) = [];
  endif
  switch (numel (args))
    case 1
      v = args{1}; vi = interp3 (v, 1, method);
      return;
    case 2
      v = args{1}; n = args{2};
      [x, y, z] = meshgrid (1:columns (v), 1:rows (v), 1:size (v, 3));
      s = 2^(-n);
      [xi, yi, zi] = meshgrid (1:s:columns (v), 1:s:rows (v), 1:s:size (v, 3));
    case 4
      v = args{1}; xi = args{2}; yi = args{3}; zi = args{4};
      [x, y, z] = meshgrid (1:columns (v), 1:rows (v), 1:size (v, 3));
    case 7
      [x, y, z, v, xi, yi, zi] = args{:};
    otherwise
      print_usage ();
  endswitch
  if (! isvector (x)) x = squeeze (x(1,:,1)); endif
  if (! isvector (y)) y = squeeze (y(:,1,1)); endif
  if (! isvector (z)) z = squeeze (z(1,1,:)); endif
  if (isvector (xi) && isvector (yi) && isvector (zi) && ! size_equal (xi, yi, zi))
    [xi, yi, zi] = meshgrid (xi, yi, zi);
  endif
  vp = permute (v, [2 1 3]);
  if (isempty (extrap))
    vi = interpn (x(:), y(:), z(:), vp, xi, yi, zi, method);
  else
    vi = interpn (x(:), y(:), z(:), vp, xi, yi, zi, method, extrap);
  endif
endfunction
