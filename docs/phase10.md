# 第十阶段：稀疏原生化、绘图第二轮、遗留补齐、0.10.0 打包

## 兼容性结果（Octave 自带测试块）

| 模式 | 第九阶段 (p9f) | 第十阶段 (p10f) |
|---|---:|---:|
| core（只用内置 + CoreLib，不加载 Octave .m） | 58.7%（6029 通过） | **63.7%**（6473/10160） |
| lib（加载 Octave scripts 目录 .m） | 88.2%（9040 通过） | **89.2%**（9091/10187） |

core 模式各区：plot 14.4% → 38.3%，sparse 24.1% → 78.8%，linear-algebra 84.0% → 88.8%，
statistics 74.3% → 78.1%，general 65.0% → 66.8%。完整报告见 `compat-core.md`、`compat-octave-lib.md`。

## CoreLib 方案说明（如实记录）

core 模式下不少函数并非 C# 原生实现，而是**我们自己编写的 .m 文件**，以嵌入资源形式放在
`src/MatRewrite.Core/CoreLib`，按需加载；计算重的部分（等值面 marching cubes、流线 Euler 积分
`__streameuler__`、稀疏核心）是 C# 原生内核。这些 .m 文件参考了 Octave 的行为和错误信息，
但不是从 Octave 拷贝的源文件。内置函数优先于 CoreLib，所以若要改由 .m 实现，需要在
`RegisterPhase10Plot()` 中移除同名原生内置（本阶段对 quiver、expm、null、condeig、ordeig 这样做）。

本阶段新增 CoreLib：stream2/stream3/streamline/streamtube、divergence、isocaps、isocolors、interpn、
gradient、isaxes、ancestor、sombrero、ellipsoid、null、expm（Padé + 缩放平方，支持复数）、condeig、
ordeig、corrcov、ranks、runlength、run_count、statistics；改进 `__quiv__`、smooth3、isonormals。

## 解释器 / 内核修复
- 测试运行器：每个块前快照共享变量，error/warning 块和失败块后恢复（与 Octave 一致）。
- 索引结果虚部全为 0 时收窄为实数。
- `[s.name] = deal(...)` 等结构数组 cs-list 赋值。
- `eig` 重写：非对称实矩阵的复特征向量（以前只取实部，错误）、复矩阵输入、广义 `eig(A,B)`
  （以前忽略 B）、特征向量单位化、`[v,d]=eig([])`。

## 打包 0.10.0
- exe（IDE 与 matrw）和安装程序带图标（`installer/app.ico`）。
- 安装时把 `{app}\cli` 加入用户 PATH，卸载时移除。
- 产物：`dist/MatRewrite-0.10.0-win-x64-setup.exe`、`dist/MatRewrite-0.10.0-win-x64-portable.zip`。

## 已知差距
- `schur` 对实矩阵返回复 Schur 形而不是实拟三角形，导致 ordeig 部分测试失败；`qz` 未实现。
- int 64 位整数内部以 double 存储，超过 2^53 精度丢失（根本性限制）。
- `min/max` 不支持 vecdim，使 statistics 的 vecdim 测试失败。
- null 的 single 空输入类型、`null(uint8([]))` 报错；null.m 中单元数组内匿名函数解析问题。
- quiver 箭头 xdata 格式（3 个测试）。
- 未实现：integral/quadgk/integral2/3、quad2d、rng、del2、cplxpair、rat、accumdim、isuniform、
  discrete_*/empirical_*、ols、interpft、reducevolume/reducepatch、fplot、stem3、tfqmr/qmr/pcr、treeplot 等。
- geometry 区仍只有 15%（delaunay/voronoi/convhull 系列）。

## 第十一阶段建议
1. 实 Schur（Hessenberg + 双移位 QR）与 qz，修 ordeig；min/max vecdim。
2. 数值积分族（integral、quadgk、integral2/3）与 rng。
3. 计算几何（delaunay、convhulln、voronoi），提升 geometry。
4. 真正的 int64 存储。
5. 解析器：单元数组行中的匿名函数。
