// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;
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

    sealed class ParulaMap : SP.IColormap
    {
        public string Name => "parula";
        public SP.Color GetColor(double t) => ToColor(FigStyle.Parula(t));
    }

    public static SP.IColormap Colormap(string name) => CustomColormaps.Get(name) is { } cm ? new CustomMap(name, cm) : name switch
    {
        "parula" or "default" => new ParulaMap(),
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
    /// <summary>Pixels per point for a figure rendered <paramref name="pixelWidth"/> px wide (export).</summary>
    public static double ExportScale(Figure f, int pixelWidth) => Math.Max(0.25, pixelWidth / (f.ExportWidthPt > 0 ? f.ExportWidthPt : FigStyle.Current.WidthPt));

    /// <param name="scale">device pixels per point (0 = on-screen default of the current style)</param>
    public static void Apply(SP.Plot plot, Axes ax, string colormap, double scale = 0)
    {
        plot.Clear();
        plot.RenderManager.RenderActions.RemoveAll(r => r is PanelLabelAction);
        var st = FigStyle.Current;
        plot.ScaleFactor = scale > 0 ? scale : st.ScreenPxPerPt;
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
                    if (l.Props.TryGetValue("visible", out var lv) && lv is MatrixValue lvm && lvm.IsChar && lvm.AsString() == "off") break;
                    var sc = plot.Add.Scatter(TX(l.X), TY(l.Y));
                    sc.Color = ToColor(l.Color);
                    sc.LineWidth = l.Style == LineStyle.None ? 0 : (float)Math.Max(0.25, l.LineWidth);
                    sc.LinePattern = Pattern(l.Style);
                    if (l.DisplayName != "") sc.LegendText = Tex(l.DisplayName);
                    void StyleMarkers(SP.Plottables.Scatter m)
                    {
                        bool filled = l.MarkerFilled || l.Marker == Marker.Point;
                        m.MarkerShape = Shape(l.Marker, filled);
                        m.MarkerSize = l.Marker == Marker.None ? 0 : (float)(l.Marker == Marker.Point ? l.MarkerSize / 3 : l.MarkerSize);
                        var edge = ToColor(l.MarkerEdge ?? l.Color);
                        m.MarkerLineColor = edge;
                        m.MarkerLineWidth = (float)Math.Max(0.5, l.LineWidth * 0.8);
                        m.MarkerFillColor = filled ? ToColor(l.MarkerFace ?? l.Color) : SP.Colors.Transparent;
                        m.MarkerStyle.OutlineColor = edge; m.MarkerStyle.OutlineWidth = (float)Math.Max(0.5, l.LineWidth * 0.8);
                        if (l.Marker == Marker.Point) m.MarkerFillColor = edge;
                    }
                    if (l.MarkerIndices == null || l.Marker == Marker.None) StyleMarkers(sc);
                    else
                    {
                        sc.MarkerSize = 0;
                        var idx = l.MarkerIndices.Where(i => i >= 1 && i <= l.X.Length).Select(i => i - 1).ToArray();
                        if (idx.Length > 0)
                        {
                            var mk = plot.Add.Scatter(TX(idx.Select(i => l.X[i]).ToArray()), TY(idx.Select(i => l.Y[i]).ToArray()));
                            mk.LineWidth = 0; mk.Color = ToColor(l.Color); StyleMarkers(mk);
                        }
                    }
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
                        var m = plot.Add.Marker(p.X[i], p.Y[i], shape, (float)Math.Sqrt(size) * 0.9f, col);
                        if (i == 0 && p.DisplayName != "") m.LegendText = p.DisplayName;
                    }
                    break;
                }
                case PatchSeries ps when !ax.Is3D:
                {
                    foreach (var (px, py) in ps.Polys)
                    {
                        var xs = TX(px); var ys = TY(py);
                        var pts = Enumerable.Range(0, Math.Min(xs.Length, ys.Length)).Where(i => double.IsFinite(xs[i]) && double.IsFinite(ys[i])).Select(i => new SP.Coordinates(xs[i], ys[i])).ToArray();
                        if (pts.Length < 2) continue;
                        var pg = plot.Add.Polygon(pts);
                        pg.FillColor = ps.FaceColor is Rgb fc ? ToColor(fc).WithAlpha(ps.FaceAlpha) : SP.Colors.Transparent;
                        pg.LineColor = ps.EdgeColor is Rgb ec ? ToColor(ec) : SP.Colors.Transparent;
                        pg.LineWidth = (float)Math.Max(0.25, ps.LineWidth);
                        if (ps.DisplayName != "") pg.LegendText = ps.DisplayName;
                    }
                    break;
                }
                case BarSeries b:
                {
                    var bars = new List<SP.Bar>();
                    double groupW = b.Width;
                    double each = b.GroupCount > 1 ? groupW / b.GroupCount : groupW;
                    double step = b.X.Length > 1 ? Enumerable.Range(1, b.X.Length - 1).Min(i => Math.Abs(b.X[i] - b.X[i - 1])) : 1;
                    if (b.X1 != null && b.X2 != null && b.Y0 != null && b.Y1 != null)
                    {
                        for (int i = 0; i < b.X1.Length; i++)
                            bars.Add(new SP.Bar { Position = (b.X1[i] + b.X2[i]) / 2, Size = b.X2[i] - b.X1[i], ValueBase = b.Y0[i], Value = b.Y1[i], FillColor = ToColor(b.Color), LineWidth = 0.4f, LineColor = SP.Colors.Black.WithAlpha(0.6) });
                    }
                    else
                    for (int i = 0; i < b.X.Length; i++)
                    {
                        double pos = b.X[i];
                        if (b.GroupCount > 1) pos += (b.GroupIndex - (b.GroupCount - 1) / 2.0) * each * step;
                        bars.Add(new SP.Bar { Position = pos, Value = b.Y[i], FillColor = ToColor(b.Color), Size = each * step, LineWidth = 0.4f, LineColor = SP.Colors.Black.WithAlpha(0.6) });
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
                        sc.LineWidth = (float)Math.Max(0.4, cs.LineWidth);
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
                        sc.Color = col; sc.LineWidth = eb.Style == LineStyle.None ? 0 : (float)st.LineWidth; sc.LinePattern = Pattern(eb.Style);
                        sc.MarkerShape = Shape(eb.Marker, false); sc.MarkerSize = eb.Marker == Marker.None ? 0 : (float)st.MarkerSize;
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
                    tx.LabelFontSize = (float)t.FontSize; tx.LabelFontName = ResolveFont(PS(t, "fontname"));
                    tx.LabelFontColor = ToColor(t.Color);
                    tx.LabelAlignment = t.HAlign switch { "center" => SP.Alignment.MiddleCenter, "right" => SP.Alignment.MiddleRight, _ => SP.Alignment.MiddleLeft };
                    break;
                }
            }
        }
        if (ax.Title != "") plot.Title(Tex(ax.Title));
        if (ax.XLabel != "") plot.XLabel(Tex(ax.XLabel));
        if (ax.YLabel != "") plot.YLabel(Tex(ax.YLabel));
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
        if (!ax.Visible)
        {
            plot.Layout.Frameless();
            // Frameless hides the title too; pie charts etc. still need it.
            if (ax.Title != "") { plot.Layout.Default(); foreach (var a in plot.Axes.GetAxes()) a.IsVisible = false; plot.Axes.Title.IsVisible = true; }
        }
        if (!string.IsNullOrEmpty(ax.PanelLabel)) plot.RenderManager.RenderActions.Add(new PanelLabelAction(ax.PanelLabel!, ResolveFont(null), (float)st.PanelLabelSize));

        var (xl, yl) = MatRewrite.Core.Builtins.BuiltinLibrary.Limits(ax);
        if (ax.XLog) { xl = xl.Select(Math.Log10).ToArray(); plot.Axes.Bottom.TickGenerator = LogTicks(); }
        if (ax.YLog) { yl = yl.Select(Math.Log10).ToArray(); plot.Axes.Left.TickGenerator = LogTicks(); }
        if (ax.YDirReverse) plot.Axes.SetLimits(xl[0], xl[1], yl[1], yl[0]);
        else plot.Axes.SetLimits(xl[0], xl[1], yl[0], yl[1]);
        if (ax.Equal || ax.Square) plot.Axes.SquareUnits();
        ApplyStyle(plot, ax, lastImage);
    }

    // ---- Phase 13: MATLAB axes styling (fonts, ticks, colours, box, grid, legend) ----
    static string? PS(GObject? o, string k) => o != null && o.Props.TryGetValue(k, out var v) && v is MatrixValue m && m.IsChar ? m.AsString() : null;
    static double? PN(GObject? o, string k) => o != null && o.Props.TryGetValue(k, out var v) && v is MatrixValue m && !m.IsChar && m.Numel >= 1 ? m.Re[0] : null;
    static double[]? PV(GObject? o, string k) => o != null && o.Props.TryGetValue(k, out var v) && v is MatrixValue m && !m.IsChar ? m.Re.ToArray() : null;
    static Rgb? PC(GObject? o, string k)
    {
        if (o == null || !o.Props.TryGetValue(k, out var v) || v is not MatrixValue m) return null;
        if (!m.IsChar && m.Numel == 3) return new Rgb(m.Re[0], m.Re[1], m.Re[2]);
        return m.IsChar ? Rgb.FromName(m.AsString().ToLowerInvariant()) : null;
    }
    static string[]? PL(GObject? o, string k)
    {
        if (o == null || !o.Props.TryGetValue(k, out var v)) return null;
        if (v is CellValue c) return c.Data.Select(x => x is MatrixValue xm && xm.IsChar ? xm.AsString() : x is MatrixValue xn && xn.Numel == 1 ? Renderer3D.Fmt(xn.Re[0], true) : "").ToArray();
        if (v is MatrixValue m && m.IsChar)
        {
            if (m.R > 1) return Enumerable.Range(0, m.R).Select(r => new string(Enumerable.Range(0, m.C).Select(cc => (char)m.Re[r + cc * m.R]).ToArray()).TrimEnd()).ToArray();
            return m.AsString().Split('|');
        }
        if (v is MatrixValue n && !n.IsChar) return n.Re.Select(x => Renderer3D.Fmt(x, true)).ToArray();
        return null;
    }
    static readonly Dictionary<string, string> TexMap = new()
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ",
        ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π", ["rho"] = "ρ", ["sigma"] = "σ",
        ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ",
        ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ", ["Pi"] = "Π", ["Sigma"] = "Σ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
        ["infty"] = "∞", ["pm"] = "±", ["circ"] = "°", ["times"] = "×", ["leq"] = "≤", ["geq"] = "≥", ["neq"] = "≠", ["approx"] = "≈",
        ["rightarrow"] = "→", ["leftarrow"] = "←", ["partial"] = "∂", ["nabla"] = "∇", ["int"] = "∫", ["sum"] = "∑", ["sqrt"] = "√", ["cdot"] = "·", ["deg"] = "°",
    };
    /// <summary>Minimal TeX interpreter: greek letters/symbols and ^{..}/_{..} with unicode super/subscripts where available.</summary>
    public static string Tex(string s)
    {
        if (string.IsNullOrEmpty(s) || (s.IndexOf('\\') < 0 && s.IndexOf('^') < 0 && s.IndexOf('_') < 0)) return s;
        var r = System.Text.RegularExpressions.Regex.Replace(s, @"\\([A-Za-z]+)", m => TexMap.TryGetValue(m.Groups[1].Value, out var u) ? u : m.Value);
        const string sup = "⁰¹²³⁴⁵⁶⁷⁸⁹", sub = "₀₁₂₃₄₅₆₇₈₉";
        string Conv(string t, string tbl, string extra) => new(t.Select(ch => char.IsDigit(ch) ? tbl[ch - '0'] : ch == '-' ? extra[0] : ch == '+' ? extra[1] : ch).ToArray());
        r = System.Text.RegularExpressions.Regex.Replace(r, @"\^\{([^}]*)\}|\^([0-9+-])", m => Conv(m.Groups[1].Success && m.Groups[1].Length > 0 ? m.Groups[1].Value : m.Groups[2].Value, sup, "⁻⁺"));
        r = System.Text.RegularExpressions.Regex.Replace(r, @"_\{([^}]*)\}|_([0-9])", m => Conv(m.Groups[1].Success && m.Groups[1].Length > 0 ? m.Groups[1].Value : m.Groups[2].Value, sub, "₋₊"));
        return r;
    }
    static readonly Dictionary<string, string> fontCache = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Resolve a requested font family to one installed (Arial, Helvetica, Liberation Sans, DejaVu Sans).</summary>
    public static string ResolveFont(string? requested)
    {
        var want = string.IsNullOrEmpty(requested) || requested == "*" ? FigStyle.Current.FontName : requested!;
        lock (fontCache)
        {
            if (fontCache.TryGetValue(want, out var hit)) return hit;
            string? found = null;
            foreach (var fam in new[] { want, "Arial", "Helvetica", "Liberation Sans", "Nimbus Sans", "DejaVu Sans" })
            {
                using var tf = SkiaSharp.SKFontManager.Default.MatchFamily(fam);
                if (tf != null && tf.FamilyName.Equals(fam, StringComparison.OrdinalIgnoreCase)) { found = tf.FamilyName; break; }
            }
            return fontCache[want] = found ?? want;
        }
    }

    /// <summary>Draws a bold lowercase panel label (a, b, c...) at the top-left of the figure cell.</summary>
    sealed class PanelLabelAction(string text, string font, float size) : SP.IRenderAction
    {
        public void Render(SP.RenderPack rp) => Draw(rp.Canvas, rp.FigureRect.Left, rp.FigureRect.Top);
        public void Draw(SkiaSharp.SKCanvas canvas, float left, float top)
        {
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = true, TextSize = size,
                Typeface = SkiaSharp.SKTypeface.FromFamilyName(font, SkiaSharp.SKFontStyle.Bold) };
            canvas.DrawText(text, left + size * 0.25f, top + size * 1.05f, paint);
        }
    }

    static void Font(SP.LabelStyle st, GObject? primary, GObject ax, double baseSize)
    {
        var name = PS(primary, "fontname") ?? PS(ax, "fontname");
        st.FontName = ResolveFont(name is null or "*" ? null : name);
        var fs = FigStyle.Current;
        var sz = PN(primary, "fontsize") ?? (PN(ax, "fontsize") is double a ? a * baseSize / fs.FontSize : baseSize);
        if (sz is double z && z > 0) st.FontSize = (float)z;
        var w = PS(primary, "fontweight") ?? (primary == null ? PS(ax, "fontweight") : null);
        if (w != null) st.Bold = w == "bold";
        var an = PS(primary, "fontangle") ?? PS(ax, "fontangle");
        if (an != null) st.Italic = an is "italic" or "oblique";
        if (PC(primary, "color") is Rgb c) st.ForeColor = ToColor(c);
    }

    static void AxColor(SP.IAxis a, SP.Color c)
    {
        a.FrameLineStyle.Color = c; a.MajorTickStyle.Color = c; a.MinorTickStyle.Color = c;
        a.TickLabelStyle.ForeColor = c; a.Label.ForeColor = c;
    }

    static void ApplyStyle(SP.Plot plot, Axes ax, SP.Plottables.Heatmap? lastImage)
    {
        var bottom = plot.Axes.Bottom; var left = plot.Axes.Left;
        // tick labels
        var fs = FigStyle.Current;
        Font(bottom.TickLabelStyle, null, ax, fs.FontSize); Font(left.TickLabelStyle, null, ax, fs.FontSize);
        if (PS(ax, "fontweight") == null) { bottom.TickLabelStyle.Bold = false; left.TickLabelStyle.Bold = false; }
        // title / labels
        foreach (var a in new SP.IAxis[] { bottom, left }) { a.Label.ForeColor = SP.Colors.Black; a.TickLabelStyle.ForeColor = SP.Colors.Black; a.FrameLineStyle.Color = SP.Colors.Black; a.MajorTickStyle.Color = SP.Colors.Black; }
        plot.Axes.Title.Label.ForeColor = SP.Colors.Black;
        Font(plot.Axes.Title.Label, ax.TitleObj, ax, fs.TitleSize);
        if (PS(ax.TitleObj, "fontweight") == null) plot.Axes.Title.Label.Bold = PS(ax, "titlefontweight") is string tw ? tw == "bold" : fs.TitleBold;
        Font(bottom.Label, ax.XLabelObj, ax, fs.LabelSize); Font(left.Label, ax.YLabelObj, ax, fs.LabelSize);
        if (PS(ax.XLabelObj, "fontweight") == null) bottom.Label.Bold = false;
        if (PS(ax.YLabelObj, "fontweight") == null) left.Label.Bold = false;
        // colours
        if (PC(ax, "xcolor") is Rgb xc) { var c = ToColor(xc); AxColor(bottom, c); plot.Axes.Top.FrameLineStyle.Color = c; }
        if (PC(ax, "ycolor") is Rgb yc) { var c = ToColor(yc); AxColor(left, c); plot.Axes.Right.FrameLineStyle.Color = c; }
        if (PC(ax, "color") is Rgb bg) plot.DataBackground.Color = ToColor(bg);
        // axis line width
        {
            float w = (float)Math.Max(0.25, PN(ax, "linewidth") ?? fs.AxesLineWidth);
            foreach (var a in new SP.IAxis[] { bottom, left, plot.Axes.Top, plot.Axes.Right }) a.FrameLineStyle.Width = w;
            bottom.MajorTickStyle.Width = left.MajorTickStyle.Width = w;
            bottom.MinorTickStyle.Width = left.MinorTickStyle.Width = w;
        }
        // ticks
        var tl = PV(ax, "ticklength");
        var dir = PS(ax, "tickdir") ?? fs.TickDir;
        foreach (var a in new SP.IAxis[] { bottom, left })
        {
            a.MajorTickStyle.Length = (float)fs.TickLength; a.MinorTickStyle.Length = (float)(fs.TickLength * 0.5);
            if (tl is { Length: > 0 }) { a.MajorTickStyle.Length = (float)(tl[0] * 300); a.MinorTickStyle.Length = (float)(tl[0] * 150); }
            bool minor = PS(ax, a == bottom ? "xminortick" : "yminortick") == "on" || (a == bottom ? ax.XLog : ax.YLog);
            if (!minor) a.MinorTickStyle.Length = 0;
            if (dir == "none") { a.MajorTickStyle.Length = 0; a.MinorTickStyle.Length = 0; }
            else if (dir == "in") { a.MajorTickStyle.Length = -Math.Abs(a.MajorTickStyle.Length); a.MinorTickStyle.Length = -Math.Abs(a.MinorTickStyle.Length); }
        }
        void Manual(SP.IAxis axis, string pos, string lab, bool log)
        {
            var tv = PV(ax, pos); var tlab = PL(ax, lab);
            if (tv == null && tlab == null) return;
            if (tv == null) return; // labels without positions: keep auto ticks
            var gen = new SP.TickGenerators.NumericManual();
            for (int i = 0; i < tv.Length; i++)
            {
                double at = log ? Math.Log10(tv[i]) : tv[i];
                string text = tlab != null ? (tlab.Length == 0 ? "" : Tex(tlab[i % tlab.Length])) : Renderer3D.Fmt(tv[i], true);
                gen.AddMajor(at, text);
            }
            axis.TickGenerator = gen;
        }
        // MATLAB-like sparse automatic ticks (ScottPlot's default density is much finer)
        if (!ax.XLog) bottom.TickGenerator = new SP.TickGenerators.NumericAutomatic { TickDensity = 0.3 };
        if (!ax.YLog) left.TickGenerator = new SP.TickGenerators.NumericAutomatic { TickDensity = 0.3 };
        Manual(bottom, "xtick", "xticklabel", ax.XLog);
        Manual(left, "ytick", "yticklabel", ax.YLog);
        if (PN(ax, "xticklabelrotation") is double xr) bottom.TickLabelStyle.Rotation = (float)-xr;
        // box
        if (!(PS(ax, "box") is string bx ? bx == "on" : ax.Box))
        {
            plot.Axes.Top.FrameLineStyle.Width = 0; plot.Axes.Right.FrameLineStyle.Width = 0;
        }
        // grids
        bool xg = PS(ax, "xgrid") is string xs0 ? xs0 == "on" : ax.Grid, yg = PS(ax, "ygrid") is string ys0 ? ys0 == "on" : ax.Grid;
        bool xmg = PS(ax, "xminorgrid") is string xm0 ? xm0 == "on" : ax.MinorGrid, ymg = PS(ax, "yminorgrid") is string ym0 ? ym0 == "on" : ax.MinorGrid;
        if (xg || yg || xmg || ymg)
        {
            plot.ShowGrid();
            plot.Grid.XAxisStyle.MajorLineStyle.Width = xg ? 0.5f : 0;
            plot.Grid.YAxisStyle.MajorLineStyle.Width = yg ? 0.5f : 0;
            if (xmg) { plot.Grid.XAxisStyle.MinorLineStyle.Width = 1; plot.Grid.XAxisStyle.MinorLineStyle.Color = SP.Colors.Black.WithAlpha(0.08); }
            if (ymg) { plot.Grid.YAxisStyle.MinorLineStyle.Width = 1; plot.Grid.YAxisStyle.MinorLineStyle.Color = SP.Colors.Black.WithAlpha(0.08); }
            if (PC(ax, "gridcolor") is Rgb gc) plot.Grid.MajorLineColor = ToColor(gc).WithAlpha(0.25);
            var gls = PS(ax, "gridlinestyle");
            if (gls is "--" or ":" or "-.") plot.Grid.MajorLinePattern = gls == ":" ? SP.LinePattern.Dotted : SP.LinePattern.Dashed;
        }
        // legend
        var lg = ax.LegendObj;
        if (lg is { Deleted: true }) plot.HideLegend();
        else if (lg != null && ax.LegendVisible)
        {
        }
        if (ax.LegendVisible && lg is not { Deleted: true })
        {
            var fname = PS(lg, "fontname") ?? PS(ax, "fontname");
            plot.Legend.FontName = ResolveFont(fname is null or "*" ? null : fname);
            var fsz = PN(lg, "fontsize") ?? (PN(ax, "fontsize") is double af ? af * fs.LegendSize / fs.FontSize : fs.LegendSize);
            plot.Legend.FontSize = (float)fsz;
            plot.Legend.SymbolWidth = (float)(fs.LegendSize * 2.2);
            plot.Legend.ShadowFillStyle.Color = SP.Colors.Transparent;
            plot.Legend.OutlineWidth = (float)fs.AxesLineWidth;
            if (PC(lg, "textcolor") is Rgb tc) plot.Legend.FontColor = ToColor(tc);
            if ((PS(lg, "box") ?? (fs.LegendBox ? "on" : "off")) == "off") { plot.Legend.OutlineWidth = 0; plot.Legend.BackgroundFillStyle.Color = SP.Colors.White.WithAlpha(0.0); }
            if (PC(lg, "color") is Rgb lbg) plot.Legend.BackgroundFillStyle.Color = ToColor(lbg);
            if (PS(lg, "orientation") == "horizontal") plot.Legend.Orientation = SP.Orientation.Horizontal;
        }
    }

    static void AddSeg(SP.Plot plot, double x0, double y0, double x1, double y1, SP.Color c)
    {
        var sc = plot.Add.Scatter(new[] { x0, x1 }, new[] { y0, y1 });
        sc.Color = c; sc.MarkerSize = 0; sc.LineWidth = (float)Math.Max(0.5, FigStyle.Current.AxesLineWidth);
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

    /// <summary>Draw a whole figure onto a canvas (vector where possible).</summary>
    public static void DrawTo(SkiaSharp.SKCanvas canvas, Figure f, int width, int height, bool vector)
    {
        var (rows, cols, cells) = Layout(f);
        var bg = f.Props.TryGetValue("color", out var bv) && bv is MatrixValue bm && !bm.IsChar && bm.Numel == 3
            ? new SkiaSharp.SKColor((byte)(bm.Re[0] * 255), (byte)(bm.Re[1] * 255), (byte)(bm.Re[2] * 255)) : SkiaSharp.SKColors.White;
        canvas.Clear(bg);
        double cw = (double)width / cols, ch = (double)height / rows;
        double scale = ExportScale(f, width);
        foreach (var (ax, r, c, rs, cs) in cells)
        {
            if (Renderer3D.Handles(ax))
            {
                Renderer3D.Draw(canvas, ax, f.Colormap, (float)(c * cw), (float)(r * ch), (float)(cw * cs), (float)(ch * rs), scale);
                if (!string.IsNullOrEmpty(ax.PanelLabel))
                {
                    canvas.Save(); canvas.Translate((float)(c * cw), (float)(r * ch)); canvas.Scale((float)scale);
                    new PanelLabelAction(ax.PanelLabel!, ResolveFont(null), (float)FigStyle.Current.PanelLabelSize).Draw(canvas, 0, 0);
                    canvas.Restore();
                }
                continue;
            }
            var plot = new SP.Plot();
            Apply(plot, ax, f.Colormap, scale);
            int w = (int)(cw * cs), h = (int)(ch * rs);
            float x0 = (float)(c * cw), y0 = (float)(r * ch);
            if (vector)
            {
                canvas.Save();
                canvas.Translate(x0, y0);
                canvas.ClipRect(new SkiaSharp.SKRect(0, 0, w, h));
                plot.RenderManager.Render(canvas, new SP.PixelRect(0, w, h, 0));
                canvas.Restore();
            }
            else
            {
                using var img = plot.GetImage(w, h);
                using var sk = SkiaSharp.SKImage.FromEncodedData(img.GetImageBytes(SP.ImageFormat.Png));
                canvas.DrawImage(sk, x0, y0);
            }
        }
        Renderer3D.DrawAnnotations(canvas, f, width, height, scale);
    }

    /// <summary>Render a whole figure to a file (png/jpg/bmp/webp raster, svg/pdf vector).</summary>
    public static void Save(Figure f, string path, int width, int height)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".pdf" || ext == ".eps" || ext == ".ps")
        {
            using var fs = File.Create(path);
            using var doc = SkiaSharp.SKDocument.CreatePdf(fs);
            var pc = doc.BeginPage(width, height);
            DrawTo(pc, f, width, height, true);
            doc.EndPage();
            doc.Close();
            return;
        }
        if (ext == ".svg")
        {
            using (var fs = File.Create(path))
            using (var sc = SkiaSharp.SKSvgCanvas.Create(new SkiaSharp.SKRect(0, 0, width, height), fs))
                DrawTo(sc, f, width, height, true);
            return;
        }
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(width, height));
        DrawTo(surface.Canvas, f, width, height, false);
        var fmt = ext switch { ".jpg" or ".jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg, ".bmp" => SkiaSharp.SKEncodedImageFormat.Bmp, ".webp" => SkiaSharp.SKEncodedImageFormat.Webp, _ => SkiaSharp.SKEncodedImageFormat.Png };
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
