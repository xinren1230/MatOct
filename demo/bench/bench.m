% Simple interpreter benchmark (phase 5)
function bench()
  tic; s = 0; for k = 1:2000000, s = s + k * 2; end; t1 = toc;
  tic; s = 0; for k = 1:200000, s = s + bench_f(k); end; t2 = toc;
  tic; v = zeros(1, 200000); for k = 1:200000, v(k) = k^2; end; t3 = toc;
  tic; A = rand(300); for k = 1:20, B = A * A'; end; t4 = toc;
  tic; c = {}; for k = 1:20000, c{end+1} = sprintf('%d', k); end; t5 = toc;
  printf('scalar loop 2e6:   %.3f s\nfunc calls 2e5:    %.3f s\nindexed assign 2e5:%.3f s\nmatmul 300x20:     %.3f s\ncell/sprintf 2e4:  %.3f s\n', t1, t2, t3, t4, t5);
end
function y = bench_f(x)
  y = x + 1;
end
