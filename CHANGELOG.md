# Changelog

All notable changes to MatOct. Detailed notes (Chinese) live in `docs/phaseN.md`.

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
