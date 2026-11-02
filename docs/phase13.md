# 第十三阶段：MATLAB 风格图窗编辑器 + 数据类型扩展（0.13.0）

## 图窗编辑器 / Figure editor（新）
- **Edit Plot 模式**：单击选中线条、坐标区、标题/轴标签、图例、文本、柱状图；拖动数据点直接改 XData/YData（Shift 仅垂直）；双击打开属性检查器；Delete 删除。
- **属性检查器（Property Inspector）**：对象下拉选择（图窗/坐标区/标签/图例/各数据系列）；常用属性分组 + "All properties" 全部属性；枚举用下拉框、颜色用色块 + 取色器（调色板/RGB 滑块/十六进制）、数值框回车/失焦提交、滚轮微调；所有编辑**实时**经 `set` 生效，代码中 `set/get` 的修改也会回显。
- 线条：Color、LineStyle、LineWidth、Marker、MarkerSize、MarkerEdgeColor、MarkerFaceColor、DisplayName、XData/YData（另有数据表格编辑器）。
- 坐标区：FontName/FontSize/FontWeight/FontAngle、XLim/YLim、XScale/YScale、XTick/YTick/TickLabel/Rotation、TickDir/TickLength、Box、Grid/MinorGrid、GridColor/GridLineStyle、XColor/YColor/Color。
- 菜单：File（Open Figure、Save Figure As、导出 PNG/SVG/PDF、Generate Code、Copy）、Edit、View、Insert（标题、轴标签、图例、颜色栏、文本、箭头、直线、文本框、矩形）、Tools；工具栏：编辑/放大/缩小/平移/数据提示/重置视图。
- 右键菜单：线条（颜色、线型、线宽、标记）、坐标区（网格、Box、刻度方向、对数轴）、文本/标签、图例。
- 数据提示（datatip）、缩放、平移。

## 新函数 / 改进
- `savefig` / `hgsave`：保存 .fig（MatOct JSON 格式 "MatOct-fig" v1）；`openfig` / `hgload`：打开并重建图窗。
- `makemcode(fig, file.m)`：生成可重建图窗的 .m 函数（同 GUI 的 Generate Code）。
- `exportgraphics` / `print` / `saveas`：.pdf/.eps 与 .svg 为**矢量**输出，并支持图窗背景色。
- `legend(ax, handles, labels)` 接受坐标区与句柄向量。
- 新增 `fft`/`ifft`/`fft2`/`ifft2`（任意长度、补零/截断、DIM、`symmetric`）、`fminsearch`（Nelder-Mead）、`optimset`/`optimget`。
- 默认刻度密度改为接近 MATLAB（更稀疏）。
- 早先（本阶段）：table/categorical/datetime/duration、int64 精确运算、字符串函数（contains/replace/extractBetween/strlength…）、jsonencode/jsondecode、fillmissing/rmmissing/findgroups/splitapply/discretize/histcounts、tiledlayout/nexttile、setappdata 系列、help/doc/lookfor、IDE 命令历史/补全/代码节/帮助浏览器。

## 测试
- xUnit：430/430 通过（新增 FigureEditTests 25 个、FftTests 5 个：线/坐标区/标签属性 set/get、带句柄 legend、savefig→openfig 往返、makemcode、SVG/PDF 导出）。
- Octave 兼容（core-only）：见 README（68.1%，6938/10186）；（lib）90.0%（9201/10218）。

## 截图
- docs/img/inspector-line.png：属性检查器编辑线条（颜色/线宽/线型/标记）。
- docs/img/example-*.png：由 MatOct 自身 `exportgraphics` 生成（docs/examples/gallery.m）。

## 已知问题
- 图例的点击区域为估算值（ScottPlot 未提供图例矩形 API）。
- `title(legendHandle, ...)` 尚不支持。
- .fig 为 MatOct 自有 JSON 格式，不能读取 MATLAB 二进制 .fig。

## 0.13.1：Nature/Science 出版风格
MatOct 定位：MATLAB 的平替，给 Octave 做的 MATLAB 风格界面（C# 编写）。
- `figstyle('nature'|'science'|'default'|'classic', 'Columns', 1|2)`：期刊栏宽（Nature 89/183 mm，Science 57/121 mm）、字号 7–9 pt、线宽、Okabe-Ito 配色；`groot` 默认值（`set(groot,'DefaultAxesFontSize',8)` 等）。
- 新默认外观：Arial、无边框、刻度朝外、无框图例、parula 色图；旧外观用 `figstyle('classic')`。
- `panellabel`：自动标注 a、b、c。
- `exportgraphics`/`print`：`Width`/`Height`/`Units`（mm/cm/in/pt）与 `Resolution`/`-rN`；PDF 嵌入字体、SVG 为矢量。
- `parula`、`MarkerIndices`；饼图标题修复；README 图库以 Nature 风格重新生成（docs/img/example-*.png）。
