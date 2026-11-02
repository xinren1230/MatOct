# Changelog

All notable changes to MatOct. Detailed notes (Chinese) live in `docs/phaseN.md`.

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
