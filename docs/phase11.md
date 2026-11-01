# 第十一阶段：分解补齐、归约 vecdim、积分族、几何、0.11.0 打包

## 完成项
1. **schur / qz / ordeig**：实数 Schur（拟三角形式）与复 Schur，`[AA,BB,Q,Z,V,W]=qz(A,B)` 广义 Schur，`ordeig` 从拟三角块读取特征值顺序。
2. **归约的 vecdim 与 'all'**：sum/prod/max/min/mean/any/all 等支持 `'all'` 与维度向量参数。
3. **积分与相关函数**：integral / integral2 / integral3 / quadgk / quadv 等，rng，del2，cplxpair，fplot，stem3。
4. **几何**（新增 Builtins/Phase11Geometry.cs）：N 维 Bowyer–Watson Delaunay、convhull/convhulln、tsearch/tsearchn、voronoi/voronoin、dsearchn、griddata/griddatan/griddata3、inpolygon、rectint、rotx/roty/rotz。geometry 目录 158/158 通过。测试运行器现在会执行 `%!testif HAVE_QHULL` 块。
5. **解析器**：修复了单元数组中的匿名函数（例如 `{@(x) x.^2, 3}`）。**null**：改用 CoreLib/null.m，linear-algebra 中 null 测试全部通过。**quiver**：当线型带标记时箭头线型为 "none"，并且 linewidth/color 会传给基点标记。**diff**：现在遵守 dim 参数，并支持 N 维、复数、single 输入。eye 支持类名参数；svd/qr/lu/chol/eig/schur/hess/inv/pinv/expm 对 single 输入返回 single。
6. **版本号**：`version()`、`OCTAVE_VERSION` 以及 IDE 标题里的版本号现在从程序集读取，不再硬编码为 0.1.0。

## 已知限制：int64 / uint64
整数类型的运算内部借助 double 实现。因此当 |x| > 2^53 时，int64/uint64 的算术会丢失精度。例如 `int64(9007199254740992)+int64(1)` 得到 9007199254740992，而 Octave 得到 ...993。2^53 以内的数值，以及饱和、取整规则均与 Octave 一致。要彻底修复，需要为 int64/uint64 增加独立的 long/ulong 存储与运算内核，计划放到后续阶段。

## 兼容性（Octave 自带测试）
| 模式 | 第十阶段 | 第十一阶段 |
|---|---|---|
| 仅核心内置 | 63.7%（6473 通过） | 67.8%（6840 通过） |
| 加载 Octave 库 | 89.2%（9091 通过） | 90.2%（9125 通过） |

单元测试：352 个全部通过（新增 Phase11Tests 共 19 个）。

## 打包 0.11.0
- 产物：`dist/MatRewrite-0.11.0-win-x64-setup.exe`、`dist/MatRewrite-0.11.0-win-x64-portable.zip`；0.9.0 的旧产物已删除。
- 已验证：卸载会移除 PATH 条目；重新安装及覆盖安装正常；图形窗口截图正常。
