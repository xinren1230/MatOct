## MatOct - GPL-3.0-or-later
function [x, fval, exitflag, output] = fminsearch (fun, x0, options, varargin)
  ## FMINSEARCH  Multidimensional unconstrained minimization (Nelder-Mead simplex).
  ##   X = fminsearch (FUN, X0)   [X, FVAL, EXITFLAG, OUTPUT] = fminsearch (FUN, X0, OPTIONS)
  if (nargin < 2), print_usage (); end
  if (nargin < 3 || isempty (options)), options = struct (); end
  if (ischar (fun)), fun = str2func (fun); end
  n = numel (x0);
  tolx = optimget (options, 'TolX', 1e-4);
  tolf = optimget (options, 'TolFun', 1e-4);
  maxit = optimget (options, 'MaxIter', 200 * n);
  maxfev = optimget (options, 'MaxFunEvals', 200 * n);
  if (ischar (maxit)), maxit = 200 * n; end
  if (ischar (maxfev)), maxfev = 200 * n; end
  f = @(v) fun (reshape (v, size (x0)), varargin{:});
  v = zeros (n, n + 1); fv = zeros (1, n + 1);
  v(:,1) = x0(:); fv(1) = f (x0(:));
  for j = 1:n
    y = x0(:);
    if (y(j) != 0), y(j) = 1.05 * y(j); else, y(j) = 0.00025; end
    v(:,j+1) = y; fv(j+1) = f (y);
  end
  [fv, idx] = sort (fv); v = v(:,idx);
  it = 0; fev = n + 1; exitflag = 0;
  while (it < maxit && fev < maxfev)
    if (max (abs (fv(1) - fv(2:end))) <= tolf && max (max (abs (v(:,2:end) - v(:,ones(1,n))))) <= tolx)
      exitflag = 1; break;
    end
    it++;
    xbar = mean (v(:,1:n), 2);
    xr = 2 * xbar - v(:,end); fr = f (xr); fev++;
    if (fr < fv(1))
      xe = 3 * xbar - 2 * v(:,end); fe = f (xe); fev++;
      if (fe < fr), v(:,end) = xe; fv(end) = fe; else, v(:,end) = xr; fv(end) = fr; end
    elseif (fr < fv(n))
      v(:,end) = xr; fv(end) = fr;
    else
      if (fr < fv(end))
        xc = 1.5 * xbar - 0.5 * v(:,end); fc = f (xc); fev++;
        shrink = fc > fr;
        if (! shrink), v(:,end) = xc; fv(end) = fc; end
      else
        xcc = 0.5 * (xbar + v(:,end)); fcc = f (xcc); fev++;
        shrink = fcc >= fv(end);
        if (! shrink), v(:,end) = xcc; fv(end) = fcc; end
      end
      if (shrink)
        for j = 2:n+1
          v(:,j) = v(:,1) + 0.5 * (v(:,j) - v(:,1)); fv(j) = f (v(:,j)); fev++;
        end
      end
    end
    [fv, idx] = sort (fv); v = v(:,idx);
  end
  x = reshape (v(:,1), size (x0)); fval = fv(1);
  output = struct ('iterations', it, 'funcCount', fev, 'algorithm', 'Nelder-Mead simplex direct search', 'message', '');
end
