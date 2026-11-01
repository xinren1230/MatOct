// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using SP = ScottPlot;

namespace MatRewrite.Plotting;

/// <summary>Converts the Core graphics model into ScottPlot plots.</summary>
public static class FigureRenderer
{
    public static SP.Color ToColor(Rgb c) => new((byte)Math.Round(c.R * 255), (byte)Math.Round(c.G * 255), (byte)Math.Round(c.B * 255));

    public static SP.IColormap Colormap(string name) => name switch
    {
        "jet" => new SP.Colormaps.Jet(),
        "gray" => new SP.Colormaps.Grayscale(),
        "hot" => new SP.Colormaps.Thermal(),
        
        "turbo" => new SP.Colormaps.Turbo(),
        "magma" => new SP.Colormaps.Magma(),
        "inferno" => new SP.Colormaps.Inferno(),
        "plasma" => new SP.Colormaps.Plasma(),
        _ => new SP.Colormaps.Viridis(),
    };

    static SP.LinePattern Pattern(LineStyle s) => s switch
    {
        LineStyle.Dashed => SP.LinePattern.Dashed,
        LineStyle.Dotted => SP.LinePattern.Dotted,
        LineStyle.DashDot => SP.LinePattern.DenselyDashed,
        _ => SP.LinePattern.Solid
    };

    static SP.MarkerShape Shape(Marker m, bool filled) => m switch
    {
        Marker.Circle => filled ? SP.MarkerShape.FilledCircle : SP.MarkerShape.OpenCircle,
        Marker.Point => SP.MarkerShape.FilledCircle,
        Marker.Plus => SP.MarkerShape.Cross,
        Marker.Cross => SP.MarkerShape.Eks,
        Marker.Star or Marker.Pentagram or Marker.Hexagram => SP.MarkerShape.Asterisk,
        Marker.Square => filled ? SP.MarkerShape.FilledSquare : SP.MarkerShape.OpenSquare,
        Marker.Diamond => filled ? SP.MarkerShape.FilledDiamond : SP.MarkerShape.OpenDiamond,
        Marker.TriangleUp or Marker.TriangleLeft or Marker.TriangleRight => filled ? SP.MarkerShape.FilledTriangleUp : SP.MarkerShape.OpenTriangleUp,
        Marker.TriangleDown => filled ? SP.MarkerShape.FilledTriangleDown : SP.MarkerShape.OpenTriangleDown,
        _ => SP.MarkerShape.None
    };

    /// <summary>Fill a ScottPlot plot from one axes.</summary>
    public static void Apply(SP.Plot plot, Axes ax, string colormap)
    {
        plot.Clear();
        var cmap = Colormap(colormap);
        SP.Plottables.Heatmap? lastImage = null;
        foreach (var s in ax.Children)
        {
            switch (s)
            {
                case LineSeries l:
                {
                    var sc = plot.Add.Scatter(l.X, l.Y);
                    sc.Color = ToColor(l.Color);
                    sc.LineWidth = l.Style == LineStyle.None ? 0 : (float)Math.Max(1, l.LineWidth * 2);
                    sc.LinePattern = Pattern(l.Style);
                    sc.MarkerShape = Shape(l.Marker, l.MarkerFilled || l.Marker == Marker.Point);
                    sc.MarkerSize = l.Marker == Marker.None ? 0 : (float)(l.Marker == Marker.Point ? l.MarkerSize / 2 : l.MarkerSize);
                    if (l.DisplayName != "") sc.LegendText = l.DisplayName;
                    break;
                }
                case ScatterSeries p:
                {
                    var shape = Shape(p.Marker, p.Filled);
                    double? cmin = p.CData?.Min(), cmax = p.CData?.Max();
                    for (int i = 0; i < p.X.Length; i++)
                    {
                        var size = p.Sizes.Length == p.X.Length ? p.Sizes[i] : p.Sizes[0];
                        var col = ToColor(p.Color);
                        if (p.CData != null)
                        {
                            double f = cmax > cmin ? (p.CData[i] - cmin!.Value) / (cmax!.Value - cmin.Value) : 0.5;
                            col = cmap.GetColor(f);
                        }
                        var m = plot.Add.Marker(p.X[i], p.Y[i], shape, (float)Math.Sqrt(size) * 1.5f, col);
                        if (i == 0 && p.DisplayName != "") m.LegendText = p.DisplayName;
                    }
                    break;
                }
                case BarSeries b:
                {
                    var bars = new List<SP.Bar>();
                    double groupW = b.Width;
                    double each = b.GroupCount > 1 ? groupW / b.GroupCount : groupW;
                    double step = b.X.Length > 1 ? Enumerable.Range(1, b.X.Length - 1).Min(i => Math.Abs(b.X[i] - b.X[i - 1])) : 1;
                    for (int i = 0; i < b.X.Length; i++)
                    {
                        double pos = b.X[i];
                        if (b.GroupCount > 1) pos += (b.GroupIndex - (b.GroupCount - 1) / 2.0) * each * step;
                        bars.Add(new SP.Bar { Position = pos, Value = b.Y[i], FillColor = ToColor(b.Color), Size = each * step, LineWidth = 0.5f, LineColor = SP.Colors.Black.WithAlpha(0.5) });
                    }
                    var bp = plot.Add.Bars(bars);
                    bp.Horizontal = b.Horizontal;
                    if (b.DisplayName != "") bp.LegendText = b.DisplayName;
                    break;
                }
                case ImageSeries im:
                {
                    int r = im.C.GetLength(0), c = im.C.GetLength(1);
                    var data = (double[,])im.C.Clone();
                    var hm = plot.Add.Heatmap(data);
                    hm.Colormap = cmap;
                    hm.ManualRange = new SP.Range(im.CMin, im.CMax);
                    double dx = c > 1 ? (im.X1 - im.X0) / (c - 1) : 1, dy = r > 1 ? (im.Y1 - im.Y0) / (r - 1) : 1;
                    // ScottPlot draws row 0 at the top; with ydir reversed we flip the axis instead.
                    hm.FlipVertically = false;
                    hm.Extent = new SP.CoordinateRect(im.X0 - dx / 2, im.X1 + dx / 2, im.Y0 - dy / 2, im.Y1 + dy / 2);
                    lastImage = hm;
                    break;
                }
            }
        }
        if (ax.Title != "") plot.Title(ax.Title);
        if (ax.XLabel != "") plot.XLabel(ax.XLabel);
        if (ax.YLabel != "") plot.YLabel(ax.YLabel);
        if (ax.Grid) plot.ShowGrid(); else plot.HideGrid();
        if (ax.LegendVisible)
        {
            plot.ShowLegend(ax.LegendLocation switch
            {
                "north" => SP.Alignment.UpperCenter,
                "south" => SP.Alignment.LowerCenter,
                "east" => SP.Alignment.MiddleRight,
                "west" => SP.Alignment.MiddleLeft,
                "northwest" => SP.Alignment.UpperLeft,
                "southwest" => SP.Alignment.LowerLeft,
                "southeast" => SP.Alignment.LowerRight,
                _ => SP.Alignment.UpperRight,
            });
        }
        else plot.HideLegend();
        if (ax.Colorbar && lastImage != null) plot.Add.ColorBar(lastImage);
        if (!ax.Visible) plot.Layout.Frameless();

        var (xl, yl) = MatRewrite.Core.Builtins.BuiltinLibrary.Limits(ax);
        if (ax.YDirReverse) plot.Axes.SetLimits(xl[0], xl[1], yl[1], yl[0]);
        else plot.Axes.SetLimits(xl[0], xl[1], yl[0], yl[1]);
        if (ax.Equal || ax.Square) plot.Axes.SquareUnits();
    }

    /// <summary>Grid layout of the figure's axes: (row, col, rowSpan, colSpan) on a common grid.</summary>
    public static (int rows, int cols, List<(Axes ax, int r, int c, int rs, int cs)> cells) Layout(Figure f)
    {
        int rows = 1, cols = 1;
        foreach (var a in f.Axes) { rows = Lcm(rows, a.Rows); cols = Lcm(cols, a.Cols); }
        var list = new List<(Axes, int, int, int, int)>();
        foreach (var a in f.Axes)
        {
            int sr = rows / a.Rows, sc = cols / a.Cols;
            int r0 = int.MaxValue, c0 = int.MaxValue, r1 = 0, c1 = 0;
            foreach (var cell in a.Cells)
            {
                int r = (cell - 1) / a.Cols, c = (cell - 1) % a.Cols;
                r0 = Math.Min(r0, r); c0 = Math.Min(c0, c); r1 = Math.Max(r1, r); c1 = Math.Max(c1, c);
            }
            list.Add((a, r0 * sr, c0 * sc, (r1 - r0 + 1) * sr, (c1 - c0 + 1) * sc));
        }
        return (rows, cols, list);
    }

    static int Lcm(int a, int b) { int g = Gcd(a, b); return a / g * b; }
    static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    /// <summary>Render a whole figure to an image file.</summary>
    public static void Save(Figure f, string path, int width, int height)
    {
        var (rows, cols, cells) = Layout(f);
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.White);
        double cw = (double)width / cols, ch = (double)height / rows;
        foreach (var (ax, r, c, rs, cs) in cells)
        {
            var plot = new SP.Plot();
            Apply(plot, ax, f.Colormap);
            int w = (int)(cw * cs), h = (int)(ch * rs);
            using var img = plot.GetImage(w, h);
            using var sk = SkiaSharp.SKImage.FromEncodedData(img.GetImageBytes(SP.ImageFormat.Png));
            canvas.DrawImage(sk, (float)(c * cw), (float)(r * ch));
        }
        if (f.Axes.Count == 0) { }
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var fmt = ext switch { ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg, ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp, ".webp" => SkiaSharp.SKEncodedImageFormat.Webp, _ => SkiaSharp.SKEncodedImageFormat.Png };
        if (ext == ".svg" && cells.Count == 1)
        {
            var plot = new SP.Plot();
            Apply(plot, cells[0].ax, f.Colormap);
            plot.SaveSvg(path, width, height);
            return;
        }
        using var snap = surface.Snapshot();
        using var data = snap.Encode(fmt, 95);
        File.WriteAllBytes(path, data.ToArray());
    }
}

/// <summary>A backend that only supports exporting (used by the console REPL when no window toolkit is available).</summary>
public sealed class FileOnlyBackend : IGraphicsBackend
{
    public void Update(Figure f) { }
    public void Close(Figure f) { }
    public void Export(Figure f, string path, int width, int height) => FigureRenderer.Save(f, path, width, height);
}
