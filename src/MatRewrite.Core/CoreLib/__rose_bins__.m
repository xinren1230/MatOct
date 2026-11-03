function [th, r] = __rose_bins__ (varargin)
  ## Bin computation from Octave's rose.m
  nargin = numel (varargin);
  th = mod (varargin{1}, 2*pi);
  custom_bins = false;
  if (nargin == 1)
    bins = [9 : 18 : 360] / 180 * pi;
  else
    bins = varargin{2};
    if (isscalar (bins))
      bins = [180/bins : 360/bins : 360] / 180 * pi;
    else
      custom_bins = true;
      bins = mod (bins, 2*pi);
      bins = unique (bins);
      bins = bins(:).';
    endif
  endif
  binedge = bins(1:end-1) + diff (bins) / 2;
  if (! custom_bins)
    binedge = [0, binedge, 2*pi];
    counts = histc (th, binedge);
  else
    last_binedge = bins(end) + diff ([bins(end), 2*pi+bins(1)]) / 2;
    if (last_binedge >= 2*pi)
      binedge = [0, last_binedge - 2*pi, binedge, 2*pi];
    else
      binedge = [0, binedge, last_binedge, 2*pi];
    endif
    counts = histc (th, binedge);
  endif
  if (isrow (counts))
    counts = counts(:);
  endif
  counts(end,:) = [];
  if (custom_bins)
    counts(end,:) += counts(1,:);
    counts(1, :) = [];
  endif
  th = zeros (4 * rows (counts), 1);
  if (! custom_bins)
    binedge(end) = [];
  else
    binedge([1,end]) = [];
  endif
  th(2:4:end) = binedge;
  th(3:4:end) = circshift (binedge, -1);
  th(end-1) += 2*pi;
  r = zeros (4 * rows (counts), columns (counts));
  r(2:4:end, :) = counts;
  r(3:4:end, :) = counts;
endfunction
