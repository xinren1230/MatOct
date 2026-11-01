// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using SkiaSharp;
using SP = ScottPlot;

namespace MatRewrite.Plotting;

/// <summary>
/// Software 3-D renderer (orthographic projection, painter's algorithm) for surf/mesh/plot3.
/// Used both for file export and for the interactive WPF view, so both look identical.
/// </summary>
public static class Renderer3D
{
    public static bool Handles(Axes ax) => ax.Is3D || ax.Children.Any(c => c is SurfaceSeries);

    static SKColor Sk(Rgb c) => new((byte)Math.Round(c.R * 255), (byte)Math.Round(c.G * 255), (byte)Math.Round(c.B * 255));
    static SKColor Sk(SP.Color c) => new(c.R, c.G, c.B, c.A);

    abstract record Prim(double Depth);
    sealed record Quad(double Depth, SKPoint[] P, SKColor Fill, bool HasFill, SKColor Edge, bool HasEdge) : Prim(Depth);
    sealed record Seg(double Depth, SKPoint A, SKPoint B, SKColor Color, float Width, LineStyle Style) : Prim(Depth);
    sealed record Mark(double Depth, SKPoint P, SKColor Color, float Size, Marker M) : Prim(Depth);
    sealed record Txt(double Depth, SKPoint P, SKColor Color, float Size, string Text, string Align) : Prim(Depth);

    public static void Draw(SKCanvas cv, Axes ax, string colormap, float left, float top, float w, float h)
    {
        var cmap = FigureRenderer.Colormap(colormap);
        var (xl, yl) = MatRewrite.Core.Builtins.BuiltinLibrary.Limits(ax);
        var zl = MatRewrite.Core.Builtins.BuiltinLibrary.ZLimits(ax);
        double az = ax.Az * Math.PI / 180, el = ax.El * Math.PI / 180;
        double ca = Math.Cos(az), sa = Math.Sin(az), ce = Math.Cos(el), se = Math.Sin(el);
        bool flat = !ax.Is3D;

        using var titlePaint = new SKPaint { Color = SKColors.Black, TextSize = 14, IsAntialias = true, FakeBoldText = true, TextAlign = SKTextAlign.Center };
        using var labelPaint = new SKPaint { Color = SKColors.Black, TextSize = 12, IsAntialias = true, TextAlign = SKTextAlign.Center };
        using var tickPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 10, IsAntialias = true, TextAlign = SKTextAlign.Center };

        float titleH = ax.Title != "" ? 24 : 6;
        float cbW = ax.Colorbar ? 60 : 0;
        float pad = flat ? 50 : 55;
        var area = new SKRect(left + pad, top + titleH + 8, left + w - pad / 2 - cbW, top + h - pad);
        if (ax.Title != "") cv.DrawText(ax.Title, left + w / 2, top + 18, titlePaint);

        // normalized cube [-1,1]^3 (z squashed a bit as in MATLAB's default plot box)
        double zs = flat ? 0 : 0.8;
        (double x, double y, double d) Proj(double x, double y, double z)
        {
            double nx = 2 * (x - xl[0]) / (xl[1] - xl[0]) - 1, ny = 2 * (y - yl[0]) / (yl[1] - yl[0]) - 1, nz = (2 * (z - zl[0]) / (zl[1] - zl[0]) - 1) * zs;
            if (ax.XLog) nx = 2 * (Math.Log10(x) - Math.Log10(xl[0])) / (Math.Log10(xl[1]) - Math.Log10(xl[0])) - 1;
            double sx = ca * nx + sa * ny;
            double sy = -se * sa * nx + se * ca * ny + ce * nz;
            double d = ce * sa * nx - ce * ca * ny + se * nz;
            return (sx, sy, d);
        }
        // fit cube corners into the area
        double mnx = double.MaxValue, mxx = double.MinValue, mny = double.MaxValue, mxy = double.MinValue;
        foreach (var cx in xl) foreach (var cy in yl) foreach (var cz in zl)
                {
                    var p = Proj(cx, cy, cz);
                    mnx = Math.Min(mnx, p.x); mxx = Math.Max(mxx, p.x); mny = Math.Min(mny, p.y); mxy = Math.Max(mxy, p.y);
                }
        double sc = Math.Min(area.Width / Math.Max(mxx - mnx, 1e-9), area.Height / Math.Max(mxy - mny, 1e-9));
        if (flat && !(ax.Equal || ax.Square)) sc = 1; // independent x/y scaling for 2-D views
        double sxs = flat && !(ax.Equal || ax.Square) ? area.Width / Math.Max(mxx - mnx, 1e-9) : sc * ax.Zoom;
        double sys = flat && !(ax.Equal || ax.Square) ? area.Height / Math.Max(mxy - mny, 1e-9) : sc * ax.Zoom;
        double cxm = (mnx + mxx) / 2, cym = (mny + mxy) / 2;
        float midX = area.MidX, midY = area.MidY;
        SKPoint Scr(double x, double y, double z, out double depth)
        {
            var p = Proj(x, y, z); depth = p.d;
            return new SKPoint((float)(midX + (p.x - cxm) * sxs), (float)(midY - (p.y - cym) * sys));
        }
        SKPoint S(double x, double y, double z) => Scr(x, y, z, out _);

        // ---- axes box: back planes + grid ----
        using var boxPaint = new SKPaint { Color = new SKColor(150, 150, 150), StrokeWidth = 1, IsStroke = true, IsAntialias = true };
        using var gridPaint = new SKPaint { Color = new SKColor(225, 225, 225), StrokeWidth = 1, IsStroke = true, IsAntialias = true };
        var xt = Ticks(xl[0], xl[1], ax.XLog); var yt = Ticks(yl[0], yl[1], ax.YLog); var zt = flat ? Array.Empty<double>() : Ticks(zl[0], zl[1], false);
        // back x-plane: the x value whose face points away from the viewer
        double bx = Proj(xl[0], 0.5 * (yl[0] + yl[1]), 0.5 * (zl[0] + zl[1])).d < Proj(xl[1], 0.5 * (yl[0] + yl[1]), 0.5 * (zl[0] + zl[1])).d ? xl[0] : xl[1];
        double by = Proj(0.5 * (xl[0] + xl[1]), yl[0], 0.5 * (zl[0] + zl[1])).d < Proj(0.5 * (xl[0] + xl[1]), yl[1], 0.5 * (zl[0] + zl[1])).d ? yl[0] : yl[1];
        double bz = flat ? zl[0] : (ax.El >= 0 ? zl[0] : zl[1]);
        if (ax.Visible)
        {
            using var bg = new SKPaint { Color = SKColors.White, IsStroke = false };
            if (ax.Grid)
            {
                foreach (var t in xt)
                {
                    cv.DrawLine(S(t, yl[0], bz), S(t, yl[1], bz), gridPaint);
                    if (!flat) cv.DrawLine(S(t, by, zl[0]), S(t, by, zl[1]), gridPaint);
                }
                foreach (var t in yt)
                {
                    cv.DrawLine(S(xl[0], t, bz), S(xl[1], t, bz), gridPaint);
                    if (!flat) cv.DrawLine(S(bx, t, zl[0]), S(bx, t, zl[1]), gridPaint);
                }
                foreach (var t in zt) { cv.DrawLine(S(xl[0], by, t), S(xl[1], by, t), gridPaint); cv.DrawLine(S(bx, yl[0], t), S(bx, yl[1], t), gridPaint); }
            }
            // back edges
            cv.DrawLine(S(xl[0], by, bz), S(xl[1], by, bz), boxPaint);
            cv.DrawLine(S(bx, yl[0], bz), S(bx, yl[1], bz), boxPaint);
            if (!flat) { cv.DrawLine(S(bx, by, zl[0]), S(bx, by, zl[1]), boxPaint); }
            double fx = bx == xl[0] ? xl[1] : xl[0], fy = by == yl[0] ? yl[1] : yl[0];
            cv.DrawLine(S(xl[0], fy, bz), S(xl[1], fy, bz), boxPaint);
            cv.DrawLine(S(fx, yl[0], bz), S(fx, yl[1], bz), boxPaint);

            // ticks: x along the front y edge, y along the front x edge, z on the left vertical edge
            var center = S(0.5 * (xl[0] + xl[1]), 0.5 * (yl[0] + yl[1]), bz);
            void TickRow(double[] ticks, Func<double, SKPoint> at, string label, bool log)
            {
                if (ticks.Length == 0) return;
                SKPoint last = default, first = at(ticks[0]);
                foreach (var t in ticks)
                {
                    var p = at(t);
                    var dir = new SKPoint(p.X - center.X, p.Y - center.Y);
                    float len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                    var off = flat ? new SKPoint(0, 0) : new SKPoint(dir.X / Math.Max(len, 1) * 14, dir.Y / Math.Max(len, 1) * 14);
                    cv.DrawText(Fmt(t, log), p.X + off.X, p.Y + off.Y + 4, tickPaint);
                    last = p;
                }
                if (label != "")
                {
                    var mid = new SKPoint((first.X + last.X) / 2, (first.Y + last.Y) / 2);
                    var d = new SKPoint(mid.X - center.X, mid.Y - center.Y);
                    float l = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y);
                    cv.DrawText(label, mid.X + d.X / Math.Max(l, 1) * 34, mid.Y + d.Y / Math.Max(l, 1) * 34 + 4, labelPaint);
                }
            }
            if (flat)
            {
                tickPaint.TextAlign = SKTextAlign.Center;
                foreach (var t in xt) { var p = S(t, yl[0], bz); cv.DrawLine(p.X, p.Y, p.X, p.Y - 4, boxPaint); cv.DrawText(Fmt(t, ax.XLog), p.X, p.Y + 14, tickPaint); }
                if (ax.XLabel != "") cv.DrawText(ax.XLabel, area.MidX, area.Bottom + 34, labelPaint);
                tickPaint.TextAlign = SKTextAlign.Right;
                foreach (var t in yt) { var p = S(xl[0], t, bz); cv.DrawLine(p.X, p.Y, p.X + 4, p.Y, boxPaint); cv.DrawText(Fmt(t, ax.YLog), p.X - 4, p.Y + 4, tickPaint); }
                if (ax.YLabel != "") { cv.Save(); cv.RotateDegrees(-90, area.Left - 40, area.MidY); cv.DrawText(ax.YLabel, area.Left - 40, area.MidY, labelPaint); cv.Restore(); }
            }
            else
            {
                double fyEdge = by == yl[0] ? yl[1] : yl[0], fxEdge = bx == xl[0] ? xl[1] : xl[0];
                TickRow(xt, t => S(t, fyEdge, bz), ax.XLabel, ax.XLog);
                TickRow(yt, t => S(fxEdge, t, bz), ax.YLabel, ax.YLog);
                // z axis on the leftmost vertical edge
                var corners = new[] { (xl[0], yl[0]), (xl[0], yl[1]), (xl[1], yl[0]), (xl[1], yl[1]) };
                var zc = corners.OrderBy(c => S(c.Item1, c.Item2, zl[0]).X).First();
                tickPaint.TextAlign = SKTextAlign.Right;
                foreach (var t in zt) { var p = S(zc.Item1, zc.Item2, t); cv.DrawLine(p.X, p.Y, p.X - 4, p.Y, boxPaint); cv.DrawText(Fmt(t, false), p.X - 6, p.Y + 4, tickPaint); }
                tickPaint.TextAlign = SKTextAlign.Center;
                if (ax.ZLabel != "")
                {
                    var p0 = S(zc.Item1, zc.Item2, zl[0]); var p1 = S(zc.Item1, zc.Item2, zl[1]);
                    float lx = Math.Min(p0.X, p1.X) - 46, ly = (p0.Y + p1.Y) / 2;
                    cv.Save(); cv.RotateDegrees(-90, lx, ly); cv.DrawText(ax.ZLabel, lx, ly, labelPaint); cv.Restore();
                }
            }
        }

        // ---- primitives ----
        var prims = new List<Prim>();
        double cmin = double.PositiveInfinity, cmax = double.NegativeInfinity;
        foreach (var sf in ax.Children.OfType<SurfaceSeries>()) foreach (var v in sf.C) if (double.IsFinite(v)) { cmin = Math.Min(cmin, v); cmax = Math.Max(cmax, v); }
        if (ax.CLim != null) { cmin = ax.CLim[0]; cmax = ax.CLim[1]; }
        SKColor CM(double v) => Sk(cmap.GetColor(cmax > cmin ? Math.Clamp((v - cmin) / (cmax - cmin), 0, 1) : 0.5));
        foreach (var s in ax.Children)
        {
            switch (s)
            {
                case SurfaceSeries sf:
                {
                    int R = sf.Z.GetLength(0), C = sf.Z.GetLength(1);
                    for (int i = 0; i + 1 < R; i++)
                        for (int j = 0; j + 1 < C; j++)
                        {
                            var idx = new[] { (i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j) };
                            if (idx.Any(q => !double.IsFinite(sf.Z[q.Item1, q.Item2]))) continue;
                            double dsum = 0; var pts = new SKPoint[4];
                            for (int q = 0; q < 4; q++) { pts[q] = Scr(sf.X[idx[q].Item1, idx[q].Item2], sf.Y[idx[q].Item1, idx[q].Item2], flat ? zl[0] : sf.Z[idx[q].Item1, idx[q].Item2], out var d); dsum += d; }
                            double cval = sf.Shading == "faceted" || sf.Mesh ? sf.C[i, j] : (sf.C[i, j] + sf.C[i, j + 1] + sf.C[i + 1, j + 1] + sf.C[i + 1, j]) / 4;
                            var col = CM(cval);
                            if (sf.Mesh)
                                prims.Add(new Quad(dsum / 4, pts, sf.FaceColor is { } fc ? Sk(fc) : SKColors.White, true, sf.EdgeColor is { } ec ? Sk(ec) : col, true));
                            else
                                prims.Add(new Quad(dsum / 4, pts, sf.FaceColor is { } fc2 ? Sk(fc2) : col, true, sf.EdgeColor is { } ec2 ? Sk(ec2) : SKColors.Black, sf.Shading == "faceted"));
                        }
                    break;
                }
                case LineSeries l:
                {
                    var z = l.Z ?? new double[l.X.Length];
                    var col = Sk(l.Color);
                    for (int k = 0; k < l.X.Length; k++)
                    {
                        var p = Scr(l.X[k], l.Y[k], l.Z == null ? zl[0] : z[k], out var d);
                        if (l.Marker != Marker.None) prims.Add(new Mark(d + 1e-6, p, col, (float)l.MarkerSize, l.Marker));
                        if (k + 1 < l.X.Length && l.Style != LineStyle.None)
                        {
                            var q = Scr(l.X[k + 1], l.Y[k + 1], l.Z == null ? zl[0] : z[k + 1], out var d2);
                            if (double.IsFinite(p.X) && double.IsFinite(q.X)) prims.Add(new Seg((d + d2) / 2 + 1e-6, p, q, col, (float)Math.Max(1, l.LineWidth * 1.5), l.Style));
                        }
                    }
                    break;
                }
                case TextSeries t:
                {
                    var p = Scr(t.X, t.Y, t.Z ?? zl[0], out var d);
                    prims.Add(new Txt(1e9, p, Sk(t.Color), (float)(t.FontSize * 1.2), t.Text, t.HAlign));
                    break;
                }
            }
        }
        cv.Save();
        if (ax.Zoom > 1) cv.ClipRect(new SKRect(left, top, left + w, top + h));
        using var fill = new SKPaint { IsStroke = false, IsAntialias = true };
        using var edge = new SKPaint { IsStroke = true, StrokeWidth = 0.6f, IsAntialias = true };
        using var line = new SKPaint { IsStroke = true, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        using var txt = new SKPaint { IsAntialias = true };
        foreach (var p in prims.OrderBy(p => p.Depth))
        {
            switch (p)
            {
                case Quad q:
                {
                    using var path = new SKPath();
                    path.AddPoly(q.P, true);
                    if (q.HasFill) { fill.Color = q.Fill; cv.DrawPath(path, fill); }
                    if (q.HasEdge) { edge.Color = q.Edge; cv.DrawPath(path, edge); }
                    else if (q.HasFill) { edge.Color = q.Fill; edge.StrokeWidth = 0.5f; cv.DrawPath(path, edge); edge.StrokeWidth = 0.6f; }
                    break;
                }
                case Seg sgm:
                    line.Color = sgm.Color; line.StrokeWidth = sgm.Width;
                    line.PathEffect = sgm.Style switch
                    {
                        LineStyle.Dashed => SKPathEffect.CreateDash(new[] { 8f, 4f }, 0),
                        LineStyle.Dotted => SKPathEffect.CreateDash(new[] { 2f, 3f }, 0),
                        LineStyle.DashDot => SKPathEffect.CreateDash(new[] { 8f, 3f, 2f, 3f }, 0),
                        _ => null
                    };
                    cv.DrawLine(sgm.A, sgm.B, line);
                    break;
                case Mark m:
                    fill.Color = m.Color; line.Color = m.Color; line.StrokeWidth = 1; line.PathEffect = null;
                    if (m.M == Marker.Point) cv.DrawCircle(m.P, 2, fill);
                    else if (m.M is Marker.Plus) { cv.DrawLine(m.P.X - m.Size / 2, m.P.Y, m.P.X + m.Size / 2, m.P.Y, line); cv.DrawLine(m.P.X, m.P.Y - m.Size / 2, m.P.X, m.P.Y + m.Size / 2, line); }
                    else if (m.M is Marker.Cross or Marker.Star) { cv.DrawLine(m.P.X - m.Size / 2, m.P.Y - m.Size / 2, m.P.X + m.Size / 2, m.P.Y + m.Size / 2, line); cv.DrawLine(m.P.X - m.Size / 2, m.P.Y + m.Size / 2, m.P.X + m.Size / 2, m.P.Y - m.Size / 2, line); }
                    else if (m.M is Marker.Square) cv.DrawRect(m.P.X - m.Size / 2, m.P.Y - m.Size / 2, m.Size, m.Size, line);
                    else cv.DrawCircle(m.P, m.Size / 2, line);
                    break;
                case Txt t:
                    txt.Color = t.Color; txt.TextSize = t.Size;
                    txt.TextAlign = t.Align switch { "center" => SKTextAlign.Center, "right" => SKTextAlign.Right, _ => SKTextAlign.Left };
                    cv.DrawText(t.Text, t.P.X, t.P.Y, txt);
                    break;
            }
        }
        cv.Restore();

        if (ax.Colorbar && cmax >= cmin && double.IsFinite(cmin))
        {
            float x0 = left + w - cbW + 10, y0 = area.Top, bh = area.Height;
            for (int k = 0; k < (int)bh; k++)
            {
                fill.Color = Sk(cmap.GetColor(1 - k / bh));
                cv.DrawRect(x0, y0 + k, 14, 1.5f, fill);
            }
            cv.DrawRect(x0, y0, 14, bh, boxPaint);
            tickPaint.TextAlign = SKTextAlign.Left;
            foreach (var t in Ticks(cmin, cmax, false))
            {
                float yy = (float)(y0 + bh * (1 - (t - cmin) / Math.Max(cmax - cmin, 1e-12)));
                cv.DrawText(Fmt(t, false), x0 + 18, yy + 4, tickPaint);
            }
        }
    }

    static double[] Ticks(double lo, double hi, bool log)
    {
        if (log)
        {
            var r = new List<double>();
            for (double e = Math.Ceiling(Math.Log10(lo) - 1e-9); e <= Math.Floor(Math.Log10(hi) + 1e-9); e++) r.Add(Math.Pow(10, e));
            return r.ToArray();
        }
        double span = hi - lo;
        if (!(span > 0)) return new[] { lo };
        double raw = span / 5, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / mag;
        double step = (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
        var list = new List<double>();
        for (double v = Math.Ceiling(lo / step - 1e-9) * step; v <= hi + step * 1e-9; v += step) list.Add(Math.Abs(v) < step * 1e-9 ? 0 : v);
        return list.ToArray();
    }

    public static string Fmt(double v, bool log) => log ? (Math.Abs(Math.Log10(v)) >= 4 ? $"1e{Math.Log10(v):0}" : v.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)) : v.ToString("G5", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Render one axes to PNG bytes (used by the WPF interactive view).</summary>
    public static byte[] RenderPng(Axes ax, string colormap, int w, int h)
    {
        using var surf = SKSurface.Create(new SKImageInfo(Math.Max(w, 10), Math.Max(h, 10)));
        surf.Canvas.Clear(SKColors.White);
        Draw(surf.Canvas, ax, colormap, 0, 0, w, h);
        using var img = surf.Snapshot();
        using var d = img.Encode(SKEncodedImageFormat.Png, 100);
        return d.ToArray();
    }

    /// <summary>Draw figure annotations (normalized figure units, origin bottom-left).</summary>
    public static void DrawAnnotations(SKCanvas cv, Figure f, float w, float h)
    {
        using var pen = new SKPaint { IsStroke = true, IsAntialias = true, StrokeWidth = 1.2f };
        using var txt = new SKPaint { IsAntialias = true };
        SKPoint P(double x, double y) => new((float)(x * w), (float)((1 - y) * h));
        void Head(SKPoint from, SKPoint to)
        {
            double ang = Math.Atan2(to.Y - from.Y, to.X - from.X);
            for (int s = -1; s <= 1; s += 2)
            {
                double a2 = ang + Math.PI + s * 0.4;
                cv.DrawLine(to, new SKPoint(to.X + (float)(10 * Math.Cos(a2)), to.Y + (float)(10 * Math.Sin(a2))), pen);
            }
        }
        foreach (var an in f.Annotations)
        {
            pen.Color = Sk(an.Color); txt.Color = Sk(an.Color); txt.TextSize = (float)(an.FontSize * 1.2);
            if (an.Position != null)
            {
                var p = an.Position;
                var r = new SKRect(P(p[0], p[1] + p[3]).X, P(p[0], p[1] + p[3]).Y, P(p[0] + p[2], p[1]).X, P(p[0] + p[2], p[1]).Y);
                if (an.Kind == "ellipse") cv.DrawOval(r, pen);
                else if (an.Kind == "rectangle" || an.Kind == "textbox") cv.DrawRect(r, pen);
                if (an.Text != "") { int k = 0; foreach (var ln in an.Text.Split('\n')) cv.DrawText(ln, r.Left + 4, r.Top + txt.TextSize + 2 + k++ * txt.TextSize * 1.2f, txt); }
                continue;
            }
            var a = P(an.X[0], an.Y[0]); var b = P(an.X[1], an.Y[1]);
            cv.DrawLine(a, b, pen);
            if (an.Kind is "arrow" or "textarrow" or "doublearrow") Head(a, b);
            if (an.Kind == "doublearrow") Head(b, a);
            if (an.Text != "") { txt.TextAlign = SKTextAlign.Center; cv.DrawText(an.Text, a.X, a.Y + (a.Y > b.Y ? txt.TextSize : -4), txt); txt.TextAlign = SKTextAlign.Left; }
        }
    }
}
