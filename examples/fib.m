function r = fib(n)
  % FIB  n-th Fibonacci number (recursive), example user function file
  if n < 2
    r = n;
  else
    r = fib(n-1) + fib(n-2);
  end
end
