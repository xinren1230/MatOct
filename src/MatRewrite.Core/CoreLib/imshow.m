function h = imshow (I, varargin)
  % IMSHOW  Display an image (grayscale, indexed or truecolor).
  if nargin < 1, print_usage (); end
  if iscell (I) || ! (isnumeric (I) || islogical (I)), error ('imshow: IM must be an image'); end
  if ndims (I) > 3 || (ndims (I) == 3 && size (I, 3) != 3), error ('imshow: image must be MxN or MxNx3 matrix'); end
  if ndims (I) == 3 && isinteger (I) && ! isa (I, 'uint8') && ! isa (I, 'uint16'), error ('imshow: TrueColor image must be uint8, uint16, double, or single'); end
  if ischar (I), I = imread (I); end
  if isinteger (I), I = double (I) / double (intmax (class (I))); elseif islogical (I), I = double (I); end
  map = [];
  if ~isempty (varargin) && isnumeric (varargin{1}) && columns (varargin{1}) == 3, map = varargin{1}; end
  if ndims (I) == 3 && size (I, 3) == 3
    q = 31; R = round (min (max (I(:,:,1), 0), 1) * q); G = round (min (max (I(:,:,2), 0), 1) * q); B = round (min (max (I(:,:,3), 0), 1) * q);
    code = R * (q+1)^2 + G * (q+1) + B;
    [u, ~, j] = unique (code(:));
    cmap = [floor(u / (q+1)^2), mod(floor(u / (q+1)), q+1), mod(u, q+1)] / q;
    idx = reshape (j, size (code));
    hh = imagesc (idx, [1 max(numel (u), 2)]); colormap (cmap);
  elseif ~isempty (map)
    hh = imagesc (I, [1 rows(map)]); colormap (map);
  else
    rng = [0 1];
    if ~isempty (varargin) && isnumeric (varargin{1}) && numel (varargin{1}) == 2, rng = varargin{1}; end
    if ~isempty (varargin) && isnumeric (varargin{1}) && isempty (varargin{1}), rng = [min(I(:)) max(I(:))]; end
    hh = imagesc (I, rng); colormap (gray (256));
  end
  axis image; axis off;
  if nargout > 0, h = hh; end
end
