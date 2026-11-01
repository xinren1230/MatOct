using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;
using MatRewrite.Plotting;
using Xunit;

namespace MatRewrite.Tests;

public class GraphicsTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "mrgfx_" + Guid.NewGuid().ToString("N"));
    public GraphicsTests() { Directory.CreateDirectory(dir); }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    sealed class Recorder : IGraphicsBackend
    {
        public int Updates, Closes;
        public void Update(Figure f) => Updates++;
        public void Close(Figure f) => Closes++;
        public void Export(Figure f, string path, int w, int h) => FigureRenderer.Save(f, path, w, h);
    }

    (Interpreter it, StringWriter o, Recorder r) Make()
    {
        var o = new StringWriter();
        var it = new Interpreter(o);
        var r = new Recorder();
        it.Graphics.Backend = r;
        return (it, o, r);
    }

    string Run(string code)
    {
        var (it, o, _) = Make();
        var old = Environment.CurrentDirectory;
        Environment.CurrentDirectory = dir;
        try { it.Eval(code); } finally { Environment.CurrentDirectory = old; }
        return o.ToString().Replace("\r\n", "\n").Trim();
    }

    [Fact] public void FigureHandles() => Assert.Equal("1\n2\n2", Run("disp(figure); disp(figure); disp(gcf)"));
    [Fact] public void PlotCreatesLines() => Assert.Equal("2", Run("h = plot(1:3, [1 2 3; 4 5 6]'); disp(numel(h))"));
    [Fact] public void HoldAccumulates() => Assert.Equal("2", Run("plot(1:3); hold on; plot(3:-1:1); disp(numel(get(gca,'children')))"));
    [Fact] public void HoldOffReplaces() => Assert.Equal("1", Run("plot(1:3); hold off; plot(3:-1:1); disp(numel(get(gca,'children')))"));
    [Fact] public void IsHold() => Assert.Equal("0\n1", Run("figure; disp(ishold); hold on; disp(ishold)"));
    [Fact] public void Labels() => Assert.Equal("T|X|Y", Run("plot(1:3); title('T'); xlabel('X'); ylabel('Y'); printf('%s|%s|%s', get(get(gca,'title'),'string'), get(get(gca,'xlabel'),'string'), get(get(gca,'ylabel'),'string'))"));
    [Fact] public void AxisLimits() => Assert.Equal("[0 10 -1 1]", Run("plot(1:3); axis([0 10 -1 1]); disp(mat2str(axis))"));
    [Fact] public void XlimYlim() => Assert.Equal("[2 5]\n[-3 3]", Run("plot(1:10); xlim([2 5]); ylim([-3 3]); disp(mat2str(xlim)); disp(mat2str(ylim))"));
    [Fact] public void SubplotDistinctAxes() => Assert.Equal("0\n1", Run("a = subplot(2,1,1); b = subplot(2,1,2); disp(a == b); c = subplot(2,1,1); disp(c == a)"));
    [Fact] public void HistCounts() => Assert.Equal("[2 1 1]", Run("n = hist([1 1 2 3], 3); disp(mat2str(n))"));
    [Fact] public void CloseAll() => Assert.Equal("1", Run("figure; figure; close all; disp(isempty(get(0,'children')))"));
    [Fact] public void CloseOne() => Assert.Equal("0\n1", Run("h = figure; g = figure; close(h); disp(ishandle(h)); disp(ishandle(g))"));
    [Fact] public void SetGetLine() => Assert.Equal("3", Run("h = plot(1:3); set(h, 'LineWidth', 3); disp(get(h, 'linewidth'))"));
    [Fact] public void GridAndLegend() => Assert.Equal("", Run("plot(1:3, 1:3, 'r--o'); grid on; legend('a'); grid off; legend off"));
    [Fact] public void ScatterBarImage() => Assert.Equal("", Run("scatter(1:3, 4:6); figure; bar([1 2 3]); figure; imagesc(magic(4)); colorbar; colormap gray"));

    [Fact]
    public void BackendIsNotified()
    {
        var (it, _, r) = Make();
        it.Eval("plot(1:3); title('x'); close all");
        Assert.True(r.Updates >= 1);
        Assert.Equal(1, r.Closes);
    }

    [Fact]
    public void PrintWritesPng()
    {
        Run("subplot(1,2,1); plot(sin(0:0.1:6)); subplot(1,2,2); imagesc(magic(5)); colorbar; print('-dpng', 'fig.png')");
        var p = Path.Combine(dir, "fig.png");
        Assert.True(File.Exists(p));
        var b = File.ReadAllBytes(p);
        Assert.True(b.Length > 1000);
        Assert.Equal(0x89, b[0]); Assert.Equal((byte)'P', b[1]);
    }

    [Fact]
    public void LayoutSpans()
    {
        var (it, _, _) = Make();
        it.Eval("subplot(2,2,[1 2]); plot(1:3); subplot(2,2,3); plot(1:3); subplot(2,2,4); plot(1:3)");
        var (rows, cols, cells) = FigureRenderer.Layout(it.Graphics.Gcf());
        Assert.Equal((2, 2), (rows, cols));
        Assert.Contains(cells, c => c.r == 0 && c.c == 0 && c.cs == 2);
        Assert.Equal(3, cells.Count);
    }
}
