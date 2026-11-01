## MatRewrite - GPL-3.0-or-later
## T = roty (t): 3x3 rotation matrix about the y axis by T degrees.
function T = roty (t)
  if (nargin < 1 || ! isscalar (t))
    print_usage ();
  endif
  t = t * pi / 180;
  c = cos (t);  s = sin (t);
  T = [c 0 s; 0 1 0; -s 0 c];
endfunction
