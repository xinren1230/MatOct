# MATLAB-Rewrite

Goal: an open replacement for MATLAB (language + numeric core + plotting + IDE).

## Existing alternatives (reference / reuse)
- GNU Octave: most MATLAB-compatible syntax, free, GPL.
- Scilab: own syntax, Xcos (Simulink-like).
- Python (NumPy/SciPy/Matplotlib/Spyder/Jupyter): largest ecosystem.
- Julia: MATLAB-like syntax, JIT, fast.
- FreeMat / RunMat (Rust, MATLAB-syntax runtime, JIT).

## Proposed plan
1. Parser for .m syntax (lexer -> AST).
2. Interpreter on top of NumPy (matrix ops, indexing 1-based, end, colon).
3. Builtins: zeros/ones/size/plot/disp/fprintf/load/save.
4. Plotting via Matplotlib; REPL + simple IDE (workspace, editor, command window).

## Phase 1: Octave-compatible engine in C# (implemented)
MatRewrite.Core is a clean-room C# reimplementation of the GNU Octave language core:
lexer, parser (AST), tree-walking interpreter, Octave-style display, ~270 builtins
(math, arrays, linear algebra via MathNet.Numerics, strings/regexp, cells/structs,
printf family, eval/feval/try-catch), and .m function/script lookup on a search path.

    dotnet build MatRewrite.sln
    dotnet test MatRewrite.sln
    dotnet run --project src/MatRewrite.Repl                     # interactive >> prompt
    dotnet run --project src/MatRewrite.Repl -- examples/demo.m  # run a script
    dotnet run --project src/MatRewrite.Repl -- --eval "A=[1 2;3 4]; B=A*A'"

## License
Because this project is modeled on GNU Octave's language and behavior, it is
distributed under the GNU General Public License v3.0 (or later). See LICENSE.
GNU Octave is Copyright (C) the Octave Project Developers; no Octave source is copied here.
