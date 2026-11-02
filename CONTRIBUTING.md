# Contributing to MatOct / 参与贡献

Thanks for your interest! 欢迎贡献代码、报告问题或改进文档。

## Build / 构建
Requirements: .NET 9 SDK. The IDE and figure windows are WPF (Windows); the core, CLI and tests also build on Linux/macOS.

```powershell
git clone https://github.com/xinren1230/MatOct
cd MatOct
dotnet build MatRewrite.sln -c Release
dotnet test tests/MatRewrite.Tests -c Release
dotnet run --project src/MatRewrite.Ide          # IDE
dotnet run --project src/MatRewrite.Repl         # console >> prompt
powershell -ExecutionPolicy Bypass -File installer\build-dist.ps1   # installer + portable zip (needs Inno Setup 6)
```
On Linux add `-p:EnableWindowsTargeting=true` to compile the WPF projects.

## Compatibility suite / 兼容性测试
With a GNU Octave source checkout:
```
matrw --octave-tests <octave>/scripts --out compat.md -j 4            # core only
matrw --octave-tests <octave>/scripts --lib <octave>/scripts --out compat-lib.md -j 4
```
Please make sure a change does not reduce the pass count.

## Guidelines / 规范
- Keep behaviour compatible with MATLAB/Octave; error messages should match Octave's where tests check them.
- Add an xUnit test for every new builtin or fix (`tests/MatRewrite.Tests`).
- Do **not** copy GNU Octave C++ source. Octave `.m` files may be used under GPL-3.0 with attribution.
- One topic per pull request; describe what MATLAB behaviour you matched.

By contributing you agree that your contribution is licensed under GPL-3.0-or-later.
