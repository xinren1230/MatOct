function B = fillmissing (A, method, varargin)
  % FILLMISSING  Fill missing (NaN) entries: 'constant',v | 'previous' | 'next' | 'nearest' | 'linear' | 'movmean',w.
  B = A;
  if isrow (A), B = fillmissing (A(:), method, varargin{:}).'; return; end
  for c = 1:size (A, 2)
    x = A(:, c); m = isnan (x); if ~any (m), continue; end
    idx = (1:numel (x))'; good = find (~m);
    switch lower (method)
      case 'constant', v = varargin{1}; if numel (v) > 1, v = v(c); end; x(m) = v;
      case 'previous', for k = 2:numel (x), if m(k), x(k) = x(k-1); end; end
      case 'next', for k = numel (x)-1:-1:1, if m(k), x(k) = x(k+1); end; end
      case 'nearest', if ~isempty (good), x(m) = interp1 (good, x(good), idx(m), 'nearest', 'extrap'); end
      case {'linear', 'spline', 'pchip'}
        if numel (good) >= 2, x(m) = interp1 (good, x(good), idx(m), lower (method), 'extrap'); end
      case 'movmean'
        w = varargin{1}; h = floor (w / 2);
        for k = find (m)', s = x(max (1, k-h):min (end, k+h)); s = s(~isnan (s)); if ~isempty (s), x(k) = mean (s); end; end
      otherwise, error ('fillmissing: unknown method ''%s''', method);
    end
    B(:, c) = x;
  end
end
