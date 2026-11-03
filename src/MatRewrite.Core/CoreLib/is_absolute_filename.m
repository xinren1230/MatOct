function t = is_absolute_filename (n)
  t = ischar (n) && ! isempty (n) && (n(1) == "/" || n(1) == "\\" || (numel (n) >= 3 && isletter (n(1)) && n(2) == ":" && any (n(3) == "/\\")));
endfunction
