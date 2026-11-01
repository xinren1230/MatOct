// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using SP = ScottPlot;

namespace MatRewrite.Plotting;

/// <summary>Converts the Core graphics model into ScottPlot plots.</summary>
public static class FigureRenderer
{
    public static SP.Color ToColor(Rgb c) => new((byte)Math.Round(c.R * 255), (byte)Math.Round(c.G * 255), (byte)Math.Round(c.B * 255));

    sealed class CustomMap : SP.IColormap
    {
        readonly Rgb[] map;
        public CustomMap(string name, Rgb[] m) { Name = name; map = m; }
        public string Name { get; }
        public SP.Color GetColor(double normalizedIntensity) => ToColor(CustomColormaps.Sample(map, double.IsFinite(normalizedIntensity) ? normalizedIntensity : 0));
    }

    public static SP.IColormap Colormap(string name) => CustomColormaps.Get(name) is { } cm ? new CustomMap(name, cm) : name switch
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
        double[] TX(double[] v) => ax.XLog ? v.Select(x => x > 0 ? Math.Log10(x) : double.NaN).ToArray() : v;
        double[] TY(double[] v) => ax.YLog ? v.Select(y => y > 0 ? Math.Log10(y) : double.NaN).ToArray() : v;
        foreach (var s in ax.Children)
        {
            switch (s)
            {
                case LineSeries l:
                {
                    var sc = plot.Add.Scatter(TX(l.X), TY(l.Y));
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
                case ContourSeries cs:
                {
                    var lv = cs.Levels;
                    double lo = lv.Length > 0 ? lv.Min() : 0, hi = lv.Length > 0 ? lv.Max() : 1;
                    if (cs.Filled)
                    {
                        // quantize Z into level bands and draw as a heatmap, then outline with black contours
                        int R = cs.Z.GetLength(0), C = cs.Z.GetLength(1);
                        int up = Math.Max(2, 400 / Math.Max(R, C)); // supersample for smoother band edges
                        int RR = (R - 1) * up + 1, CC = (C - 1) * up + 1;
                        var q = new double[RR, CC];
                        for (int i = 0; i < RR; i++)
                            for (int j = 0; j < CC; j++)
                            {
                                double fi = (double)i / up, fj = (double)j / up;
                                int i0 = Math.Min((int)fi, R - 2), j0 = Math.Min((int)fj, C - 2);
                                double ti = fi - i0, tj = fj - j0;
                                double z = (1 - ti) * ((1 - tj) * cs.Z[i0, j0] + tj * cs.Z[i0, j0 + 1]) + ti * ((1 - tj) * cs.Z[i0 + 1, j0] + tj * cs.Z[i0 + 1, j0 + 1]);
                                int band = 0; while (band < lv.Length && z >= lv[band]) band++;
                                q[RR - 1 - i, j] = band;
                            }
                        var hm = plot.Add.Heatmap(q);
                        hm.Colormap = cmap;
                        hm.ManualRange = new SP.Range(0, Math.Max(lv.Length, 1));
                        double x0 = cs.X[0], x1 = cs.X[^1], y0 = cs.Y[0], y1 = cs.Y[^1];
                        hm.Extent = new SP.CoordinateRect(Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1), Math.Max(y0, y1));
                        lastImage = hm;
                    }
                    foreach (var (level, x, y) in cs.Lines)
                    {
                        var sc = plot.Add.Scatter(TX(x), TY(y));
                        sc.MarkerSize = 0;
                        sc.LineWidth = (float)Math.Max(1, cs.LineWidth * 2);
                        sc.Color = cs.LineColor is { } lc ? ToColor(lc) : cs.Filled ? SP.Colors.Black : cmap.GetColor(hi > lo ? (level - lo) / (hi - lo) : 0.5);
                    }
                    break;
                }
                case ErrorBarSeries eb:
                {
                    var col = ToColor(eb.Color);
                    if (eb.Style != LineStyle.None || eb.Marker != Marker.None)
                    {
                        var sc = plot.Add.Scatter(TX(eb.X), TY(eb.Y));
                        sc.Color = col; sc.LineWidth = eb.Style == LineStyle.None ? 0 : 1.5f; sc.LinePattern = Pattern(eb.Style);
                        sc.MarkerShape = Shape(eb.Marker, false); sc.MarkerSize = eb.Marker == Marker.None ? 0 : 6;
                        if (eb.DisplayName != "") sc.LegendText = eb.DisplayName;
                    }
                    var xs = TX(eb.X); var ys = TY(eb.Y);
                    double span = eb.X.Length > 1 ? (xs.Max() - xs.Min()) : 1, cap = span * 0.01 + 1e-12;
                    double spanY = eb.Y.Length > 1 ? (ys.Max() - ys.Min()) : 1, capY = spanY * 0.01 + 1e-12;
                    for (int i = 0; i < eb.X.Length; i++)
                    {
                        if (eb.Horizontal)
                        {
                            double a = TX(new[] { eb.X[i] - eb.L[i] })[0], b = TX(new[] { eb.X[i] + eb.U[i] })[0];
                            AddSeg(plot, a, ys[i], b, ys[i], col);
                            AddSeg(plot, a, ys[i] - capY, a, ys[i] + capY, col); AddSeg(plot, b, ys[i] - capY, b, ys[i] + capY, col);
                        }
                        else
                        {
                            double a = TY(new[] { eb.Y[i] - eb.L[i] })[0], b = TY(new[] { eb.Y[i] + eb.U[i] })[0];
                            AddSeg(plot, xs[i], a, xs[i], b, col);
                            AddSeg(plot, xs[i] - cap, a, xs[i] + cap, a, col); AddSeg(plot, xs[i] - cap, b, xs[i] + cap, b, col);
                        }
                    }
                    break;
                }
                case TextSeries t:
                {
                    var tx = plot.Add.Text(t.Text, TX(new[] { t.X })[0], TY(new[] { t.Y })[0]);
                    tx.LabelFontSize = (float)(t.FontSize * 1.2);
                    tx.LabelFontColor = ToColor(t.Color);
                    tx.LabelAlignment = t.HAlign switch { "center" => SP.Alignment.MiddleCenter, "right" => SP.Alignment.MiddleRight, _ => SP.Alignment.MiddleLeft };
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
        if (ax.XLog) { xl = xl.Select(Math.Log10).ToArray(); plot.Axes.Bottom.TickGenerator = LogTicks(); }
        if (ax.YLog) { yl = yl.Select(Math.Log10).ToArray(); plot.Axes.Left.TickGenerator = LogTicks(); }
        if (ax.YDirReverse) plot.Axes.SetLimits(xl[0], xl[1], yl[1], yl[0]);
        else plot.Axes.SetLimits(xl[0], xl[1], yl[0], yl[1]);
        if (ax.Equal || ax.Square) plot.Axes.SquareUnits();
    }

    static void AddSeg(SP.Plot plot, double x0, double y0, double x1, double y1, SP.Color c)
    {
        var sc = plot.Add.Scatter(new[] { x0, x1 }, new[] { y0, y1 });
        sc.Color = c; sc.MarkerSize = 0; sc.LineWidth = 1.2f;
    }

    /// <summary>Ticks for a log10-transformed axis: integer decades labelled with the real value.</summary>
    static SP.ITickGenerator LogTicks()
    {
        var tg = new SP.TickGenerators.NumericAutomatic
        {
            IntegerTicksOnly = true,
            LabelFormatter = v => Renderer3D.Fmt(Math.Pow(10, v), true),
            MinorTickGenerator = new SP.TickGenerators.LogMinorTickGenerator(),
        };
        return tg;
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
            if (Renderer3D.Handles(ax))
            {
                Renderer3D.Draw(canvas, ax, f.Colormap, (float)(c * cw), (float)(r * ch), (float)(cw * cs), (float)(ch * rs));
                continue;
            }
            var plot = new SP.Plot();
            Apply(plot, ax, f.Colormap);
            int w = (int)(cw * cs), h = (int)(ch * rs);
            using var img = plot.GetImage(w, h);
            using var sk = SkiaSharp.SKImage.FromEncodedData(img.GetImageBytes(SP.ImageFormat.Png));
            canvas.DrawImage(sk, (float)(c * cw), (float)(r * ch));
        }
        Renderer3D.DrawAnnotations(canvas, f, width, height);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var fmt = ext switch { ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg, ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp, ".webp" => SkiaSharp.SKEncodedImageFormat.Webp, _ => SkiaSharp.SKEncodedImageFormat.Png };
        if (ext == ".svg" && cells.Count == 1 && !Renderer3D.Handles(cells[0].ax) && f.Annotations.Count == 0)
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
