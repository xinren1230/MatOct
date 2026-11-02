function tf = ismissing (A, indicator)
  % ISMISSING  True for missing entries (NaN, NaT, '' in cellstr, <undefined> categorical).
  if nargin > 1
    if iscell (indicator), tf = false (size (A)); for k = 1:numel (indicator), tf = tf | ismissing (A, indicator{k}); end; return; end
    if ischar (indicator) && iscell (A), tf = strcmp (A, indicator); return; end
    tf = (A == indicator); if isfloat (A), tf = tf | isnan (A); end
    return;
  end
  if istable (A)
    v = table2cell (A); tf = false (size (v));
    for k = 1:numel (v), x = v{k}; tf(k) = (isnumeric (x) && isscalar (x) && isnan (x)) || (ischar (x) && isempty (x)); end
  elseif iscategorical (A) || isdatetime (A) || isduration (A)
    if iscategorical (A), tf = isundefined (A); elseif isdatetime (A), tf = isnat (A); else, tf = isnan (seconds (A)); end
  elseif iscell (A)
    tf = cellfun (@(x) ischar (x) && isempty (x), A);
  elseif ischar (A)
    tf = (A == ' ');
  elseif isfloat (A)
    tf = isnan (A);
  else
    tf = false (size (A));
  end
end
