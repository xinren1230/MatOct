# MatOct
A MATLAB-style IDE built on a C# rewrite of GNU Octave.

**License: GPL-3.0-or-later** (see LICENSE). MatOct is a port of GNU Octave and is distributed under the same GPL-3.0 terms.

- IDE: `MatOct.exe` (editor, command window, workspace, debugger, figures, Current Folder browser, Set Path dialog)
- Console: `matoct` / `matrw` (same program; `matrw` kept as an alias)
- Search path: `addpath`/`rmpath`/`path`/`genpath`/`savepath`/`restoredefaultpath`/`userpath`/`which`/`rehash`; saved to `%APPDATA%\MatOct\pathdef.m` and loaded at startup (see docs/phase12.md)

Internal project and namespace names (`MatRewrite.*`) are unchanged.

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
