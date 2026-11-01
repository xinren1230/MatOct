## MatRewrite - GPL-3.0-or-later
function s = rng (varargin)
  if (nargin > 2)
    print_usage ();
  endif
  srng = struct ("Type", "twister", "Seed", "Not applicable", "State", {{rand("state"), randn("state")}});
  if (nargin == 0)
    s = srng;
    return;
  endif
  a = varargin{1};
  if (isscalar (a) && isnumeric (a) && isreal (a) && a >= 0)
    sr = a; sn = a; gen = __rng_gen__ (varargin(2:end));
  elseif (ischar (a) && strcmpi (a, "shuffle"))
    sr = sum (1e6 * clock ()) + 1e3 * rand (); sn = sr + 1; gen = __rng_gen__ (varargin(2:end));
  elseif (ischar (a) && strcmpi (a, "default") && nargin == 1)
    gen = "twister"; sr = 0; sn = 0;
  elseif (isstruct (a) && isscalar (a) && nargin == 1)
    if (numfields (a) != 3 || ! all (isfield (a, {"Type", "Seed", "State"})))
      error ('rng: input structure requires "Type", "Seed", "State" fields"');
    endif
    gen = a.Type;
    if (iscell (a.State))
      sr = a.State{1}; sn = a.State{end};
    else
      sr = a.State; sn = a.State;
    endif
  else
    print_usage ();
  endif
  if (isempty (gen))
    gen = "twister";
  endif
  switch (gen)
    case {"twister", "legacy"}
      rand ("state", sr); randn ("state", sn);
    case "v5uniform"
      rand ("state", sr);
    case "v5normal"
      randn ("state", sn);
    otherwise
      error ('rng: invalid GENERATOR: "%s"', gen);
  endswitch
  if (nargout > 0)
    s = srng;
  endif
endfunction

function gen = __rng_gen__ (val)
  if (isempty (val))
    gen = ""; return;
  elseif (! iscellstr (val))
    error ("rng: GENERATOR must be a string");
  endif
  gen = lower (char (val));
  if (any (strcmp (gen, {"simdtwister", "combrecursive", "philox", "threefry", "multfibonacci", "v4"})))
    error ('rng: random number generator "%s" is not available in Octave', gen);
  elseif (! any (strcmp (gen, {"twister", "v5uniform", "v5normal"})))
    error ('rng: unknown random number generator "%s"', gen);
  endif
endfunction
