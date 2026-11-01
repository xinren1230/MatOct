# Phase 7 — 闭包编译器 + 核心内建扩充

## 结果
| 模式 | Phase 6 | Phase 7 |
|---|---|---|
| core-only（不加载 Octave m 文件） | 17.1% | **40.5%** |
| lib 模式（加载 m 文件） | 85.7% | **86.4%** |
| 单元测试 | — | 242/242 |

core-only 分区：elfun 0→100%，signal 6.9→73.3%，statistics 10.6→74.2%，set 15.0→70.1%，
strings 38.5→57.5%，general 30.1→53.2%，special-matrix 3.6→36.3%，linear-algebra 7.9→31.4%，testfun 31.2→44.9%。

## 新增
- 闭包编译器（AST → .NET 委托）
- 严格的 assert（检查维度、class 和 NaN/Inf 位置；可用 MATRW_ASSERT_STRICT=0 / MATRW_ASSERT_CLASS=0 关闭）
- C# 内建：Elfun、General、Stats、Strings（startsWith/endsWith/erase/strjust/regexptranslate/validatestring/untabify/compare_versions）、
  Matrices（toeplitz/hankel/vander/hilb/invhilb/pascal/rosser/wilkinson/compan/hadamard/repelem/ndgrid/rotdim/sortrows/perms）、
  Sets（unique/union/intersect/setdiff/setxor/ismember）、Mov（movfun/movslice/__parse_movargs__/movmean/median/min/max/sum/prod/std/var/mad，
  支持 shrink/discard/fill/数值/same/periodic 端点、omitnan/nanval、samplepoints、outdim）
- lib 模式下，回退的名字放进 MFileFirstDefaults，改为优先用 m 文件：normalize, quantile, cov, cart2*/sph2cart/pol2cart, sortrows, ndgrid, hadamard, compan, istriu, isbanded, rescale, blkdiag, corr, bitset

## 遗留问题
- time（datenum/datevec/datestr）、tensorprod、gallery、gammainc/betainc、interp2、uniquetol、ilu、funm 在 core-only 下仍未定义
- Linalg/Poly 组尚未启用（cholupdate/qrupdate、ppval/spline）
- union({},[]) 的形状、cell 输入的 sortrows、hadamard 的 12/20/28 阶、warning("query") 返回 struct
