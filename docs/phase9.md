# Phase 9 — 图形属性校验、Linalg/色图/图像/杂项内建函数、Windows 打包

## 结果
| 模式 | Phase 8 | Phase 9 |
|---|---|---|
| core-only（不加载 Octave m 文件） | 51.8% | **58.7%**（6029 通过 / 4246 失败） |
| lib 模式（加载 m 文件） | 88.1% | **88.2%**（9040 通过 / 1211 失败） |
| 单元测试 | 288/288 | **318/318**（新增 Phase9Tests 30 项） |

core-only 分区：linear-algebra 55% → 84.0%，image 7.3% → 80.3%，plot 11.0% → 14.4%，signal 88.3%，general 65.0%，
strings 61.3%，optimization 22.1%，specfun 40.3%。

Release 基准测试（Windows，demo/bench/bench.m）：scalar loop 2e6 用时 0.50–0.64 s，func calls 2e5 用时 0.26 s，indexed assign 2e5 用时 0.068 s，
matmul 300x20 用时 0.12–0.14 s，cell/sprintf 2e4 用时 2.85 s（比 Phase 8 慢：`c{end+1}` 每次追加都会复制整个数组，增长是 O(n²)，见“遗留问题”）。

## 新增
- 图形属性校验与 Octave 一致：
  - radio 属性：`set: invalid value for radio property "xscale" (value = foo)`
  - bool 属性：`set: invalid value for bool property "visible" (value = maybe)`
  - double 属性：`set: invalid value for double property "linewidth"`
  - color 属性：`set: invalid value for color property "color" (value = purple)`
  - 只读属性：`set: "type" is read-only`
- 原生图形函数：axes（与 axes.m 语义一致：axes(h) 设为当前坐标轴，并支持 parent/容器参数）、sphere、stairs、stem、xline/yline、polar、quiver、rose、trisurf
- Linalg：sqrtm、logm、funm、cross、normest/normest1、condest、rref、linsolve、isdefinite、subspace、housh、planerot、vech、commutation_matrix、duplication_matrix、lscov；trace、cond 改为原生实现；norm(A, ±Inf) 已修正
- 色图：viridis、turbo、jet、hot、gray、bone、pink、cubehelix、colorcube 等 22 种；iscolormap、hsv2rgb、rgb2hsv
- 图像：im2double、rgb2gray、gray2ind、ind2rgb、ind2gray
- 杂项：fftshift/ifftshift、hanning/hamming/blackman、beta、bincoeff、strtrunc、rectint、bitcmp、base2dec、nargchk、inpolygon、fzero、fminbnd
- rand、randn、randi 支持 "single"/"double" 类型参数；测试运行器按块恢复 warning 状态
- CellValue.Resized 不再为保留的元素重新分配 Empty

## 打包（installer/）
- `installer/build-dist.ps1`：对 matrw 和 MatRewriteIde 执行 Release 版 self-contained win-x64 发布，生成便携 zip，并在找到 Inno Setup 时编译安装包
- `installer/MatRewrite.iss`：按用户安装（无需管理员权限），创建开始菜单快捷方式（IDE、控制台、卸载），注册 .m 文件关联（HKCU）
- `installer/install-portable.ps1` 与 `uninstall-portable.ps1`：便携版的安装和卸载脚本，同样会创建快捷方式和文件关联
- 产物（dist/，已加入 .gitignore）：MatRewrite-0.9.0-win-x64-setup.exe（103 MB）、MatRewrite-0.9.0-win-x64-portable.zip（145 MB）
- 已验证：静默安装 → 快捷方式和 .m 关联已注册 → 已安装的 IDE 能运行 demo（截图 dist/ide_demo_installed.png）→ 已安装的 matrw 能绘制 surf 图（截图 dist/ide_demo_figure.png）

## 遗留问题
- plot 目录仍然较低：isosurface/stream*/quiver3/surfnorm/smooth3 等未实现，部分输入校验缺失，多输出也不完整
- 稀疏矩阵入参被转为稠密，导致与稀疏相关的报错测试失败；funm/ordeig/condeig 仍有失败
- 索引赋值总是复制整个数组（没有写时复制或容量预留），因此 `x(end+1)=…` 追加是 O(n²)
