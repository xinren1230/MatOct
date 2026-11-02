// MatOct - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

/// <summary>Phase 13: handle-graphics set/get used by the figure Property Inspector, .fig round-trip, code generation, export.</summary>
public class FigureEditTests
{
    static string Run(string code)
    {
        var o = new StringWriter(); var it = new Interpreter(o);
        it.Graphics.Backend = new MatRewrite.Plotting.FileOnlyBackend();
        it.Eval(code); return o.ToString().Replace("\r\n", "\n").Trim();
    }
    static string Tmp(string ext) => Path.Combine(Path.GetTempPath(), "fe_" + Guid.NewGuid().ToString("N")[..8] + ext).Replace("'", "''");
    const string Fig = "f = figure('visible','off'); h = plot(1:5,[2 4 3 5 1]); ax = gca; ";

    [Theory]
    [InlineData("LineWidth", "3", "num2str(get(h,'LineWidth'))", "3")]
    [InlineData("LineStyle", "'--'", "get(h,'LineStyle')", "--")]
    [InlineData("Marker", "'s'", "get(h,'Marker')", "s")]
    [InlineData("MarkerSize", "9", "num2str(get(h,'MarkerSize'))", "9")]
    [InlineData("MarkerFaceColor", "[1 0 0]", "num2str(get(h,'MarkerFaceColor'))", "1  0  0")]
    [InlineData("MarkerEdgeColor", "[0 0 1]", "num2str(get(h,'MarkerEdgeColor'))", "0  0  1")]
    [InlineData("Color", "[0.5 0.25 0]", "num2str(get(h,'Color'))", "0.5        0.25           0")]
    [InlineData("DisplayName", "'abc'", "get(h,'DisplayName')", "abc")]
    [InlineData("YData", "[5 4 3 2 1]", "num2str(get(h,'YData'))", "5  4  3  2  1")]
    public void LineProps(string p, string v, string get, string expect)
        => Assert.Equal(expect, Run(Fig + $"set(h,'{p}',{v}); printf('%s', {get})"));

    [Theory]
    [InlineData("FontName", "'Times New Roman'", "get(ax,'FontName')", "Times New Roman")]
    [InlineData("FontSize", "14", "num2str(get(ax,'FontSize'))", "14")]
    [InlineData("FontWeight", "'bold'", "get(ax,'FontWeight')", "bold")]
    [InlineData("TickDir", "'out'", "get(ax,'TickDir')", "out")]
    [InlineData("XGrid", "'on'", "get(ax,'XGrid')", "on")]
    [InlineData("YScale", "'log'", "get(ax,'YScale')", "log")]
    [InlineData("XLim", "[0 10]", "num2str(get(ax,'XLim'))", "0  10")]
    [InlineData("XTick", "[1 3 5]", "num2str(get(ax,'XTick'))", "1  3  5")]
    [InlineData("Box", "'off'", "get(ax,'Box')", "off")]
    [InlineData("XColor", "[1 0 0]", "num2str(get(ax,'XColor'))", "1  0  0")]
    public void AxesProps(string p, string v, string get, string expect)
        => Assert.Equal(expect, Run(Fig + $"set(ax,'{p}',{v}); printf('%s', {get})"));

    [Fact]
    public void LabelProps() => Assert.Equal("16 bold", Run(Fig + "t = title('Hi','FontSize',16,'FontWeight','bold'); printf('%d %s', get(t,'FontSize'), get(t,'FontWeight'))"));

    [Fact]
    public void LegendWithHandles() => Assert.Equal("B", Run(Fig + "h2 = line(1:5,1:5); hold on; legend(ax, h2, {'B'}); printf('%s', get(h2,'DisplayName'))"));

    [Fact]
    public void SavefigOpenfigRoundTrip()
    {
        var f = Tmp(".fig");
        var r = Run(Fig + $"set(h,'LineWidth',2.5,'LineStyle',':','Marker','o'); set(ax,'TickDir','out'); title('RT'); savefig(f,'{f}'); close(f); " +
                    $"g = openfig('{f}'); hh = findobj(g,'type','line'); a2 = get(g,'CurrentAxes'); printf('%g %s %s %s %s', get(hh(1),'LineWidth'), get(hh(1),'LineStyle'), get(hh(1),'Marker'), get(a2,'TickDir'), get(get(a2,'Title'),'String'))");
        Assert.Equal("2.5 : o out RT", r);
    }

    [Fact]
    public void MakeMCode()
    {
        var f = Tmp(".m");
        Run(Fig + $"set(h,'LineWidth',4); xlabel('time'); makemcode(f,'{f}')");
        var code = File.ReadAllText(f.Replace("''", "'"));
        Assert.Contains("plot (", code); Assert.Contains("'LineWidth', 4", code); Assert.Contains("xlabel (", code);
    }

    [Theory]
    [InlineData(".svg", "<svg")]
    [InlineData(".pdf", "%PDF")]
    public void VectorExport(string ext, string magic)
    {
        var f = Tmp(ext);
        Run(Fig + $"exportgraphics(f,'{f}')");
        var p = f.Replace("''", "'");
        Assert.True(File.Exists(p));
        Assert.Contains(magic, File.ReadAllText(p)[..Math.Min(400, (int)new FileInfo(p).Length)]);
    }
}

public class FftTests
{
    static string Run(string code) { var o = new StringWriter(); var it = new Interpreter(o); it.Eval(code); return o.ToString().Replace("\r\n", "\n").Trim(); }
    [Fact] public void FftDc() => Assert.Equal("15", Run("x = fft([1 2 3 4 5]); printf('%g', real(x(1)))"));
    [Fact] public void FftRoundTrip() => Assert.Equal("1 2 3 4 5 6 7", Run("printf('%g ', round(real(ifft(fft(1:7)))*1e9)/1e9)"));
    [Fact] public void FftPad() => Assert.Equal("8 3", Run("printf('%d ', size(fft(eye(3),8)))"));
    [Fact] public void Fft2() => Assert.Equal("10", Run("x = fft2([1 2;3 4]); printf('%g', x(1,1))"));
    [Fact] public void Fminsearch() => Assert.Equal("2.000 -1.000", Run("x = fminsearch(@(x)(x(1)-2)^2+(x(2)+1)^2,[0 0]); printf('%.3f %.3f', x)"));
}
