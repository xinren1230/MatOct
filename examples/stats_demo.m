function [m, s] = stats_demo(x)
  % STATS_DEMO  return mean and std, demonstrates multiple outputs
  m = mean(x);
  if nargout > 1
    s = std(x);
  end
end
