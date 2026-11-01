## MatRewrite CoreLib: pick a representative iso value from a histogram.
function iso = __calc_isovalue_from_data__ (data)
  data = double (data(:));
  if (numel (data) > 20000)
    data = data(1:floor (numel (data) / 10000):end);
  endif
  nb = 100;
  [cnt, ctr] = hist (data, nb);
  if (any (cnt(1:2) > 10 * numel (data) / nb))
    cnt(1:2) = []; ctr(1:2) = [];
  endif
  drop = find (cnt < max (cnt) / 50);
  if (numel (drop) < 0.9 * nb)
    ctr(drop) = [];
  endif
  iso = ctr(max (1, floor (numel (ctr) / 2)));
endfunction
