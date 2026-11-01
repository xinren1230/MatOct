## MatRewrite CoreLib: smooth3 (box / gaussian 3-D smoothing, replicate padding).
function sd = smooth3 (data, method = "box", sz = 3, std_dev = 0.65)
  if (nargin < 1) print_usage (); endif
  if (ndims (data) != 3)
    error ("smooth3: DATA must be a 3-D numeric matrix");
  endif
  if (! isnumeric (data))
    if (islogical (data)) data = double (data);
    else error ("smooth3: DATA must be a 3-D numeric matrix"); endif
  endif
  if (! isfloat (data)) data = double (data); endif
  if (! ischar (method)) error ("smooth3: METHOD must be a string"); endif
  if (strcmp (method, ":")) method = "box"; endif
  if (ischar (sz) && strcmp (sz, ":")) sz = 3; endif
  if (ischar (std_dev) && strcmp (std_dev, ":")) std_dev = 0.65; endif
  if (! isnumeric (sz) || ! isreal (sz))
    error ("smooth3: SZ must be a real scalar or 3-element vector");
  elseif (isscalar (sz))
    sz = [sz sz sz];
  elseif (numel (sz) != 3)
    error ("smooth3: SZ of the convolution kernel must be a scalar or 3-element vector");
  endif
  sz = sz(:).';
  if (any (sz < 1))
    error ("smooth3: SZ of the convolution kernel must consist of positive odd integers");
  endif
  if (any (rem (sz, 2) != 1))
    error ("smooth3: SZ of the convolution kernel must consist of odd integers");
  endif
  switch (lower (method))
    case {"b", "box"}
      k = ones (sz) / prod (sz);
    case {"g", "gaussian"}
      if (! isnumeric (std_dev) || ! isreal (std_dev))
        error ("smooth3: STD_DEV must be a real scalar or 3-element vector");
      elseif (isscalar (std_dev))
        std_dev = [std_dev std_dev std_dev];
      elseif (numel (std_dev) != 3)
        error ("smooth3: STD_DEV of the Gaussian convolution kernel must be scalar or 3-element vector");
      endif
      gx = (-(sz(2)-1)/2:(sz(2)-1)/2) / std_dev(2);
      gy = (-(sz(1)-1)/2:(sz(1)-1)/2) / std_dev(1);
      gz = (-(sz(3)-1)/2:(sz(3)-1)/2) / std_dev(3);
      [xx, yy, zz] = meshgrid (gx, gy, gz);
      k = exp (-(xx.^2 + yy.^2 + zz.^2) / 2);
      k = reshape (k, sz);
      k /= sum (k(:));
    otherwise
      error ("smooth3: invalid METHOD '%s'", method);
  endswitch
  if (! isfloat (data)) data = double (data); endif
  if (all (sz == 1))
    sd = data;
    return;
  endif
  h = (sz - 1) / 2;
  [r, c, p] = size (data);
  ir = [ones(1, h(1)), 1:r, r*ones(1, h(1))];
  ic = [ones(1, h(2)), 1:c, c*ones(1, h(2))];
  ip = [ones(1, h(3)), 1:p, p*ones(1, h(3))];
  sd = convn (data(ir, ic, ip), k, "valid");
  if (isa (data, "single")) sd = single (sd); endif
endfunction
