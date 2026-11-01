# Phase 5: compatibility push and performance basics

## Pass-rate progression (Octave `%!` suite, 864 files)

| Step | Library loaded | Core only |
|---|---:|---:|
| Phase 4 baseline | 61.5% | 16.6% |
| Batch 1: parser fixes, package functions, about 50 new builtins | 67.0% | – |
| Batch 2: MFileFirst set, isna N-D fix, nth_element | 76.8% | – |
| Batch 3: `[] && x` treated as false, `figure` dropped from MFileFirst, filter, pow2 | **77.9%** | 16.6% |
| After the performance work (no regressions) | **77.9%** (8007 pass / 2274 fail / 489 skip) | 16.6% |

The core-only rate did not move. Almost every Octave `%!` test exercises the function defined in its own m-file, so without the library those functions are simply missing.

### Per area (library loaded)

| Area | Phase 4 | Phase 5 |
|---|---:|---:|
| +containers | 46.7% | 42.2% |
| +matlab | 2.1% | 52.1% |
| @ftp | 100.0% | 100.0% |
| audio | 94.2% | 98.6% |
| elfun | 97.6% | 92.8% |
| general | 64.5% | 76.4% |
| geometry | 93.7% | 93.7% |
| gui | 63.7% | 63.7% |
| help | 47.9% | 68.8% |
| image | 78.7% | 86.1% |
| io | 71.4% | 76.2% |
| java | 93.3% | 100.0% |
| legacy | 66.7% | 66.7% |
| linear-algebra | 77.2% | 79.4% |
| miscellaneous | 60.3% | 74.3% |
| ode | 66.9% | 66.2% |
| optimization | 56.6% | 57.2% |
| path | 42.9% | 57.1% |
| plot | 40.5% | 52.6% |
| polynomial | 61.0% | 75.7% |
| prefs | 75.0% | 75.0% |
| profiler | 90.9% | 90.9% |
| set | 68.5% | 96.9% |
| signal | 84.6% | 96.4% |
| sparse | 57.9% | 61.1% |
| specfun | 45.5% | 75.1% |
| special-matrix | 95.3% | 96.4% |
| statistics | 57.7% | 82.3% |
| strings | 60.3% | 82.2% |
| testfun | 46.7% | 89.0% |
| time | 37.1% | 95.2% |
| web | 55.6% | 100.0% |
## About MFileFirst

When the Octave m-file library is on the path, the interpreter prefers its own C# builtins (`PreferBuiltins`). For many functions, Octave's m-file has much more complete input validation and edge-case handling than our builtin. Most of the 439 "expected an error but none was raised" failures came from this.

`Runtime/MFileFirstDefaults.cs` lists the names for which the Octave m-file is used instead of our builtin. The list was tuned like this:

1. Find the 183 names that exist both as our builtins and as Octave m-files.
2. Run the suite with all of them switched to the m-file.
3. Keep only the names whose own test file had fewer failures.
4. Drop names that broke other files (`figure` needs the internal `__go_figure__`).

The list has 67 names, including var, mean, median, mode, interp1, unique, ismember, validateattributes, num2str, dec2base, cast and trapz.

**Note:** about 10 percentage points of the phase 5 gain come from running Octave's own m-files rather than from our C# implementations. Our interpreter runs those m-files correctly, which is a real measure of language compatibility. But for these functions it does not show that our C# builtins are complete. The core-only report (`compat-core.md`) is still 16.6%. The environment variable `MATRW_MFILE_FIRST` adds names, and a leading `=` replaces the list.

## Performance (`demo/bench/bench.m`, Debug build, Linux box)

| Benchmark | Before | After |
|---|---:|---:|
| Scalar loop, 2e6 iterations | 2.34 s | 2.46 s (noise) |
| User function calls, 2e5 | 0.53 s | 0.58 s (noise) |
| Indexed assign `v(k)=…`, 2e5 into a preallocated vector | **101.2 s** | **0.28 s** |
| Matrix multiply 300×300, ×20 | 0.26 s | 0.46 s (noise, BLAS-bound) |
| Cell growth `c{end+1}=sprintf(…)`, 2e4 | **11.9 s** | **1.09 s** |

The fix is an ownership flag (`ArrayValue.Exclusive`). It is set only when a variable holds a freshly created array that nothing else references. Any read that could create an alias clears it: a bare variable reference, passing the array as an argument, capturing it in a closure, returning it from a function, or storing it in a cell. While the flag is set, `v(i)=s`, `v(i,j)=s`, `c{i}=x` and `c{end+1}=x` modify the array in place instead of copying the whole array on every assignment. Tests in `Phase5Tests.cs` check that value semantics are preserved. Parse results were already cached by path and mtime (`fileCache`), so they need no further work.

## Remaining top failure categories (library loaded)

- About 410 assert value mismatches (numerical edge cases, display formatting).
- 109 expected errors that are not raised.
- 96 cellfun same-shape errors (gammainc, datenum, cart2pol and others).
- 69 "conversion to cell array" errors.
- 42 invalid assignment targets.
- Graphics: `get` on empty handles, the `__go_figure__` internals, legend and colorbar.
- `+containers/Map` is at 42%; the classdef corner cases there remain.
