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
    public static bool Handles(Axes ax) => ax.Is3D || ax.Children.Any(c => c is SurfaceSeries || c is ContourSeries { Contour3: true } || c is ContourSeries { ZPlane: not null });

    /// <summary>Above this many quads per surface the grid is decimated for interactive speed.</summary>
    public static int MaxQuads = 90_000;

    static SKColor Sk(Rgb c) => new((byte)Math.Round(c.R * 255), (byte)Math.Round(c.G * 255), (byte)Math.Round(c.B * 255));
    static SKColor Sk(SP.Color c) => new(c.R, c.G, c.B, c.A);

    abstract record Prim(double Depth);
    sealed record Quad(double Depth, SKPoint[] P, SKColor Fill, bool HasFill, SKColor Edge, bool HasEdge) : Prim(Depth)
    {
        public SKColor[]? Vertex; // gouraud vertex colours
    }
    sealed record Seg(double Depth, SKPoint A, SKPoint B, SKColor Color, float Width, LineStyle Style) : Prim(Depth);
    sealed record Mark(double Depth, SKPoint P, SKColor Color, float Size, Marker M) : Prim(Depth);
    sealed record Txt(double Depth, SKPoint P, SKColor Color, float Size, string Text, string Align) : Prim(Depth);

    /// <summary>Canvas zoom for the legacy px-based 3D/annotation layout at <paramref name="pxPerPt"/> device pixels per point.</summary>
    static float Zoom(double pxPerPt) => (float)Math.Max(0.3, (pxPerPt > 0 ? pxPerPt : FigStyle.Current.ScreenPxPerPt) * 0.75);

    public static void Draw(SKCanvas cv, Axes ax, string colormap, float left, float top, float w, float h, double pxPerPt = 0)
    {
        float k = Zoom(pxPerPt);
        cv.Save(); cv.Translate(left, top); cv.Scale(k);
        DrawCore(cv, ax, colormap, 0, 0, w / k, h / k);
        cv.Restore();
    }

    static void DrawCore(SKCanvas cv, Axes ax, string colormap, float left, float top, float w, float h)
    {
        var cmap = FigureRenderer.Colormap(colormap);
        var (xl, yl) = MatRewrite.Core.Builtins.BuiltinLibrary.Limits(ax);
        var zl = MatRewrite.Core.Builtins.BuiltinLibrary.ZLimits(ax);
        double az = ax.Az * Math.PI / 180, el = ax.El * Math.PI / 180;
        double ca = Math.Cos(az), sa = Math.Sin(az), ce = Math.Cos(el), se = Math.Sin(el);
        bool flat = !ax.Is3D;

        using var titlePaint = new SKPaint { Color = SKColors.Black, TextSize = 12, IsAntialias = true, FakeBoldText = FigStyle.Current.TitleBold, Typeface = SKTypeface.FromFamilyName(FigureRenderer.ResolveFont(null)), TextAlign = SKTextAlign.Center };
        using var labelPaint = new SKPaint { Color = SKColors.Black, TextSize = 11, IsAntialias = true, Typeface = SKTypeface.FromFamilyName(FigureRenderer.ResolveFont(null)), TextAlign = SKTextAlign.Center };
        using var tickPaint = new SKPaint { Color = new SKColor(40, 40, 40), TextSize = 10, IsAntialias = true, Typeface = SKTypeface.FromFamilyName(FigureRenderer.ResolveFont(null)), TextAlign = SKTextAlign.Center };

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
        if (!double.IsFinite(cmin))
            foreach (var cs in ax.Children.OfType<ContourSeries>()) foreach (var v in cs.Levels) { cmin = Math.Min(cmin, v); cmax = Math.Max(cmax, v); }
        if (ax.CLim != null) { cmin = ax.CLim[0]; cmax = ax.CLim[1]; }
        // colormap lookup table (256 entries) avoids per-quad colormap evaluation
        var lut = new SKColor[256];
        for (int k = 0; k < 256; k++) lut[k] = Sk(cmap.GetColor(k / 255.0));
        SKColor CM(double v) => cmax > cmin ? lut[(int)Math.Round(Math.Clamp((v - cmin) / (cmax - cmin), 0, 1) * 255)] : lut[128];

        // lights: directions in normalized (projected-space) coordinates
        var lights = ax.Children.OfType<LightObject>().Where(l => l.Visible).Select(l =>
        {
            double laz, lel;
            if (l.CameraRelative) { laz = ax.Az + l.Az; lel = ax.El + l.El; } else { laz = l.Az; lel = l.El; }
            double ar = laz * Math.PI / 180, er = lel * Math.PI / 180;
            // same convention as the view direction (ce*sa, -ce*ca, se)
            return (x: Math.Cos(er) * Math.Sin(ar), y: -Math.Cos(er) * Math.Cos(ar), z: Math.Sin(er), c: l.Color);
        }).ToList();
        double vx = ce * sa, vy = -ce * ca, vz = se;
        (double nx, double ny, double nz) Norm3(double x, double y, double z) =>
            (2 * (x - xl[0]) / (xl[1] - xl[0]) - 1, 2 * (y - yl[0]) / (yl[1] - yl[0]) - 1, (2 * (z - zl[0]) / (zl[1] - zl[0]) - 1) * (flat ? 1 : 0.8));
        SKColor Shade(SKColor c, double nx, double ny, double nz)
        {
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-15) return c;
            nx /= len; ny /= len; nz /= len;
            if (nx * vx + ny * vy + nz * vz < 0) { nx = -nx; ny = -ny; nz = -nz; } // two-sided
            double r = 0.3 * c.Red, g = 0.3 * c.Green, b = 0.3 * c.Blue;
            foreach (var L in lights)
            {
                double diff = Math.Max(0, nx * L.x + ny * L.y + nz * L.z);
                // Blinn-Phong specular
                double hx = L.x + vx, hy = L.y + vy, hz = L.z + vz, hl = Math.Sqrt(hx * hx + hy * hy + hz * hz);
                double spec = hl > 0 ? Math.Pow(Math.Max(0, (nx * hx + ny * hy + nz * hz) / hl), 10) * 0.1 * 255 : 0;
                r += 0.6 * diff * c.Red * L.c.R + spec; g += 0.6 * diff * c.Green * L.c.G + spec; b += 0.6 * diff * c.Blue * L.c.B + spec;
            }
            return new SKColor((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255), c.Alpha);
        }
        foreach (var s in ax.Children)
        {
            switch (s)
            {
                case SurfaceSeries sf:
                {
                    int R = sf.Z.GetLength(0), C = sf.Z.GetLength(1);
                    // decimate very large grids (keeps first/last rows and columns)
                    int stride = 1;
                    while ((long)((R - 1) / stride) * ((C - 1) / stride) > MaxQuads) stride++;
                    int[] ri = Enumerable.Range(0, (R - 1) / stride + 1).Select(i => i * stride).ToList().Append(R - 1).Distinct().ToArray();
                    int[] cj = Enumerable.Range(0, (C - 1) / stride + 1).Select(j => j * stride).ToList().Append(C - 1).Distinct().ToArray();
                    int nr = ri.Length, nc = cj.Length;
                    // project every vertex once
                    var P = new SKPoint[nr, nc]; var Dp = new double[nr, nc]; var ok = new bool[nr, nc];
                    for (int a = 0; a < nr; a++)
                        for (int b = 0; b < nc; b++)
                        {
                            int i = ri[a], j = cj[b];
                            ok[a, b] = double.IsFinite(sf.Z[i, j]);
                            if (ok[a, b]) P[a, b] = Scr(sf.X[i, j], sf.Y[i, j], flat ? zl[0] : sf.Z[i, j], out Dp[a, b]);
                        }
                    bool lit = lights.Count > 0 && sf.FaceLighting != "none" && ax.Lighting != "none" && !sf.Mesh && !flat;
                    bool gouraud = lit && sf.FaceLighting == "gouraud";
                    byte alpha = (byte)Math.Round(sf.FaceAlpha * 255);
                    // vertex normals for gouraud shading
                    (double, double, double) VNormal(int a, int b)
                    {
                        int a0 = Math.Max(a - 1, 0), a1 = Math.Min(a + 1, nr - 1), b0 = Math.Max(b - 1, 0), b1 = Math.Min(b + 1, nc - 1);
                        var pA0 = Norm3(sf.X[ri[a0], cj[b]], sf.Y[ri[a0], cj[b]], sf.Z[ri[a0], cj[b]]); var pA1 = Norm3(sf.X[ri[a1], cj[b]], sf.Y[ri[a1], cj[b]], sf.Z[ri[a1], cj[b]]);
                        var pB0 = Norm3(sf.X[ri[a], cj[b0]], sf.Y[ri[a], cj[b0]], sf.Z[ri[a], cj[b0]]); var pB1 = Norm3(sf.X[ri[a], cj[b1]], sf.Y[ri[a], cj[b1]], sf.Z[ri[a], cj[b1]]);
                        double ux = pA1.nx - pA0.nx, uy = pA1.ny - pA0.ny, uz = pA1.nz - pA0.nz, wx = pB1.nx - pB0.nx, wy = pB1.ny - pB0.ny, wz = pB1.nz - pB0.nz;
                        return (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx);
                    }
                    for (int a = 0; a + 1 < nr; a++)
                        for (int b = 0; b + 1 < nc; b++)
                        {
                            if (!(ok[a, b] && ok[a, b + 1] && ok[a + 1, b + 1] && ok[a + 1, b])) continue;
                            int i = ri[a], j = cj[b], i1 = ri[a + 1], j1 = cj[b + 1];
                            var pts = new[] { P[a, b], P[a, b + 1], P[a + 1, b + 1], P[a + 1, b] };
                            double depth = (Dp[a, b] + Dp[a, b + 1] + Dp[a + 1, b + 1] + Dp[a + 1, b]) / 4;
                            double cval = sf.Shading == "faceted" || sf.Mesh ? sf.C[i, j] : (sf.C[i, j] + sf.C[i, j1] + sf.C[i1, j1] + sf.C[i1, j]) / 4;
                            var col = CM(cval);
                            if (sf.Mesh)
                            {
                                prims.Add(new Quad(depth, pts, sf.FaceColor is { } fc ? Sk(fc) : SKColors.White, true, sf.EdgeColor is { } ec ? Sk(ec) : col, true));
                                continue;
                            }
                            var face = sf.FaceColor is { } fc2 ? Sk(fc2) : col;
                            SKColor[]? vcols = null;
                            if (lit)
                            {
                                if (gouraud)
                                {
                                    var corners = new[] { (a, b), (a, b + 1), (a + 1, b + 1), (a + 1, b) };
                                    vcols = new SKColor[4];
                                    for (int q = 0; q < 4; q++)
                                    {
                                        var (qa, qb) = corners[q];
                                        var baseCol = sf.FaceColor is { } fc3 ? Sk(fc3) : sf.Shading == "interp" ? CM(sf.C[ri[qa], cj[qb]]) : face;
                                        var nrm = VNormal(qa, qb);
                                        vcols[q] = Shade(baseCol, nrm.Item1, nrm.Item2, nrm.Item3).WithAlpha(alpha);
                                    }
                                }
                                else
                                {
                                    var p0 = Norm3(sf.X[i, j], sf.Y[i, j], sf.Z[i, j]); var p1 = Norm3(sf.X[i, j1], sf.Y[i, j1], sf.Z[i, j1]); var p3 = Norm3(sf.X[i1, j], sf.Y[i1, j], sf.Z[i1, j]);
                                    double ux = p1.nx - p0.nx, uy = p1.ny - p0.ny, uz = p1.nz - p0.nz, wx = p3.nx - p0.nx, wy = p3.ny - p0.ny, wz = p3.nz - p0.nz;
                                    face = Shade(face, uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx);
                                }
                            }
                            var edgeCol = sf.EdgeColor is { } ec2 ? Sk(ec2) : SKColors.Black;
                            if (sf.EdgeAlpha < 1) edgeCol = edgeCol.WithAlpha((byte)Math.Round(sf.EdgeAlpha * 255));
                            prims.Add(new Quad(depth, pts, face.WithAlpha(alpha), sf.FaceAlpha > 0, edgeCol, sf.Shading == "faceted" && stride == 1) { Vertex = vcols });
                        }
                    break;
                }
                case ContourSeries cs when cs.Contour3 || cs.ZPlane != null:
                {
                    foreach (var (level, lx, ly) in cs.Lines)
                    {
                        var col = cs.LineColor is { } lc ? Sk(lc) : CM(level);
                        double zz = cs.Contour3 ? level : cs.ZPlane!.Value;
                        for (int k = 0; k + 1 < lx.Length; k++)
                        {
                            if (!double.IsFinite(lx[k]) || !double.IsFinite(lx[k + 1])) continue;
                            var p = Scr(lx[k], ly[k], zz, out var d1); var q = Scr(lx[k + 1], ly[k + 1], zz, out var d2);
                            prims.Add(new Seg((d1 + d2) / 2 + 1e-6, p, q, col, (float)Math.Max(1, cs.LineWidth * 1.5), LineStyle.Solid));
                        }
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
        bool big = prims.Count > 20_000;
        if (big) { fill.IsAntialias = false; edge.IsAntialias = false; }
        var keys = new double[prims.Count];
        for (int k = 0; k < keys.Length; k++) keys[k] = prims[k].Depth;
        var order = prims.ToArray();
        Array.Sort(keys, order); // stable enough for painter's algorithm
        using var quadPath = new SKPath();
        foreach (var p in order)
        {
            switch (p)
            {
                case Quad q:
                {
                    var path = quadPath;
                    path.Rewind();
                    path.AddPoly(q.P, true);
                    if (q.Vertex != null && q.HasFill)
                    {
                        var tri = new[] { q.P[0], q.P[1], q.P[2], q.P[0], q.P[2], q.P[3] };
                        var tc = new[] { q.Vertex[0], q.Vertex[1], q.Vertex[2], q.Vertex[0], q.Vertex[2], q.Vertex[3] };
                        fill.Color = SKColors.White;
                        cv.DrawVertices(SKVertexMode.Triangles, tri, tc, fill);
                        if (q.HasEdge) { edge.Color = q.Edge; cv.DrawPath(path, edge); }
                        break;
                    }
                    if (q.HasFill) { fill.Color = q.Fill; cv.DrawPath(path, fill); }
                    if (q.HasEdge) { edge.Color = q.Edge; cv.DrawPath(path, edge); }
                    else if (q.HasFill && q.Fill.Alpha == 255) { edge.Color = q.Fill; edge.StrokeWidth = 0.5f; cv.DrawPath(path, edge); edge.StrokeWidth = 0.6f; }
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
    public static void DrawAnnotations(SKCanvas cv, Figure f, float w, float h, double pxPerPt = 0)
    {
        float k = Zoom(pxPerPt);
        cv.Save(); cv.Scale(k);
        DrawAnnotationsCore(cv, f, w / k, h / k);
        cv.Restore();
    }

    static void DrawAnnotationsCore(SKCanvas cv, Figure f, float w, float h)
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
                else if (an.Kind == "rectangle" || an.Kind == "textbox")
                {
                    bool noLine = an.Props.TryGetValue("linestyle", out var lsv) && lsv is MatRewrite.Core.Runtime.MatrixValue lsm && lsm.IsChar && lsm.AsString() == "none";
                    if (!noLine) cv.DrawRect(r, pen);
                }
                bool centered = an.Props.TryGetValue("horizontalalignment", out var hav) && hav is MatRewrite.Core.Runtime.MatrixValue ham && ham.IsChar && ham.AsString() == "center";
                if (centered) txt.TextAlign = SKTextAlign.Center;
                if (an.Text != "") { int k = 0; foreach (var ln in an.Text.Split('\n')) cv.DrawText(ln, centered ? r.MidX : r.Left + 4, r.Top + txt.TextSize + 2 + k++ * txt.TextSize * 1.2f, txt); }
                txt.TextAlign = SKTextAlign.Left;
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
