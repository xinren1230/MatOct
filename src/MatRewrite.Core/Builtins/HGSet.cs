// MatRewrite - GPL-3.0-or-later
// Phase 6: generic set, patch rebuild, callbacks.
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static double[] Vec(Value v, string fn) => M(v, fn).Re.ToArray();
        static bool OnOff(Value v, string fn)
        {
            if (v is MatrixValue m && !m.IsChar && m.Numel == 1) return m.Re[0] != 0;
            var s = S(v, fn).ToLowerInvariant();
            return s switch { "on" => true, "off" => false, _ => throw new OctaveException($"{fn}: invalid value '{s}' (must be \"on\" or \"off\")") };
        }
        static double[]? Lim(Value v, string fn, string p)
        {
            if (IsString(v)) return null;
            var m = M(v, fn);
            if (m.Numel != 2) throw new OctaveException($"{fn}: {p} must be a 2-element vector");
            if (!(m.Re[0] < m.Re[1]) && !double.IsNaN(m.Re[0])) throw new OctaveException($"{fn}: {p} must be increasing");
            return m.Re.ToArray();
        }
        static Rgb? ColorOrNone(Value v, string fn) => IsString(v) && S(v, fn) is "none" or "flat" or "interp" or "auto" ? null : ParseColor(v, fn);

        /// <summary>Generic set; returns false for unknown properties.</summary>
        static bool SetGeneric(Interpreter it, GObject o, string p, Value v, string fn)
        {
            var g = it.Graphics;
            if (p.StartsWith("default")) return true;
            if (p == "parent") { Reparent(it, o, v, fn); return true; }
            if (p is "children" or "type") return true;
            if (o is GenericObject && BaselineSet(it, o, p, v, fn)) return true;
            switch (o, p)
            {
                case (RootObject, "currentfigure"):
                    if (v is MatrixValue cm && cm.IsEmpty) { g.Current = null; return true; }
                    g.Current = g.Lookup(D(v, fn)) as Figure ?? throw new OctaveException($"{fn}: currentfigure must be a figure handle"); return true;
                case (RootObject, _): return true;
                case (Figure f, "currentaxes"):
                    f.CurrentAxes = g.Lookup(D(v, fn)) as Axes ?? throw new OctaveException($"{fn}: currentaxes must be an axes handle"); return true;
                case (Figure f, "name"): f.Name = S(v, fn); return true;
                case (Figure f, "visible"): f.Visible = OnOff(v, fn); return true;
                case (Figure f, "numbertitle"): f.NumberTitle = OnOff(v, fn); return true;
                case (Figure f, "position" or "innerposition"):
                {
                    var pos = Vec(v, fn); if (pos.Length != 4) throw new OctaveException($"{fn}: position must be a 4-element vector");
                    bool changed = (int)pos[2] != f.Width || (int)pos[3] != f.Height;
                    f.Width = Math.Max(1, (int)pos[2]); f.Height = Math.Max(1, (int)pos[3]);
                    if (changed) { f.Props[p] = v; RunCallbackProp(it, f, "sizechangedfcn"); RunCallbackProp(it, f, "resizefcn"); }
                    return true;
                }
                case (Figure f, "colormap"):
                    it.CallFunction("colormap", new Value[] { Num(f.Handle), v }, 0); return true;
                case (Axes ax, "title" or "xlabel" or "ylabel" or "zlabel"):
                    if (IsString(v)) g.Label(ax, p).Text = S(v, fn);
                    else if (g.Lookup(D(v, fn)) is LabelObject src) g.Label(ax, p).Text = src.Text;
                    return true;
                case (Axes ax, "xlim"): ax.XLim = Lim(v, fn, "xlim"); return true;
                case (Axes ax, "ylim"): ax.YLim = Lim(v, fn, "ylim"); return true;
                case (Axes ax, "zlim"): ax.ZLim = Lim(v, fn, "zlim"); return true;
                case (Axes ax, "clim"): ax.CLim = Lim(v, fn, "clim"); return true;
                case (Axes ax, "xlimmode"): if (S(v, fn) == "auto") ax.XLim = null; else ax.XLim = Limits(ax).x; return true;
                case (Axes ax, "ylimmode"): if (S(v, fn) == "auto") ax.YLim = null; else ax.YLim = Limits(ax).y; return true;
                case (Axes ax, "zlimmode"): if (S(v, fn) == "auto") ax.ZLim = null; return true;
                case (Axes ax, "climmode"): if (S(v, fn) == "auto") ax.CLim = null; return true;
                case (Axes ax, "view"): { var w = Vec(v, fn); if (w.Length >= 2) { ax.Az = w[0]; ax.El = w[1]; } return true; }
                case (Axes ax, "xscale"): ax.XLog = S(v, fn) == "log"; return true;
                case (Axes ax, "yscale"): ax.YLog = S(v, fn) == "log"; return true;
                case (Axes ax, "visible"): ax.Visible = OnOff(v, fn); return true;
                case (Axes ax, "xgrid" or "ygrid" or "zgrid"): ax.Grid = OnOff(v, fn); return true;
                case (Axes ax, "colormap"): it.CallFunction("colormap", new Value[] { Num(ax.Handle), v }, 0); return true;
                case (LabelObject l, "string"): l.Text = LabelString(v, fn); return true;
                case (TextSeries t, "position"): { var w = Vec(v, fn); if (w.Length >= 2) { t.X = w[0]; t.Y = w[1]; t.Z = w.Length > 2 ? w[2] : null; } return true; }
                case (TextSeries t, "string"): t.Text = LabelString(v, fn); if (v is CellValue || v is MatrixValue { R: > 1 }) t.Props["__string"] = v; else t.Props.Remove("__string"); return true;
                case (ImageSeries im, "cdata"): { var m = M(v, fn); im.C = ToGrid(m); return true; }
                case (ImageSeries im, "xdata"): { var w = Vec(v, fn); if (w.Length > 0) { im.X0 = w[0]; im.X1 = w[^1]; } return true; }
                case (ImageSeries im, "ydata"): { var w = Vec(v, fn); if (w.Length > 0) { im.Y0 = w[0]; im.Y1 = w[^1]; } return true; }
                case (SurfaceSeries sf, "xdata"): sf.X = ToGrid(M(v, fn)); return true;
                case (SurfaceSeries sf, "ydata"): sf.Y = ToGrid(M(v, fn)); return true;
                case (SurfaceSeries sf, "zdata"): sf.Z = ToGrid(M(v, fn)); return true;
                case (SurfaceSeries sf, "cdata"): sf.C = ToGrid(M(v, fn)); return true;
                case (PatchSeries ps, "facecolor"): ps.FaceColor = ColorOrNone(v, fn); if (IsString(v) && S(v, fn) is "flat" or "interp") ps.FaceColor = ps.Parent?.NextColor(); return true;
                case (PatchSeries ps, "edgecolor"): ps.EdgeColor = ColorOrNone(v, fn); return true;
                case (PatchSeries ps, "facealpha"): ps.FaceAlpha = D(v, fn); return true;
                case (PatchSeries ps, "linewidth"): ps.LineWidth = D(v, fn); return true;
                case (PatchSeries ps, "faces" or "vertices" or "xdata" or "ydata" or "zdata"):
                    ps.Props[p] = v; RebuildPatch(ps, p is "faces" or "vertices"); return true;
                case (LineSeries l, "zdata"): l.Z = M(v, fn).IsEmpty ? null : Vec(v, fn); return true;
                case (LineSeries l, "visible"): return true;
            }
            if (o.Props.ContainsKey(p) || KnownProp(o, p)) return true;
            return false;
        }

        static string LabelString(Value v, string fn)
        {
            if (v is CellValue c) return string.Join("\n", c.Data.Select(x => x is MatrixValue xm && !xm.IsChar ? string.Join(" ", xm.Re.Select(d => d.ToString("G5", System.Globalization.CultureInfo.InvariantCulture))) : S(x, fn)));
            if (v is MatrixValue m && !m.IsChar) return m.Numel == 0 ? "" : string.Join(" ", m.Re.Select(d => d.ToString("G5", System.Globalization.CultureInfo.InvariantCulture)));
            var mv = M(v, fn);
            return string.Join("\n", Enumerable.Range(0, mv.R).Select(mv.RowString));
        }

        static double[,] ToGrid(MatrixValue m)
        {
            var r = new double[m.R, m.C];
            for (int i = 0; i < m.R; i++) for (int j = 0; j < m.C; j++) r[i, j] = m.Re[i + j * m.R];
            return r;
        }

        static void Reparent(Interpreter it, GObject o, Value v, string fn)
        {
            var g = it.Graphics;
            var np = g.Lookup(D(v, fn)) ?? throw new OctaveException($"{fn}: invalid parent handle");
            switch (o)
            {
                case Axes ax when np is Figure nf && nf != ax.Figure:
                    ax.Figure.Axes.Remove(ax); if (ax.Figure.CurrentAxes == ax) ax.Figure.CurrentAxes = ax.Figure.Axes.LastOrDefault();
                    g.Touch(ax.Figure); ax.Figure = nf; nf.Axes.Add(ax); nf.CurrentAxes = ax; g.Touch(nf); break;
                case Series s when np is Axes na:
                    if (s.Parent != na) { s.Parent?.Children.Remove(s); s.Parent = na; na.Children.Add(s); g.Touch(na.Figure); }
                    s.Group = null; break;
                case Series s when np is Series grp:
                    if (s.Parent != grp.Parent) { s.Parent?.Children.Remove(s); s.Parent = grp.Parent; grp.Parent.Children.Add(s); }
                    s.Group = grp; break;
                case GenericObject go when go.Owner != null:
                    if (go.Owner is Figure of) of.Others.Remove(go); else g.Root.Others.Remove(go);
                    go.Owner = np; if (np is Figure nf2) nf2.Others.Add(go); else g.Root.Others.Add(go); break;
            }
        }

        /// <summary>Rebuild the polygons of a patch from faces/vertices or xdata/ydata.</summary>
        static void RebuildPatch(PatchSeries ps, bool fromFV)
        {
            ps.Polys.Clear();
            ps.Props.TryGetValue("faces", out var fv); ps.Props.TryGetValue("vertices", out var vv);
            ps.Props.TryGetValue("xdata", out var xv); ps.Props.TryGetValue("ydata", out var yv);
            if (fromFV && fv is MatrixValue F && vv is MatrixValue V && !F.IsEmpty && !V.IsEmpty)
            {
                for (int i = 0; i < F.R; i++)
                {
                    var xs = new List<double>(); var ys = new List<double>();
                    for (int j = 0; j < F.C; j++)
                    {
                        double k = F.Re[i + j * F.R];
                        if (double.IsNaN(k)) break;
                        int r = (int)k - 1; if (r < 0 || r >= V.R) continue;
                        xs.Add(V.Re[r]); ys.Add(V.C > 1 ? V.Re[r + V.R] : 0);
                    }
                    ps.Polys.Add((xs.ToArray(), ys.ToArray()));
                }
                // keep xdata/ydata consistent
                int nv = F.C, nf = F.R;
                var X = new MatrixValue(nv, nf); var Y = new MatrixValue(nv, nf);
                for (int i = 0; i < nf; i++) for (int j = 0; j < nv; j++)
                {
                    double k = F.Re[i + j * F.R]; int r = double.IsNaN(k) ? -1 : (int)k - 1;
                    X.Re[j + i * nv] = r >= 0 && r < V.R ? V.Re[r] : double.NaN;
                    Y.Re[j + i * nv] = r >= 0 && r < V.R && V.C > 1 ? V.Re[r + V.R] : double.NaN;
                }
                ps.Props["xdata"] = X; ps.Props["ydata"] = Y;
                return;
            }
            if (xv is MatrixValue XM && yv is MatrixValue YM && !XM.IsEmpty)
            {
                bool vec = XM.R == 1 || XM.C == 1;
                int rows = vec ? XM.Numel : XM.R, cols = vec ? 1 : XM.C;
                var verts = new List<double[]>(); var faces = new MatrixValue(cols, rows);
                for (int c = 0; c < cols; c++)
                {
                    var xs = new double[rows]; var ys = new double[rows];
                    for (int r = 0; r < rows; r++)
                    {
                        int idx = r + c * rows;
                        xs[r] = XM.Re[Math.Min(idx, XM.Numel - 1)]; ys[r] = YM.Numel > 0 ? YM.Re[Math.Min(idx, YM.Numel - 1)] : 0;
                        verts.Add(new[] { xs[r], ys[r] }); faces.Re[c + r * cols] = verts.Count;
                    }
                    ps.Polys.Add((xs, ys));
                }
                var Vm = new MatrixValue(verts.Count, 2);
                for (int i = 0; i < verts.Count; i++) { Vm.Re[i] = verts[i][0]; Vm.Re[i + verts.Count] = verts[i][1]; }
                ps.Props["faces"] = faces; ps.Props["vertices"] = Vm;
            }
        }

        /// <summary>Run a callback value (function handle, cell {fh, args...}, or string) with src/evt.</summary>
        internal static void RunCallback(Interpreter it, GObject src, Value cb, Value? evt = null)
        {
            var g = it.Graphics;
            var prev = g.CallbackObject; g.CallbackObject = src;
            try
            {
                evt ??= new MatrixValue(0, 0);
                switch (cb)
                {
                    case FunctionHandleValue: it.CallCallable(cb, new[] { Num(src.Handle), evt }, 0); break;
                    case CellValue c when c.Data.Length > 0:
                        if (IsString(c.Data[0])) it.CallFunction(S(c.Data[0], "callback"), new[] { Num(src.Handle), evt }.Concat(c.Data.Skip(1)).ToArray(), 0);
                        else it.CallCallable(c.Data[0], new[] { Num(src.Handle), evt }.Concat(c.Data.Skip(1)).ToArray(), 0);
                        break;
                    case MatrixValue m when m.IsChar && m.Numel > 0: it.EvalInCurrentScope(S(m, "callback")); break;
                }
            }
            finally { g.CallbackObject = prev; }
        }

        internal static void RunCallbackProp(Interpreter it, GObject o, string prop, Value? evt = null)
        {
            if (o.Props.TryGetValue(prop, out var cb) || TryDefault(it, o, prop, out cb))
                if (!(cb is MatrixValue m && !m.IsChar && m.IsEmpty)) RunCallback(it, o, cb, evt);
        }

        internal static void DrainCallbacks(Interpreter it)
        {
            while (it.Graphics.PendingCallbacks.TryDequeue(out var c))
            {
                var o = it.Graphics.Lookup(c.handle);
                if (o == null) continue;
                if (c.prop == "closerequestfcn" && o is Figure f) { CloseFigure(it, f, false); continue; }
                RunCallbackProp(it, o, c.prop);
            }
        }

        static void CloseFigure(Interpreter it, Figure f, bool force)
        {
            if (!force && f.Props.TryGetValue("closerequestfcn", out var cb) && !(IsString(cb) && S(cb, "close") == "closereq") && !(cb is MatrixValue em && em.IsEmpty && !em.IsChar))
            { RunCallback(it, f, cb); return; }
            DeleteObject(it, f);
        }

        internal static void DeleteObject(Interpreter it, GObject o)
        {
            if (o.Deleted || o is RootObject) return;
            o.Props["beingdeleted"] = Str("on");
            foreach (var c in it.Graphics.ChildrenOf(o)) DeleteObject(it, c);
            try { RunCallbackProp(it, o, "deletefcn"); } catch (OctaveException) { }
            it.Graphics.Delete(o);
        }
    }
}
