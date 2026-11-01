## MatRewrite - GPL-3.0-or-later
## T = rotz (t): 3x3 rotation matrix about the z axis by T degrees.
function T = rotz (t)
  if (nargin < 1 || ! isscalar (t))
    print_usage ();
  endif
  t = t * pi / 180;
  c = cos (t);  s = sin (t);
  T = [c -s 0; s c 0; 0 0 1];
endfunction
