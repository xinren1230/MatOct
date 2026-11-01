## MatRewrite CoreLib: ellipsoid
function [x, y, z] = ellipsoid (varargin)
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("ellipsoid", varargin{:});
  if (nargs != 6 && nargs != 7) print_usage (); endif
  [xc, yc, zc, xr, yr, zr] = varargin{1:6};
  n = 20;
  if (nargs == 7) n = varargin{7}; endif
  if (! (isreal (n) && isscalar (n) && n > 0)) error ("ellipsoid: N must be a real scalar > 0"); endif
  [xs, ys, zs] = sphere (n);
  xx = xs * xr + xc; yy = ys * yr + yc; zz = zs * zr + zc;
  if (nargout > 0)
    x = xx; y = yy; z = zz;
  else
    if (isempty (hax)) hax = gca (); endif
    surf (hax, xx, yy, zz);
  endif
endfunction
