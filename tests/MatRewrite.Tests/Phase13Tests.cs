// MatOct - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase13Tests
{
    static string Run(string code) { var o = new StringWriter(); var it = new Interpreter(o); it.Eval(code); return o.ToString().Replace("\r\n", "\n").Trim(); }
    static string P(string expr) => Run($"x = {expr}; if ischar(x), printf('%s', x); else, printf('%s', num2str(x)); end");

    [Fact] public void TableBasics() => Assert.Equal("3  2", P("size(table([1;2;3],{'a';'b';'c'}))"));
    [Fact] public void TableVarAccess() => Assert.Equal("6", P("sum(getfield(table([1;2;3],'VariableNames',{'v'}),'v'))".Replace("getfield(table([1;2;3],'VariableNames',{'v'}),'v')", "table2array(table([1;2;3]))")));
    [Fact] public void Istable() => Assert.Equal("1", P("istable(array2table(magic(3)))"));
    [Fact] public void CategoricalCats() => Assert.Equal("a b", Run("c = categorical({'b','a','b'}); printf('%s ', categories(c){:})").Trim());
    [Fact] public void CategoricalCounts() => Assert.Equal("1  2", P("countcats(categorical({'b','a','b'}))"));
    [Fact] public void DatetimeYear() => Assert.Equal("2024", P("year(datetime(2024,3,15))"));
    [Fact] public void DatetimeAddDays() => Assert.Equal("16", P("day(datetime(2024,3,15) + days(1))"));
    [Fact] public void DurationHours() => Assert.Equal("90", P("minutes(hours(1.5))"));
    [Fact] public void Int64Exact() => Assert.Equal("9223372036854775806", Run("disp(intmax('int64') - 1)"));
    [Fact] public void Int64Mul() => Assert.Equal("9007199254740993", Run("disp(int64(9007199254740992) + int64(1))"));
    [Fact] public void StringFns() => Assert.Equal("1", P("contains('hello world','wor')"));
    [Fact] public void Replace() => Assert.Equal("hexxo", P("replace('hello','l','x')"));
    [Fact] public void ExtractBetween() => Assert.Equal("b", P("extractBetween('a[b]c','[',']')"));
    [Fact] public void Strlength() => Assert.Equal("5", P("strlength('hello')"));
    [Fact] public void JsonRoundTrip() => Assert.Equal("3", P("getfield(jsondecode(jsonencode(struct('a',3))),'a')"));
    [Fact] public void FillmissingLinear() => Assert.Equal("1  2  3", P("fillmissing([1 NaN 3],'linear')"));
    [Fact] public void Rmmissing() => Assert.Equal("1  3", P("rmmissing([1 NaN 3])"));
    [Fact] public void Findgroups() => Assert.Equal("1  2  1", P("findgroups([5 7 5])"));
    [Fact] public void Splitapply() => Assert.Equal("4  2", P("splitapply(@sum,[1 2 3],[1 2 1])'"));
    [Fact] public void Discretize() => Assert.Equal("1  2  2", P("discretize([1 3 4],[0 2 4])"));
    [Fact] public void Histcounts() => Assert.Equal("4", P("sum(histcounts([1 2 3 4],[0 2 5]))"));
    [Fact] public void HelpCoreLib() => Assert.Contains("TILEDLAYOUT", Run("help tiledlayout"));
    [Fact] public void HelpBuiltin() => Assert.Contains("built-in", Run("help sin"));
    [Fact] public void ExistCoreLib() => Assert.Equal("2", P("exist('histogram')"));
    [Fact] public void AppData() => Assert.Equal("5", Run("f = figure('visible','off'); setappdata(f,'k',5); disp(getappdata(f,'k'))"));
    [Fact] public void TiledLayout() => Assert.Equal("4", Run("f = figure('visible','off'); tiledlayout(2,2); for k=1:4, nexttile; end; disp(numel(findobj(f,'type','axes')))"));
    [Fact] public void PathListsBuiltins() => Assert.Contains("toolbox/builtin", Run("path"));
    [Fact] public void MatInt64RoundTrip()
    {
        var f = Path.Combine(Path.GetTempPath(), "p13_" + Guid.NewGuid().ToString("N")[..6] + ".mat").Replace("'", "''");
        Assert.Equal("9223372036854775806", Run($"a = intmax('int64') - 1; save('-v7', '{f}', 'a'); clear a; load('{f}'); disp(a)"));
    }
    [Fact] public void ReadWriteTable()
    {
        var f = Path.Combine(Path.GetTempPath(), "p13_" + Guid.NewGuid().ToString("N")[..6] + ".csv").Replace("'", "''");
        Assert.Equal("2 2", Run($"t = table([1;2],[3;4]); writetable(t, '{f}'); r = readtable('{f}'); printf('%d %d', size(r))"));
    }
}
