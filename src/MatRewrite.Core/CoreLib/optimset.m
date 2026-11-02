## MatOct - GPL-3.0-or-later
function s = optimset (varargin)
  ## OPTIMSET  Create or alter an optimization options structure.
  ##   S = optimset ('Name', value, ...)   S = optimset (OLD, 'Name', value, ...)
  names = {'Display','MaxIter','MaxFunEvals','TolX','TolFun','OutputFcn','FunValCheck','GradObj','Jacobian','PlotFcns'};
  if (nargin == 0 && nargout == 0)
    printf ('  %s\n', names{:}); return;
  end
  s = struct ();
  k = 1;
  while (k <= nargin)
    if (isstruct (varargin{k}))
      f = fieldnames (varargin{k});
      for j = 1:numel (f), s.(f{j}) = varargin{k}.(f{j}); end
      k = k + 1;
    else
      s.(varargin{k}) = varargin{k+1};
      k = k + 2;
    end
  end
end
