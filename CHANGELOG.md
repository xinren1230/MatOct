# Changelog

All notable changes to MatOct. Detailed notes (Chinese) live in `docs/phaseN.md`.

## [0.23.0] - 2026-11-03

MatOct is a MATLAB-style IDE for GNU Octave, written in C#.

### Changed
- Octave core test suite: 10253 / 10397 blocks pass (98.6 %); with the Octave script library on the path: 10141 / 10400 (97.5 %).
- `funm` now runs Octave's own m-file implementation.
- `var` / `std` accept sparse input; `sumsq` keeps sparsity.

### Fixed
- Exact int64/uint64 arithmetic, printing and literal handling.
- Faithful ports of `ichol`, `ilu` (ilu0 / crout / ilutp, real and complex) and complex sparse arithmetic.
- New `airy` (all kinds, scaling option), `tmpfile`, `__blas_version__`, `__lapack_version__`.
- `complex(z)` keeps the imaginary part and preserves single precision; `log` handles signed zero (fixes `cosint`).
- `switch` matches function handles in case labels.
- `schur(A, "complex")` returns a correct factorisation for real, defective matrices.
- `fopen` / `is_valid_file_id` validate file ids like Octave.

## [0.22.0] - 2026-11-03

MatOct is a MATLAB-style IDE for GNU Octave, written in C#.

### Changed
- Octave test suite: 10159/10354 core (98.1 %), 10096/10400 with Octave's m-file library (97.1 %).
- `onCleanup` is now a builtin whose task runs when the variable is cleared or goes out of scope.
- `fill` uses Octave's implementation (respects `newplot`/hold).

### Fixed
- `hgsave`/`hgload`/`openfig` read and write Octave-compatible `.ofig` text files.
- `ishold` no longer creates axes and accepts figure/axes handles; `clf(h)` and `clf reset`.
- Axes `colororder` and `linestyleorder` are honored by `plot`.
- `patch` accepts long color names; `currentpoint` is settable; `print_usage` message punctuation.

## [0.21.0] - 2026-11-03

MatOct is a MATLAB-style IDE for GNU Octave, written in C#.

### Changed
- Octave test suite: 10147/10354 core blocks pass (98.0%, up from 97.1%); 10090/10400 with Octave's own library (97.0%).
- `colormap` returns the stored map unchanged (e.g. `gray(16)` stays 16x3).

### Fixed
- subplot "position" mode and Octave's default subplot positions; overlapping axes are deleted properly.
- `cla` with axes handle and "reset"; `isfigure` element-wise; `colororderindex`; mesh surface default properties.
- Log axes with negative data (limits and ticks); legend titles; closerequestfcn handling.
- `isobject`/`isprop`/`__fieldnames__` for containers.Map; `structfun`, `gammaln`, `sprand`, `logical` on complex, `isindex` with Inf.
- `double` of a cell array raises "invalid conversion"; lexer escapes `\v` and `\f`.

## [0.20.0] - 2026-11-03

MatOct is a MATLAB-style IDE for GNU Octave, written in C#.

### Changed
- Octave test suite: core 97.1 % (10054/10354), with-lib 96.3 % (10013/10400).
- NA is now a distinct value (isna, printf/num2str print "NA"); interp1/interp2 extrapolate to NA by default and dlmread reads NA and supports "emptyvalue".
- warning("error", ID) turns that warning into an error, and warning(..., "local") restores the state when the function returns.

### Fixed
- Iterative solvers (pcg, bicg, gmres, tfqmr) report flag 2 for a singular preconditioner.
- bitand/bitor/bitxor/bitshift keep the integer class; bitcmp on integers.
- sort, nth_element, cummax/cummin, flip and ipermute keep sparse results (median of sparse input).
- cat of empty arrays along higher dimensions (iqr of empty N-D input).
- min/max "omitnan"/"includenan"/"all"; range and bounds; struct2cell on struct arrays; structfun; merge with cell arguments; regexp "matches".
- fread SIZE with a precision string (fileread, importdata); textread.
- Axes ticklabelrotation properties; axis(hax, ...); ";label;" plot format strings; legend "string"; contour levellist/levelstep.
- lasterr second output; switch on numeric arrays; toeplitz validation; meansq keeps single.
- is_same_file and dir_encoding; fopen can read bundled function files (test, example, type).

## [0.19.0] - 2026-11-03

MatOct is a MATLAB-style IDE for GNU Octave, written in C#.

### Changed
- Octave test suite: core 96.5 % (9988/10354), with-lib 95.7 % (9953/10400).

### Fixed
- Ranges clamp the last element to the stop value; linspace/logspace give exact endpoints, accept vector and complex limits, and keep the single class.
- regexprep replacement escapes follow Octave ($N, \\$, \\n, \\t).
- Matrix multiply propagates NaN*0.
- NaN/Inf/pi/eps accept "single" and "like".
- chol supports the [R, p] output and the "lower" option.
- lookup and the bit functions handle the single class.
- rose bin outputs; trisurf color validation; isprop on handle arrays; hgload validation.

## [0.18.0] - 2026-11-03
MatOct is a MATLAB-style IDE for GNU Octave, written in C#.
### Changed
- Octave test-suite pass rate out of the box: **94.6 % → 95.7 %** (9911/10354 blocks); with `--lib`: 94.2 % → 95.0 % (9877/10400).
- More Octave m-files used by default (`getfield`, `setfield`, `isfile`, `isfolder`); new core `subsref`/`subsasgn`.
### Fixed
- Octave-compatible argument validation and error messages for `colorbar`, `camlight`, `annotation`, `stem`, `patch`, `saveas`, `savefig`, `hgsave`, `figure`, `campos`/`camtarget`/`camup`/`camva`, `axis`, `isprop`, `fullfile`, `computer`, `pathdef`, `gco`, `delete`, `dir`, `help`, `which`.
- `fullfile` broadcasts over cell-string arguments; `strfind` accepts a cell PATTERN; `unlink` returns `[err, msg]`.
- New: `stat`, `system` (status/output), `S_ISDIR` & friends, `erfinv`, `erfcinv`, `roundb`, `__methods__`, `isjava`, `list_in_columns`.

## [0.17.0] - 2026-11-02
MatOct is a MATLAB-style IDE for GNU Octave, written in C#.
### Changed
- Octave test-suite pass rate out of the box: **93.7 % → 94.6 %** (9795/10354 blocks); with `--lib`: 93.2 % → 94.2 % (9794/10400).
- Octave-faithful `optimset`/`optimget` (option list, case-insensitive prefix matching, ambiguity errors, `optimset(fname)` defaults); more Octave m-files used by default (`int2str`, `condest`, `methods`, `hanning`, `spy`, `center`, `kurtosis`, `skewness`, `bin2dec`, `hex2dec`). Note: `hanning` now follows current Octave (`hanning(3)` = `[0 1 0]`).
### Fixed
- New `isprime` builtin (Miller-Rabin, Gaussian primes, validation); `sort` along any dimension of N-d arrays (fixes N-d `median`).
- `repmat`/`resize` on cell and struct arrays; `{a{:}, b{:}}` with empty cs-lists; char ranges (`'a':'e'`).
- `stairs` axes-handle argument and validation; `sprintf('%s', 65)` gives `A`.
- New `getpwuid`, `getuid`/`geteuid`/`getgid`/`getegid`, `is_sq_string`/`is_dq_string`, `__keywords__`, `__operators__`.

## [0.16.0] - 2026-11-02
MatOct is a MATLAB-style IDE for GNU Octave, written in C#.
### Changed
- Octave test-suite pass rate out of the box: **92.6 % → 93.7 %** (9707/10356 blocks); with `--lib`: 93.2 %.
- `bar`/`barh` now create Octave-style hggroup bar series (patch children, baseline, `barwidth`/`basevalue`/`horizontal`/stacked props); stacked bars really stack, grouped bars use Octave's geometry, integer xticks 1..N.
- New `xline`/`yline` (hggroup + line, `value`/`label`), rewritten `polar` (Octave validation, `rtick`/`ttick`).
### Fixed
- `findobj`/`findall`: level-order results, `-and` > `-or` > `-xor` precedence, struct values, empty results, HLIST validation; `subplot(…, prop, value)`.
- `text`: no-argument / property-only calls, cellstr and multi-row strings, one string per object.
- `errorbar` and `funm` input validation with Octave messages; `stemleaf`.
- Char vertical concatenation pads unequal rows; ranges keep `-0` at the limit.

## [0.15.0] - 2026-11-02
MatOct is a MATLAB-style IDE for GNU Octave, written in C#.
### Changed
- Octave test-suite pass rate out of the box: **90.1 % → 92.6 %** (9590/10356 blocks); with `--lib`: 92.8 %.
- More functions use Octave's bundled m-file (`strread`, `iqr`, `mad`, `strsplit`, `accumarray`, …); new `__accumarray_sum__/max/min__` builtins.
### Fixed
- `sum`: `"extra"` (compensated) mode, `omitnan`/`includenan` flags, empty results with explicit DIM.
- `strncmp`/`strncmpi` with strings of different length (previously always false).
- `zeros`/`ones(…, "like", p)`; `num2cell(A, dims)` for N-d; `isrow`/`iscolumn` on N-d arrays.
- `[~, q] = f()` usable as a call argument; `word ...` line continuation; `==`/`!=` on function handles; `||` on sparse scalars.
- `vecnorm` with p=Inf on empty input; `flintmax(class)`; N-d empty concatenation (crash in `iqr`).
- `bar`/`barh`/`text` input validation and Octave error messages.
- Test runner: crashed files are retried serially with a longer timeout.

## [0.14.0] - 2026-11-02
MatOct is a MATLAB-style IDE for GNU Octave, written in C#.
### Changed
- Octave test-suite pass rate out of the box: **68.1 % → 90.1 %** (9169/10172 blocks); with `--lib`: 90.5 %.
- GNU Octave's GPL m-file library (979 functions, plus the `+matlab` package) is bundled unmodified as embedded resources. MatOct builtins still take precedence, except for a tuned list where Octave's m-file has the stricter validation. Graphics stays on MatOct's native renderer.
### Fixed
- classdef files load from embedded sources (e.g. `inputParser`); package functions (`matlab.lang.makeValidName`, …) resolve.
- `matlab.lang.makeValidName` options (`ReplacementStyle`, `Prefix`); `base64_encode`/`base64_decode`.
- `isequal`: argument validation, N-d size comparison, function handles.
- `colormap`: input validation and Octave error messages, `colormap(hax, …)`.

## [0.13.1] - 2026-11-02
MatOct is a MATLAB-style IDE/interface for GNU Octave, written in C# (MATLAB 的平替，给 Octave 做的 MATLAB 风格界面).
### Added
- `figstyle` presets `nature`, `nature2`, `science`, `science2`, `default`, `classic` (journal column widths, font sizes, line widths); `groot` and `set(groot,'Default…')` defaults.
- `panellabel` (bold a/b/c panel letters).
- `exportgraphics`/`print`/`saveas` with physical `Width`/`Height`/`Units` and `Resolution` (`-rN`); vector PDF (embedded fonts) and SVG.
- `parula` colormap; `MarkerIndices` and more line properties.
### Changed
- New default publication look: Arial, no box, outward ticks, Okabe-Ito colour order, parula, frameless legend. Use `figstyle('classic')` for the old look.
- `colormap` with no arguments returns the colormap matrix.
- README gallery regenerated in Nature style; pie titles fixed.

## [0.13.0] - 2026-11-02
### Added
- MATLAB-style **figure editor**: Edit Plot mode, live **Property Inspector**, point dragging and data table for XData/YData, context menus, Insert menu (title/labels/legend/colorbar/text/arrow/line/rectangle), datatips, zoom, pan.
- `savefig`/`hgsave`, `openfig`/`hgload` (.fig as MatOct JSON), `makemcode` / File ▸ Generate Code.
- Vector export to PDF/EPS/SVG; figure `Color` honoured on export.
- `legend(ax, handles, labels)`.
- `fft`, `ifft`, `fft2`, `ifft2`, `fminsearch`, `optimset`, `optimget`.
- table, categorical, datetime/duration, exact int64, string functions, JSON, missing-data helpers, tiledlayout, appdata, help/doc/lookfor.
- IDE: command history, completion, code sections, help browser.
### Changed
- Sparser, MATLAB-like automatic tick spacing.

## [0.12.0]
- MATLAB-style search path (`addpath`, `savepath`, `pathdef.m`, Set Path dialog, Current Folder browser); renamed to **MatOct**; GPL-3.0.

## [0.11.0] and earlier
- See `docs/phase5.md` … `docs/phase11.md`: interpreter, Octave display, ~1000 builtins, 3-D graphics, debugger, workspace/variable editor, installer.
