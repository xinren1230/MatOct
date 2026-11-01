## MatRewrite CoreLib: sombrero
function [x, y, z] = sombrero (n = 41)
  if (nargin > 1) print_usage (); endif
  if (n <= 1) error ("sombrero: number of grid lines N must be greater than 1"); endif
  [xx, yy] = meshgrid (linspace (-8, 8, n));
  r = sqrt (xx.^2 + yy.^2) + eps;
  zz = sin (r) ./ r;
  if (nargout == 0)
    surf (xx, yy, zz); box ("off");
  else
    x = xx; y = yy; z = zz;
  endif
endfunction
