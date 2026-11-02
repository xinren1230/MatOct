function [B, BG, BP] = groupcounts (A, groupvars)
  % GROUPCOUNTS  Number of elements in each group (vector, cellstr, categorical or table).
  if istable (A)
    if nargin < 2, error ('groupcounts: table input needs grouping variable names'); end
    if ischar (groupvars), groupvars = {groupvars}; end
    cols = cellfun (@(v) A.(v), groupvars, 'UniformOutput', false);
    [G, ids{1:numel(cols)}] = findgroups (cols{:});
    cnt = accumarray (G(:), 1); tot = sum (cnt);
    args = {};
    for k = 1:numel (groupvars), args = [args, {ids{k}}]; end
    B = table (args{:}, cnt, 100 * cnt / tot, 'VariableNames', [groupvars, {'GroupCount', 'Percent'}]);
    return;
  end
  [G, BG] = findgroups (A(:));
  B = accumarray (G(:), 1); BP = 100 * B / sum (B);
end
