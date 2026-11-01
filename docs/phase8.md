# Phase 8 — 日期/插值/Linalg/Poly/gallery，以及收尾 Phase 7 遗留问题

## 结果
| 模式 | Phase 7 | Phase 8 |
|---|---|---|
| core-only（不加载 Octave m 文件） | 40.5% | **51.8%**（5328 通过 / 4951 失败） |
| lib 模式（加载 m 文件） | 86.4% | **88.1%**（9036 通过 / 1215 失败） |
| 单元测试 | 242/242 | **288/288**（新增 Phase7Tests 20 项、Phase8Tests 27 项） |

core-only 分区：general 64.0%，special-matrix 100%，linear-algebra 55%。

Release 基准测试（demo/bench/bench.m）：scalar loop 2e6 用时 0.524 s，func calls 2e5 用时 0.179 s，indexed assign 2e5 用时 0.063 s，
matmul 300x20 用时 0.169 s，cell/sprintf 2e4 用时 0.718 s。

## 新增
- 日期：datenum、datevec、datestr、weekday、etime、addtodate、calendar、eomday、is_leap_year
- Linalg 组：qrupdate、qrinsert、qrdelete、cholupdate、cholinsert、choldelete 等；新增 orth
- Poly 组：polyfit、ppval、spline、pchip、mkpp/unmkpp
- 插值：修正了 interp1；新增 interp2（含复数 extrap）
- 新增完整的 gallery（未实现的矩阵名会报错）
- 新增原生 decic（Levenberg-Marquardt 求解）
- nthargout 支持向量 N 和 NTOT；[f,s]=format() 可用；odeset 的前缀匹配和报错信息与 Octave 一致
- sum 支持 "double"、"native"、"default" 和 "extra" 类型参数
- 新增 .gitattributes（eol=lf）并做了换行符规范化

## MFileFirst 调整（依据 ab.sh 的 A/B 结果）
- 移除：interp1、sortrows、hadamard、decic、odeset（这些已改用原生实现）
- 加入：rmse（42→56）、mape（46→49）、nthargout（4→8）、polyfit（26→27）、ishermitian、kurtosis
- interp2、spline、tensorprod、union 用内建实现更好，保持不变

## 遗留问题
- interp1.m:645/665，interp2.m:643（FIXME 测试）
- plot 属性校验尚未处理
- linear-algebra 里 funm、cross、normest1 等仍未通过
- mean.m（lib 模式）：Phase 7 时直接崩溃，现在 102 项中通过 73 项；剩余失败在空数组的维度，以及 int64 求和精度
- kurtosis 的 "too many inputs" 报错信息，isosurface 的 Y/Z 报错信息
