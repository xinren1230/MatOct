// MatRewrite - GPL-3.0-or-later
// bar / barh with Octave's object model: one hggroup per column (xdata, ydata, barwidth, barlayout,
// horizontal, basevalue, baseline, bargroup), each with a patch child and a shared hidden baseline line.
// Geometry follows scripts/plot/draw/private/__bar__.m.
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        /// <summary>__bar__ geometry: xb/yb as [4, ngrp, nbars] (column-major).</summary>
        static (double[] xb, double[] yb) BarGeometry(double[] x, double[,] y, double width, bool group, bool stacked, bool ishist, bool histc, double bv)
        {
            int ngrp = x.Length, nbars = y.GetLength(1);
            double cwidth, gwidth;
            if (ishist) { cwidth = 1; gwidth = nbars == 1 ? 1 : width * width; }
            else if (nbars == 1) { cwidth = 1; gwidth = width; }
            else { cwidth = gwidth = width; }
            double cutoff = 1;
            if (ngrp > 1) { cutoff = double.PositiveInfinity; for (int i = 1; i < ngrp; i++) cutoff = Math.Min(cutoff, x[i] - x[i - 1]); cutoff /= 2; }
            double gdelta = cutoff * gwidth / nbars;
            double cdelta = group ? (1 - ((1 - cwidth) / 2)) * gdelta : cutoff * gwidth;
            var xb = new double[4 * ngrp * nbars]; var yb = new double[4 * ngrp * nbars];
            for (int b = 0; b < nbars; b++)
            {
                double off = 0, adj = 0;
                if (group)
                {
                    off = ishist && histc ? 2 * cdelta * b + cdelta : 2 * cdelta * (b - (nbars - 1) / 2.0);
                    adj = (1 - cwidth) / 2 * (2 * gdelta);
                }
                for (int g = 0; g < ngrp; g++)
                {
                    int o = 4 * (g + ngrp * b);
                    double x1 = x[g] - cdelta, x2 = x[g] + cdelta;
                    xb[o] = x1 + off + adj; xb[o + 1] = x1 + off + adj; xb[o + 2] = x2 + off - adj; xb[o + 3] = x2 + off - adj;
                    double y0, y1;
                    if (group) { y0 = bv; y1 = y[g, b]; }
                    else
                    {
                        bool anyNeg = false; for (int k = 0; k < nbars; k++) if (y[g, k] < 0) anyNeg = true;
                        if (anyNeg)
                        {
                            double p = bv, q = bv, p0 = 0, n0 = 0;
                            double cp = 0, cn = 0;
                            for (int k = 0; k <= b; k++)
                            {
                                p0 = cp; n0 = cn;
                                if (y[g, k] >= 0) cp += y[g, k]; else cn += y[g, k];
                            }
                            bool pos = y[g, b] >= 0;
                            y1 = pos ? cp : cn;
                            y0 = b == 0 ? bv : (pos ? p0 : n0);
                            _ = p; _ = q;
                        }
                        else
                        {
                            double c = 0, c0 = 0;
                            for (int k = 0; k <= b; k++) { c0 = c; c += y[g, k]; }
                            y1 = c; y0 = b == 0 ? bv : c0;
                        }
                    }
                    yb[o] = y0; yb[o + 1] = y1; yb[o + 2] = y1; yb[o + 3] = y0;
                }
            }
            return (xb, yb);
        }

        static void BarRecompute(Interpreter it, BarSeries first)
        {
            var mem = first.Members ?? new List<BarSeries> { first };
            var x = first.X; int ngrp = x.Length, nb = mem.Count;
            var y = new double[ngrp, nb];
            for (int b = 0; b < nb; b++) for (int g = 0; g < ngrp; g++) y[g, b] = g < mem[b].Y.Length ? mem[b].Y[g] : 0;
            bool group = first.Layout != "stacked";
            var (xb, yb) = BarGeometry(x, y, first.Width, group, !group, false, false, EffBase(first));
            for (int b = 0; b < nb; b++)
            {
                var s = mem[b];
                s.X1 = new double[ngrp]; s.X2 = new double[ngrp]; s.Y0 = new double[ngrp]; s.Y1 = new double[ngrp];
                var XB = new MatrixValue(4, ngrp); var YB = new MatrixValue(4, ngrp);
                for (int g = 0; g < ngrp; g++)
                {
                    int o = 4 * (g + ngrp * b);
                    s.X1[g] = xb[o]; s.X2[g] = xb[o + 2]; s.Y0[g] = yb[o]; s.Y1[g] = yb[o + 1];
                    for (int r = 0; r < 4; r++) { XB.Re[r + 4 * g] = xb[o + r]; YB.Re[r + 4 * g] = yb[o + r]; }
                }
                s.GroupIndex = b; s.GroupCount = nb;
                if (s.Patch is PatchSeries ps)
                {
                    var (px, py) = s.Horizontal ? (YB, XB) : (XB, YB);
                    ps.Props["xdata"] = px; ps.Props["ydata"] = py;
                    var faces = new MatrixValue(ngrp, 4); var verts = new MatrixValue(4 * ngrp, 2);
                    for (int g = 0; g < ngrp; g++)
                        for (int r = 0; r < 4; r++)
                        {
                            faces.Re[g + ngrp * r] = 4 * g + r + 1;
                            verts.Re[4 * g + r] = px.Re[r + 4 * g]; verts.Re[4 * g + r + 4 * ngrp] = py.Re[r + 4 * g];
                        }
                    ps.Props["faces"] = faces; ps.Props["vertices"] = verts;
                    ps.FaceColor = s.Color; ps.Props["facecolor"] = Rgbv(s.Color);
                }
            }
            it.Graphics.Touch(first.Parent.Figure);
        }

        static Value[] BarFn(Interpreter it, Value[] a, int nout, bool horiz, string fn)
        {
            if (a.Length == 0) throw new OctaveException($"Invalid call to {fn}");
            if (!(a[0] is MatrixValue c0 && !c0.IsChar)) throw new OctaveException($"{fn}: Y must be numeric");
            MatrixValue xm, ym; int idx;
            if (a.Length > 1 && a[1] is MatrixValue m1 && !m1.IsChar)
            {
                var m0 = M(a[0], fn);
                if (m1.Numel == 1 && m0.Numel != 1) { ym = m0; xm = null!; idx = 1; }
                else
                {
                    if (!(m0.R == 1 || m0.C == 1) || m0.ND != null) throw new OctaveException($"{fn}: X must be a vector");
                    if (m0.Re.Distinct().Count() != m0.Numel) throw new OctaveException($"{fn}: X vector values must be unique");
                    xm = m0; ym = m1; idx = 2;
                }
            }
            else { ym = M(a[0], fn); xm = null!; idx = 1; }
            double width = 0.8, bv = 0; bool group = true, stacked = false; bool? histc = null;
            Rgb? col = null; var rest = new List<Value>();
            while (idx < a.Length)
            {
                var v = a[idx];
                if (IsString(v))
                {
                    var str = S(v, fn).ToLowerInvariant();
                    if (str == "grouped") { group = true; idx++; continue; }
                    if (str == "stacked") { stacked = true; group = false; idx++; continue; }
                    if (str == "histc") { group = true; histc = true; idx++; continue; }
                    if (str == "hist") { group = true; histc = false; idx++; continue; }
                    if (col == null && ParseFmt(str, out _, out _, out var lc) && lc != null && !KnownBarProp(str)) { col = lc; idx++; continue; }
                    if (str == "basevalue" && idx + 1 < a.Length) { bv = D(a[idx + 1], fn); idx += 2; continue; }
                    if (str == "facecolor" && idx + 1 < a.Length) { col = ParseColor(a[idx + 1], fn); idx += 2; continue; }
                    if (idx + 1 >= a.Length) { rest.Add(v); idx++; continue; }
                    rest.Add(v); rest.Add(a[idx + 1]); idx += 2; continue;
                }
                if (v is MatrixValue sm && sm.Numel == 1) { width = sm.Re[0]; idx++; continue; }
                if (idx + 1 >= a.Length) { rest.Add(v); idx++; continue; }
                rest.Add(v); rest.Add(a[idx + 1]); idx += 2;
            }
            bool vec = ym.R == 1 || ym.C == 1;
            int ngrp = xm != null ? xm.Numel : (vec ? ym.Numel : ym.R);
            int nb = vec && (xm == null || ym.Numel == ngrp) ? 1 : ym.C;
            if (xm == null || !vec) { if ((vec ? ym.Numel : ym.R) != ngrp) throw new OctaveException($"{fn}: length of X and Y must be equal"); }
            else if (ym.Numel != ngrp) throw new OctaveException($"{fn}: length of X and Y must be equal");
            var Y = new double[ngrp, nb];
            for (int g = 0; g < ngrp; g++) for (int b = 0; b < nb; b++) Y[g, b] = vec ? ym.Re[g] : ym.Re[g + ngrp * b];
            var xs = xm?.Re.ToArray() ?? Enumerable.Range(1, ngrp).Select(i => (double)i).ToArray();
            bool ishist = histc != null;
            var (xb, yb) = BarGeometry(xs, Y, width, group, stacked, ishist, histc == true, bv);
            if (nout >= 2)
            {
                var XB = new MatrixValue(4, ngrp * nb, xb); XB.SetDims(new[] { 4, ngrp, nb });
                var YB = new MatrixValue(4, ngrp * nb, yb); YB.SetDims(new[] { 4, ngrp, nb });
                return horiz ? Out(YB, XB) : Out(XB, YB);
            }
            var ax = PrepareAxes(it);
            var handles = new List<double>();
            if (ishist)
            {
                for (int b = 0; b < nb; b++)
                {
                    var ps = (PatchSeries)MakeGo(it, "patch", ax);
                    var X = new MatrixValue(4, ngrp, xb.Skip(4 * ngrp * b).Take(4 * ngrp).ToArray());
                    var YY = new MatrixValue(4, ngrp, yb.Skip(4 * ngrp * b).Take(4 * ngrp).ToArray());
                    ps.Props["xdata"] = horiz ? YY : X; ps.Props["ydata"] = horiz ? X : YY;
                    RebuildPatch(ps, false);
                    ps.Props["cdata"] = Num(b + 1); ps.Props["facecolor"] = Str("flat");
                    ps.FaceColor = col ?? ax.NextColor();
                    if (rest.Count > 0) SetProps(it, ps, rest.ToArray(), fn);
                    handles.Add(ps.Handle);
                }
            }
            else
            {
                var mem = new List<BarSeries>();
                for (int b = 0; b < nb; b++)
                {
                    var s = it.Graphics.Add(ax, new BarSeries
                    {
                        X = xs, Y = Enumerable.Range(0, ngrp).Select(g => Y[g, b]).ToArray(), Width = width,
                        Color = col ?? ax.NextColor(), Horizontal = horiz, Layout = group ? "grouped" : "stacked", BaseValue = bv, Members = mem
                    });
                    mem.Add(s);
                    var ps = new PatchSeries { Group = s, FaceColor = s.Color };
                    it.Graphics.Add(ax, ps); s.Patch = ps;
                }
                var bl = it.Graphics.Register(new GenericObject { Type = "line", Parent = ax, Group = mem[0] });
                ax.Children.Add(bl);
                bl.Props["handlevisibility"] = Str("off"); bl.Props["color"] = MatrixValue.Row(new double[] { 0, 0, 0 }); bl.Props["linestyle"] = Str("-");
                bl.Props["__bar__"] = Num(mem[0].Handle);
                foreach (var s in mem) s.Baseline = bl;
                BarRecompute(it, mem[0]);
                if (rest.Count > 0) foreach (var s in mem) SetProps(it, s, rest.ToArray(), fn);
                handles.AddRange(mem.Select(s => s.Handle));
            }
            if (!ax.Hold && ngrp <= 15 && xs.All(v => v == Math.Floor(v)))
                ax.Props[horiz ? "ytick" : "xtick"] = MatrixValue.Row(xs);
            return nout > 0 ? Out(MatrixValue.Col(handles)) : Array.Empty<Value>();
        }

        static BarSeries? BarOfBaseline(Interpreter it, GObject o) =>
            o is GenericObject go && go.Props.TryGetValue("__bar__", out var h) && h is MatrixValue hm ? it.Graphics.Lookup(hm.Re[0]) as BarSeries : null;

        static Value? BaselineGet(Interpreter it, GObject o, string p)
        {
            if (p is not ("xdata" or "ydata") || BarOfBaseline(it, o) is not BarSeries b) return null;
            bool along = (p == "xdata") != b.Horizontal;
            if (along) { var lim = Limits(b.Parent); return MatrixValue.Row(b.Horizontal ? lim.y : lim.x); }
            return MatrixValue.Row(new[] { EffBase(b), EffBase(b) });
        }

        static bool BaselineSet(Interpreter it, GObject o, string p, Value v, string fn)
        {
            if (p is not ("xdata" or "ydata") || BarOfBaseline(it, o) is not BarSeries b) return false;
            bool along = (p == "xdata") != b.Horizontal;
            if (!along) { var bv = M(v, fn).Re[0]; foreach (var m in b.Members ?? new() { b }) m.BaseValue = bv; BarRecompute(it, b); }
            return true;
        }

        static double EffBase(BarSeries b) => b.BaseValue == 0 && (b.Horizontal ? b.Parent.XLog : b.Parent.YLog) ? 1 : b.BaseValue;

        static bool KnownBarProp(string s) => s is "edgecolor" or "facecolor" or "linewidth" or "linestyle" or "barwidth" or "basevalue" or "displayname" or "facealpha" or "tag" or "parent" or "visible" or "userdata";
    }
}
