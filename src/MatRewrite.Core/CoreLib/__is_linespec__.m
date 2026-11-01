## MatRewrite CoreLib: true if S parses completely as a line spec.
function tf = __is_linespec__ (s)
  tf = false;
  if (! ischar (s) || isempty (s)) return; endif
  r = regexprep (s, "--|-\\.|-|:", "");
  r = regexprep (r, "[rgbcmykw+o*.xsd^v<>ph]", "");
  tf = isempty (r) && ! any (strcmpi (s, {"filled", "off", "on"}));
endfunction
