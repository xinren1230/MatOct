# 第十二阶段：MATLAB 风格搜索路径 + 更名 MatOct（0.12.0）

## 搜索路径模型
- 解析顺序：当前文件夹 → SearchPath（用户路径）→ 内置 CoreLib。支持 private/、+pkg、@class。
- 持久化：`%APPDATA%\MatOct\pathdef.m`（MATLAB `p = [ '...;', ...];` 格式），可用 `MATRW_CONFIG_DIR` 覆盖；`MATRW_NO_PATHDEF=1` 或 `--no-pathdef` 跳过加载。CLI 与 IDE 启动时都会加载，已不存在的目录自动剔除。
- userpath 默认 `Documents\MatOct`，存于 userpath.txt。

## 内置函数
addpath（-begin/-end、多目录、pathsep 字符串、cell）、rmpath、path、genpath（跳过 private/@/+/点目录/resources）、pathsep、savepath、restoredefaultpath、pathdef、userpath、rehash、which（-all、遮蔽提示）、exist、what、cd/chdir、pathtool。路径变化时清除未命中缓存。

## IDE
- 「设置路径」对话框：Add Folder、Add with Subfolders、Move Top/Up/Down/Bottom、Remove、Save、Close、Revert、Default；更改即时生效，关闭时若未保存会询问。
- 工具栏当前文件夹框 + ↑ + Browse，左侧「Current Folder」浏览器与 cd 同步，右键 Add to Path / 含子文件夹 / Remove from Path。

## 更名
面向用户部分更名为 **MatOct**：IDE 标题与 MatOct.exe、安装程序（新 AppId，安装到 `%LOCALAPPDATA%\Programs\MatOct`）、产物名、README（GPL-3.0）。CLI 保留 matrw.exe，并新增别名 matoct.exe。内部命名空间/项目仍为 MatRewrite.*。

## 测试
- xUnit：371/371 通过（新增 Phase12Tests 19 个）。
- Octave 兼容（core-only）：6889/10140 = 67.9%（上期 6840，67.8%）。
- Octave 兼容（lib）：9200/10222 = 90.0%（上期 9125；用例总数增加，通过数无回退）。

## 产物
- dist\MatOct-0.12.0-win-x64-setup.exe、dist\MatOct-0.12.0-win-x64-portable.zip
- 已验证：matrw addpath+savepath 后，新进程 matoct 中 which/调用成功。
- 截图：docs/img/ide_012_setpath.png

## 已知问题
- 默认路径为空（CoreLib 内嵌），故 `path` 只显示用户目录。
- 旧版 MatRewrite 安装与新 MatOct 使用不同 AppId，需要手动卸载旧版（本机已卸载）。
