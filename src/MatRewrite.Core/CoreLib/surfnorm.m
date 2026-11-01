## MatRewrite CoreLib: surfnorm (normals via cross product of grid tangents).
function [Nx, Ny, Nz] = surfnorm (varargin)
  [hax, varargin, nargs] = __plt_get_axis_arg__ ("surfnorm", varargin{:});
  if (nargs != 1 && nargs != 3) print_usage (); endif
  if (nargs == 1)
    z = varargin{1};
    [x, y] = meshgrid (1:columns (z), 1:rows (z));
    ioff = 2;
  else
    [x, y, z] = varargin{1:3};
  endif
  if (! (isnumeric (x) && isreal (x) && ismatrix (x) && isnumeric (y) && isreal (y) && ismatrix (y) && isnumeric (z) && isreal (z) && ismatrix (z)))
    error ("surfnorm: X, Y, and Z must be 2-D real matrices");
  endif
  if (! size_equal (x, y, z))
    error ("surfnorm: X, Y, and Z must have the same dimensions");
  endif
  x = double (x); y = double (y); z = double (z);
  [r, c] = size (z);
  if (r < 2 || c < 2)
    nx = zeros (size (z)); ny = nx; nz = ones (size (z));
  else
    ex = @(m) [2*m(1,:)-m(2,:); m; 2*m(end,:)-m(end-1,:)];
    ey = @(m) [2*m(:,1)-m(:,2), m, 2*m(:,end)-m(:,end-1)];
    xx = ey (ex (x)); yy = ey (ex (y)); zz = ey (ex (z));
    u = [xx(3:end,2:end-1)-xx(1:end-2,2:end-1)](:); u(:,2) = (yy(3:end,2:end-1)-yy(1:end-2,2:end-1))(:); u(:,3) = (zz(3:end,2:end-1)-zz(1:end-2,2:end-1))(:);
    v = [xx(2:end-1,3:end)-xx(2:end-1,1:end-2)](:); v(:,2) = (yy(2:end-1,3:end)-yy(2:end-1,1:end-2))(:); v(:,3) = (zz(2:end-1,3:end)-zz(2:end-1,1:end-2))(:);
    n = cross (v, u, 2);
    len = sqrt (sum (n.^2, 2)); len(len == 0) = 1;
    n ./= len;
    nx = reshape (n(:,1), r, c); ny = reshape (n(:,2), r, c); nz = reshape (n(:,3), r, c);
  endif
  if (nargout == 0)
    if (isempty (hax)) hax = gca (); endif
    surf (hax, x, y, z);
    hold (hax, "on");
    plot3 (hax, [x(:).'; x(:).' + nx(:).'; NaN(1, numel (x))](:), [y(:).'; y(:).' + ny(:).'; NaN(1, numel (y))](:), [z(:).'; z(:).' + nz(:).'; NaN(1, numel (z))](:), "r");
    hold (hax, "off");
  else
    Nx = nx; Ny = ny; Nz = nz;
  endif
endfunction
