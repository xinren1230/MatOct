function pos = __get_position__ (h, units)
  ## Return the position of graphics object H in UNITS ("pixels" supported).
  pos = get (h, "position");
  if (isfigure (h) || ! strcmpi (units, "pixels"))
    return;
  endif
  u = "pixels";
  try
    u = get (h, "units");
  end_try_catch
  if (strcmpi (u, "normalized"))
    ppos = __get_position__ (get (h, "parent"), "pixels");
    pos = pos .* [ppos(3:4), ppos(3:4)];
  endif
endfunction
