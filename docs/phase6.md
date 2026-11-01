# Phase 6

## Compat (Octave scripts test blocks)
- lib mode: 85.7% (8673 pass / 1444 fail / 489 skip, 864 files)
- core-only: 17.1% (was 16.6%)
- plot 79.3%, elfun 100%, +containers 97.8% (was 42.2%), ode 96.2% (was 66.2%)

## HG
- legend/colorbar rendered in PNG and WPF (shared FigureRenderer)
- Callbacks: ButtonDownFcn, CloseRequestFcn, SizeChangedFcn via FigureWindow.CallbackSink; pause drains queue

## AST closure compiler (Runtime/Compiler.cs), Release
| bench | before | tree | compiled |
|---|---:|---:|---:|
| scalar loop 2e6 | 1.1 s | 0.91 | 0.64 |
| func calls 2e5 | 0.34 | 0.32 | 0.31 |
| indexed assign 2e5 | 0.17 | 0.155 | 0.19 |
| matmul 300x20 | 0.31 | 0.30 | 0.32 |
| cell/sprintf 2e4 | 8.4 | 0.76 | 0.75 |
MATRW_NO_COMPILE=1 disables compiler.

## Gaps
cholupdate missing; Map.m:967 multi-output; odeset error case; plot 215 failures; core-only builtins coverage low.

## Phase 7 suggestions
Compile statements/indexing; more core builtins (linear algebra updates, strings); plot property validation; core-only coverage push.
