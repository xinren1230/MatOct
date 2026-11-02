// MatOct - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

[Collection("FigStyle")]
public class Phase131Tests
{
    static (Interpreter it, StringWriter o) New()
    {
        var o = new StringWriter(); var it = new Interpreter(o);
        it.Graphics.Backend = new MatRewrite.Plotting.FileOnlyBackend();
        return (it, o);
    }
    static string Run(string code) { var (it, o) = New(); try { it.Eval(code); } finally { FigStyle.Current = FigStyle.Preset("default"); } return o.ToString().Replace("\r\n", "\n").Trim(); }
    static string Tmp(string ext) => Path.Combine(Path.GetTempPath(), "mo131_" + Guid.NewGuid().ToString("N") + ext);
    static (int w, int h) PngSize(string f) { var b = File.ReadAllBytes(f); int W(int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]; return (W(16), W(20)); }

    [Fact] public void DefaultStyleName() => Assert.Equal("default", Run("disp(figstyle)"));
    [Fact] public void NaturePreset() => Assert.Equal("nature 7", Run("figstyle('nature'); printf('%s %g', figstyle, get(groot,'DefaultAxesFontSize'))"));
    [Fact] public void ScienceTwoColumns() => Assert.Equal("science2", Run("figstyle('science','Columns',2); disp(figstyle)"));
    [Fact] public void UnknownPresetErrors() => Assert.Contains("unknown style", Run("try, figstyle('foo'); catch e, disp(e.message); end"));
    [Fact] public void GrootDefaultFontSize() => Assert.Equal("11", Run("set(groot,'DefaultAxesFontSize',11); disp(get(groot,'DefaultAxesFontSize'))"));
    [Fact] public void GrootDefaultLineWidth() => Assert.Equal("2", Run("set(groot,'DefaultLineLineWidth',2); h = plot(1:3); disp(get(h,'LineWidth'))"));
    [Fact] public void OkabeItoColorOrder() => Assert.Equal("0 0.447 0.698", Run("h = plot(1:3); printf('%.3g ', get(h,'Color'))").Trim());
    [Fact] public void ClassicRestoresMatlabColors() => Assert.Equal("0 0.447 0.741", Run("figstyle('classic'); h = plot(1:3); printf('%.3g ', get(h,'Color'))").Trim());
    [Fact] public void BoxOffByDefault() => Assert.Equal("off", Run("plot(1:3); disp(get(gca,'Box'))"));
    [Fact] public void TickDirOut() => Assert.Equal("out", Run("plot(1:3); disp(get(gca,'TickDir'))"));
    [Fact] public void ParulaMap() => Assert.Equal("64 3 0.242", Run("m = parula(64); printf('%d %d %.3g', size(m,1), size(m,2), m(1,1))"));
    [Fact] public void DefaultColormapIsParula() => Assert.Equal("1", Run("figure('visible','off'); colormap('default'); c = colormap; disp(abs(c(1,1)-0.2422) < 1e-3)"));
    [Fact] public void PanelLabelRuns() => Assert.Equal("ok", Run("figure('visible','off'); subplot(1,2,1); plot(1:3); subplot(1,2,2); plot(1:3); panellabel; panellabel(gca,'B'); panellabel('off'); disp('ok')"));
    [Fact]
    public void ExportgraphicsWidthResolution()
    {
        var f = Tmp(".png");
        Run($"figure('visible','off'); plot(1:10); exportgraphics(gcf, '{f.Replace("\\", "/")}', 'Width', 89, 'Units', 'mm', 'Resolution', 300);");
        var (w, _) = PngSize(f); File.Delete(f);
        Assert.InRange(w, 1050, 1053); // 89 mm at 300 dpi = 1051 px
    }
    [Fact]
    public void PrintResolutionFlag()
    {
        var f = Tmp(".png");
        Run($"figstyle('nature'); figure('visible','off'); plot(1:10); print('{f.Replace("\\", "/")}', '-dpng', '-r600');");
        var (w, _) = PngSize(f); File.Delete(f);
        Assert.InRange(w, 2100, 2104); // 89 mm at 600 dpi
    }
    [Fact]
    public void PdfPageInPoints()
    {
        var f = Tmp(".pdf");
        Run($"figure('visible','off'); plot(1:10); title('t'); exportgraphics(gcf, '{f.Replace("\\", "/")}', 'Width', 8.9, 'Units', 'cm');");
        var txt = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(f)); File.Delete(f);
        Assert.Contains("FontFile2", txt); // embedded TrueType subset
        Assert.Matches(@"MediaBox \[0 0 252", txt);
    }
    [Fact]
    public void SvgExport()
    {
        var f = Tmp(".svg");
        Run($"figure('visible','off'); plot(1:10); xlabel('time'); print('{f.Replace("\\", "/")}', '-dsvg');");
        var txt = File.ReadAllText(f); File.Delete(f);
        Assert.Contains("<svg", txt);
    }
}
