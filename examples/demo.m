% demo.m - run with: matrw examples/demo.m
A = [1 2; 3 4];
B = A * A'
for k = 1:5
  printf('fib(%d) = %d\n', k, fib(k));
end
[m, s] = stats_demo([2 4 4 4 5 5 7 9])
names = {'alice', 'bob'};
for i = 1:numel(names)
  fprintf('hello %s\n', upper(names{i}));
end
