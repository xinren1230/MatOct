using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;
using MatRewrite.Plotting;
using Xunit;

namespace MatRewrite.Tests;

public class Graphics3DTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "mrg3_" + Guid.NewGuid().ToString("N"));
    public Graphics3DTests() { Directory.CreateDirectory(dir); }
    public void Dispose() { try { Directory.Delete(dir, true); } catch (IOException) { } }

    sealed class FileBackend : IGraphicsBackend
    {
        public void Update(Figure f) { }
        public void Close(Figure f) { }
        public void Export(Figure f, string path, int w, int h) => FigureRenderer.Save(f, path, w, h);
    }

    (Interpreter it, string output) Exec(string code)
    {
        var o = new StringWriter();
        var it = new Interpreter(o) { };
        it.Graphics.Backend = new FileBackend();
        var old = Environment.CurrentDirectory;
        Environment.CurrentDirectory = dir;
        try { it.Eval(code); } finally { Environment.CurrentDirectory = old; }
        return (it, o.ToString().Replace("\r\n", "\n").Trim());
    }
    string Run(string code) => Exec(code).output;

    const string Peaks = "[X,Y] = meshgrid(-2:0.25:2); Z = X.*exp(-X.^2-Y.^2); ";

    [Fact] public void SurfSets3DView() => Assert.Equal("-37.5 30", Run(Peaks + "surf(X,Y,Z); v = view; printf('%g %g', v)"));
    [Fact] public void ViewForms() => Assert.Equal("0 90|30 45|-37.5 30", Run("plot(1:3); a=view; view(30,45); b=view; view(3); c=view; printf('%g %g|%g %g|%g %g', a, b, c)"));
    [Fact] public void SurfType() => Assert.Equal("surface 17", Run(Peaks + "h = surf(X,Y,Z); printf('%s %d', get(h,'type'), rows(get(h,'zdata')))"));
    [Fact] public void MeshAndShading() => Assert.Equal("", Run(Peaks + "mesh(Z); figure; surf(Z); shading flat; shading interp"));
    [Fact] public void Plot3ZData() => Assert.Equal("[3 2 1]", Run("h = plot3(1:3, 1:3, [3 2 1], 'r-o'); disp(mat2str(get(h,'zdata')))"));
    [Fact] public void ZlabelZlim() => Assert.Equal("Z|[-5 5]", Run("plot3(1:3,1:3,1:3); zlabel('Z'); zlim([-5 5]); printf('%s|%s', get(get(gca,'zlabel'),'string'), mat2str(zlim))"));
    [Fact] public void ZlimAuto() => Assert.Equal("[0 10]", Run("plot3([0 1],[0 1],[0.5 9.5]); disp(mat2str(zlim))"));
    [Fact] public void PlotAfterSurfIs2D() => Assert.Equal("0 90", Run(Peaks + "surf(Z); plot(1:3); printf('%g %g', view)"));

    [Fact] public void ContourcSimple() => Assert.Equal("[2.5 1 2;2 1.75 1.25]", Run("disp(mat2str(contourc([1 2;3 4], [2.5 2.5])))"));
    [Fact] public void ContourLevelsVector() => Assert.Equal("[-0.2 0 0.2]", Run(Peaks + "[C,h] = contour(X,Y,Z,[-0.2 0 0.2]); disp(mat2str(get(h,'levellist')))"));
    [Fact] public void ContourNLevels() => Assert.Equal("4", Run(Peaks + "[C,h] = contourf(Z, 4); disp(numel(get(h,'levellist')))"));
    [Fact]
    public void ContourClosedRing()
    {
        // a radial bowl gives one closed ring per level: first and last points coincide
        var r = Run("[X,Y]=meshgrid(-1:0.1:1); C = contourc(-1:0.1:1, -1:0.1:1, X.^2+Y.^2, [0.25 0.25]); n = C(2,1); printf('%d %d', n == columns(C)-1, norm(C(:,2)-C(:,end)) < 1e-9)");
        Assert.Equal("1 1", r);
    }
    [Fact]
    public void ContourAutoLevelsInside()
    {
        var lv = Contour.AutoLevels(new double[,] { { 0, 1 }, { 2, 10 } });
        Assert.All(lv, v => Assert.InRange(v, 0.0001, 9.9999));
        Assert.True(lv.Length >= 4);
    }

    [Fact] public void LogScales() => Assert.Equal("log linear|linear log|log log", Run("semilogx(1:10); a=get(gca,'xscale'); b=get(gca,'yscale'); semilogy(1:10); c=get(gca,'xscale'); d=get(gca,'yscale'); loglog(1:10,1:10); printf('%s %s|%s %s|%s %s', a,b,c,d,get(gca,'xscale'),get(gca,'yscale'))"));
    [Fact] public void LogLimitsAreDecades() => Assert.Equal("[1 1000]", Run("loglog([2 300],[1 2]); disp(mat2str(xlim))"));
    [Fact] public void SetXScale() => Assert.Equal("log", Run("plot(1:10); set(gca,'xscale','log'); disp(get(gca,'xscale'))"));
    [Fact] public void ErrorbarLimits() => Assert.Equal("hggroup 1", Run("h = errorbar(1:3, [1 2 3], [0.5 0.5 0.5]); yl = ylim; printf('%s %d', get(h,'type'), yl(1) <= 0.5)"));
    [Fact] public void TextObject() => Assert.Equal("text hello [2 3 0]", Run("plot(1:5); h = text(2, 3, 'hello'); printf('%s %s %s', get(h,'type'), get(h,'string'), mat2str(get(h,'position')))"));
    [Fact] public void AnnotationTypes() => Assert.Equal("hggroup", Run("plot(1:3); annotation('arrow',[0.1 0.4],[0.1 0.4]); annotation('textbox',[0.5 0.5 0.2 0.1],'String','hi'); h = annotation('line',[0 1],[0 1]); disp(get(h,'type'))"));
    [Fact] public void AnnotationBadType() => Assert.Contains("unknown annotation TYPE", Assert.Throws<OctaveException>(() => Run("annotation('blob')")).Message);

    [Fact]
    public void SurfPngHasColormapColors()
    {
        Run(Peaks + "surf(X,Y,Z); colormap jet; colorbar; title('t'); print('-dpng','s.png')");
        var p = Path.Combine(dir, "s.png");
        Assert.True(File.Exists(p));
        using var bmp = SkiaSharp.SKBitmap.Decode(p);
        var colors = new HashSet<uint>();
        for (int y = 0; y < bmp.Height; y += 7) for (int x = 0; x < bmp.Width; x += 7) colors.Add((uint)bmp.GetPixel(x, y));
        Assert.True(colors.Count > 50, $"only {colors.Count} colors");
    }

    [Fact]
    public void AllNewPlotsExport()
    {
        Run(Peaks + "subplot(2,3,1); mesh(Z); subplot(2,3,2); plot3(1:3,1:3,1:3); subplot(2,3,3); contour(Z); subplot(2,3,4); contourf(X,Y,Z); colorbar; " +
            "subplot(2,3,5); loglog(1:100); subplot(2,3,6); errorbar(1:3,1:3,[1 1 1]); text(1,1,'a'); annotation('doublearrow',[0.2 0.8],[0.5 0.5]); print('-dpng','all.png')");
        Assert.True(new FileInfo(Path.Combine(dir, "all.png")).Length > 5000);
    }

    [Fact]
    public void Renderer3DRotatesWithView()
    {
        var (it, _) = Exec(Peaks + "surf(X,Y,Z);");
        var ax = it.Graphics.Gca();
        var a = Renderer3D.RenderPng(ax, "viridis", 300, 200);
        ax.Az = 60; ax.El = 10;
        var b = Renderer3D.RenderPng(ax, "viridis", 300, 200);
        Assert.NotEqual(Convert.ToBase64String(a), Convert.ToBase64String(b));
    }
}
