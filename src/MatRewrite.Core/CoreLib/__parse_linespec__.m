## MatRewrite CoreLib: split a line spec into style, colour and marker.
function [ls, c, mk] = __parse_linespec__ (s)
  ls = ""; c = ""; mk = "";
  t = regexp (s, "--|-\\.|-|:", "match", "once");
  if (! isempty (t)) ls = t; s = strrep (s, t, ""); endif
  for ch = s
    if (any (ch == "rgbcmykw") && isempty (c))
      c = ch;
    elseif (any (ch == "+o*.xsd^v<>ph"))
      mk = ch;
    endif
  endfor
endfunction
