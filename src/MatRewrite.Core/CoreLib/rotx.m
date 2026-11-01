## MatRewrite - GPL-3.0-or-later
## T = rotx (t): 3x3 rotation matrix about the x axis by T degrees.
function T = rotx (t)
  if (nargin < 1 || ! isscalar (t))
    print_usage ();
  endif
  t = t * pi / 180;
  c = cos (t);  s = sin (t);
  T = [1 0 0; 0 c -s; 0 s c];
endfunction
