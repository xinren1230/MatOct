## MatOct - GPL-3.0-or-later
function names = __optim_names__ ()
  ## Static list of option names known to optimset/optimget (Octave's __all_opts__).
  names = {"AutoScaling"; "ComplexEqn"; "Display"; "FinDiffType"; "FunValCheck"; ...
           "GradObj"; "Jacobian"; "MaxFunEvals"; "MaxIter"; "OutputFcn"; ...
           "TolFun"; "TolX"; "TypicalX"; "Updating"};
end
