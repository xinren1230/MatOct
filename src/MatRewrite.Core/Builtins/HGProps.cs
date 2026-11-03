// MatRewrite - GPL-3.0-or-later
// Phase 6: generic get/set on top of the typed graphics model.
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        internal static string TypeOf(GObject o) => o switch
        {
            RootObject => "root", Figure => "figure", Axes => "axes", LabelObject => "text", LineSeries => "line",
            ScatterSeries => "scatter", BarSeries => "hggroup", ImageSeries => "image", SurfaceSeries => "surface",
            ContourSeries => "hggroup", LightObject => "light", ErrorBarSeries => "hggroup", TextSeries => "text",
            PatchSeries => "patch", GenericObject g => g.Type, Annotation => "hggroup", _ => "unknown"
        };

        static bool TryDefault(Interpreter it, GObject o, string p, out Value v)
        {
            var t = TypeOf(o);
            // user defaults stored on ancestors as "default<type><prop>"
            for (GObject? a = it.Graphics.ParentOf(o); a != null; a = it.Graphics.ParentOf(a))
                if (a.Props.TryGetValue("default" + t + p, out v!)) return true;
            if (it.Graphics.RootProps.TryGetValue("default" + t + p, out v!)) return true;
            if (TypeDefaults.TryGetValue(t, out var d) && d.TryGetValue(p, out var f)) { v = f(); return true; }
            if (CommonDefaults.TryGetValue(p, out var cf)) { v = cf(); return true; }
            v = null!; return false;
        }

        static bool KnownProp(GObject o, string p) =>
            (TypeDefaults.TryGetValue(TypeOf(o), out var d) && d.ContainsKey(p)) || CommonDefaults.ContainsKey(p)
            || p is "type" or "parent" or "children" or "tag" or "userdata";

        static Value HandleOrEmpty(GObject? o) => o == null ? new MatrixValue(0, 0) : Num(o.Handle);
        static Value HCol(IEnumerable<GObject> xs) { var a = xs.Select(x => x.Handle).ToArray(); return new MatrixValue(a.Length, a.Length == 0 ? 0 : 1, a); }
        static Value Rgbv(Rgb c) => MatrixValue.Row(new[] { c.R, c.G, c.B });

        /// <summary>Generic get: computed properties, then the property bag, then defaults.</summary>
        static Value? GetGeneric(Interpreter it, GObject o, string p)
        {
            var g = it.Graphics;
            switch (p)
            {
                case "type": return Str(TypeOf(o));
                case "parent": return HandleOrEmpty(g.ParentOf(o));
                case "children": return HCol(g.ChildrenOf(o).Where(c => !c.Deleted && !IsHidden(c) ));
                case "beingdeleted": return Str(o.Deleted ? "on" : "off");
            }
            switch (o, p)
            {
                case (RootObject, "currentfigure"): return HandleOrEmpty(g.Current);
                case (RootObject, "callbackobject"): return HandleOrEmpty(g.CallbackObject);
                case (Figure f, "currentaxes"): return HandleOrEmpty(f.CurrentAxes);
                case (Figure f, "name"): return Str(f.Name);
                case (Figure f, "number"): return f.Handle == Math.Floor(f.Handle) && f.Handle > 0 ? Num(f.Handle) : new MatrixValue(0, 0);
                case (Figure f, "visible"): return Str(f.Visible ? "on" : "off");
                case (Figure f, "numbertitle"): return Str(f.NumberTitle ? "on" : "off");
                case (Figure f, "integerhandle"): return Str(f.Handle == Math.Floor(f.Handle) && f.Handle > 0 ? "on" : "off");
                case (Figure f, "colormap"): return it.CallFunction(f.Colormap.StartsWith("custom") ? "parula" : f.Colormap, new Value[] { Num(64) }, 1)[0];
                case (Axes ax, "title"): return Num(g.Label(ax, "title").Handle);
                case (Axes ax, "xlabel"): return Num(g.Label(ax, "xlabel").Handle);
                case (Axes ax, "ylabel"): return Num(g.Label(ax, "ylabel").Handle);
                case (Axes ax, "zlabel"): return Num(g.Label(ax, "zlabel").Handle);
                case (Axes ax, "xlim"): return MatrixValue.Row(Limits(ax).x);
                case (Axes ax, "ylim"): return MatrixValue.Row(Limits(ax).y);
                case (Axes ax, "zlim"): return MatrixValue.Row(ZLimits(ax));
                case (Axes ax, "clim"): return MatrixValue.Row(ax.CLim ?? new double[] { 0, 1 });
                case (Axes ax, "xlimmode"): return Str(ax.XLim == null ? "auto" : "manual");
                case (Axes ax, "ylimmode"): return Str(ax.YLim == null ? "auto" : "manual");
                case (Axes ax, "zlimmode"): return Str(ax.ZLim == null ? "auto" : "manual");
                case (Axes ax, "climmode"): return Str(ax.CLim == null ? "auto" : "manual");
                case (Axes ax, "view"): return MatrixValue.Row(new[] { ax.Az, ax.El });
                case (Axes ax, "xscale"): return Str(ax.XLog ? "log" : "linear");
                case (Axes ax, "yscale"): return Str(ax.YLog ? "log" : "linear");
                case (Axes ax, "nextplot"): return Str(ax.Hold ? "add" : "replace");
                case (Axes ax, "xgrid" or "ygrid"): return Str(ax.Grid ? "on" : "off");
                case (Axes ax, "box"): return Str(ax.Box ? "on" : "off");
                case (Axes ax, "ydir"): return Str(ax.YDirReverse ? "reverse" : "normal");
                case (Axes ax, "visible"): return Str(ax.Visible ? "on" : "off");
                case (Axes ax, "colororder"): return ColorOrderM();
                case (Axes ax, "xtick") when !ax.Props.ContainsKey("xtick"): return MatrixValue.Row(Ticks(Limits(ax).x, ax.XLog));
                case (Axes ax, "ytick") when !ax.Props.ContainsKey("ytick"): return MatrixValue.Row(Ticks(Limits(ax).y, ax.YLog));
                case (Axes ax, "xticklabel" or "yticklabel") when !ax.Props.ContainsKey(p):
                {
                    var tv = GetGeneric(it, ax, p[0] + "tick") as MatrixValue;
                    var labs = (tv?.Re ?? Array.Empty<double>()).Select(d => (Value)Str(d.ToString("G5", System.Globalization.CultureInfo.InvariantCulture))).ToArray();
                    return new CellValue(labs.Length, labs.Length == 0 ? 0 : 1, labs);
                }
                case (Axes ax, "colormap"): return GetGeneric(it, ax.Figure, "colormap");
                case (LabelObject l, "string"): return Str(l.Text);
                case (LabelObject l, "position"):
                {
                    if (l.Props.TryGetValue("position", out var pv)) return pv;
                    var (xl, yl) = Limits(l.Axes);
                    double mx = (xl[0] + xl[1]) / 2, my = (yl[0] + yl[1]) / 2;
                    return l.Which switch
                    {
                        "title" => MatrixValue.Row(new[] { mx, yl[1] + 0.02 * (yl[1] - yl[0]), 0 }),
                        "xlabel" => MatrixValue.Row(new[] { mx, yl[0] - 0.08 * (yl[1] - yl[0]), 0 }),
                        "ylabel" => MatrixValue.Row(new[] { xl[0] - 0.08 * (xl[1] - xl[0]), my, 0 }),
                        _ => MatrixValue.Row(new double[] { 0, 0, 0 })
                    };
                }
                case (LabelObject l, "horizontalalignment"): return l.Props.TryGetValue(p, out var ha) ? ha : Str("center");
                case (LabelObject l, "verticalalignment"): return l.Props.TryGetValue(p, out var va) ? va : Str(l.Which == "title" ? "bottom" : l.Which == "xlabel" ? "top" : "bottom");
                case (LabelObject l, "rotation"): return l.Props.TryGetValue(p, out var ro) ? ro : Num(l.Which == "ylabel" ? 90 : 0);
                case (LabelObject l, "fontweight"): return l.Props.TryGetValue(p, out var fw) ? fw : Str(l.Which == "title" ? "bold" : "normal");
                case (LabelObject l, "fontsize"): return l.Props.TryGetValue(p, out var fs) ? fs : Num(11);
                case (LineSeries l, "color"): return Rgbv(l.Color);
                case (LineSeries l, "linewidth"): return Num(l.LineWidth);
                case (LineSeries l, "markersize"): return Num(l.MarkerSize);
                case (LineSeries l, "xdata"): return MatrixValue.Row(l.X);
                case (LineSeries l, "ydata"): return MatrixValue.Row(l.Y);
                case (LineSeries l, "zdata"): return l.Z == null ? new MatrixValue(0, 0) : MatrixValue.Row(l.Z);
                case (LineSeries l, "linestyle"): return Str(l.Style switch { LineStyle.Solid => "-", LineStyle.Dashed => "--", LineStyle.Dotted => ":", LineStyle.DashDot => "-.", _ => "none" });
                case (LineSeries l, "marker"): return Str(MarkerName(l.Marker));
                case (TextSeries t, "string"): return t.Props.TryGetValue("__string", out var tsv) ? tsv : Str(t.Text);
                case (TextSeries t, "position"): return MatrixValue.Row(new[] { t.X, t.Y, t.Z ?? 0 });
                case (TextSeries t, "color"): return Rgbv(t.Color);
                case (TextSeries t, "fontsize"): return Num(t.FontSize);
                case (TextSeries t, "horizontalalignment"): return Str(t.HAlign);
                case (SurfaceSeries sf, "zdata"): return FromGrid(sf.Z);
                case (SurfaceSeries sf, "xdata"): return FromGrid(sf.X);
                case (SurfaceSeries sf, "ydata"): return FromGrid(sf.Y);
                case (SurfaceSeries sf, "cdata"): return FromGrid(sf.C);
                case (SurfaceSeries sf, "facealpha"): return Num(sf.FaceAlpha);
                case (SurfaceSeries sf, "facelighting"): return Str(sf.FaceLighting);
                case (ImageSeries im, "cdata"): return FromGrid(im.C);
                case (ImageSeries im, "xdata"): return MatrixValue.Row(new[] { im.X0, im.X1 });
                case (ImageSeries im, "ydata"): return MatrixValue.Row(new[] { im.Y0, im.Y1 });
                case (LightObject lo, "position"): return LightPos(lo);
                case (LightObject lo, "visible"): return Str(lo.Visible ? "on" : "off");
                case (LightObject lo, "color"): return Rgbv(lo.Color);
                case (ContourSeries cs, "levellist"): return MatrixValue.Row(cs.Levels);
                case (GenericObject lgo, "string") when lgo.Type == "legend" && lgo.Parent is Axes lgax:
                    {
                        var labs = lgax.LegendLabels ?? lgax.Children.OfType<Series>().Where(c => c.DisplayName != "").Select(c => c.DisplayName).ToArray();
                        return CellValue.RowOf(labs.Select(x => (Value)Str(x)));
                    }
                case (PatchSeries ps, "facealpha"): return Num(ps.FaceAlpha);
                case (PatchSeries ps, "linewidth"): return Num(ps.LineWidth);
                case (Annotation an, "string"): return Str(an.Text);
                case (BarSeries b, "xdata"): return MatrixValue.Col(b.X);
                case (BarSeries b, "ydata"): return MatrixValue.Col(b.Y);
                case (BarSeries b, "barwidth"): return Num(b.Width);
                case (BarSeries b, "barlayout"): return Str(b.Layout);
                case (BarSeries b, "horizontal"): return Str(b.Horizontal ? "on" : "off");
                case (BarSeries b, "basevalue"): return Num(EffBase(b));
                case (PatchSeries { Group: BarSeries pb }, "xdata" or "ydata" or "vertices"): BarRecompute(it, (pb.Members ?? new() { pb })[0]); return o.Props[p];
                case (BarSeries b, "showbaseline"): return Str(b.ShowBaseline ? "on" : "off");
                case (BarSeries b, "baseline"): return HandleOrEmpty(b.Baseline);
                case (BarSeries b, "bargroup"): return MatrixValue.Col((b.Members ?? new() { b }).Select(m => m.Handle));
                case (BarSeries b, "facecolor"): return Rgbv(b.Color);
                case (BarSeries b, "edgecolor"): return b.Props.TryGetValue("edgecolor", out var bec) ? bec : MatrixValue.Row(new double[] { 0, 0, 0 });
                case (BarSeries b, "linewidth"): return b.Props.TryGetValue("linewidth", out var blw) ? blw : Num(0.5);
                case (BarSeries b, "linestyle"): return b.Props.TryGetValue("linestyle", out var bls) ? bls : Str("-");
                case (BarSeries b, "hittestarea"): return Str("off");
                case (_, "displayname"): return Str(o.DisplayName);
            }
            if (o is GenericObject && BaselineGet(it, o, p) is Value blv) return blv;
            if (o.Props.TryGetValue(p, out var bag)) return bag;
            if (TryDefault(it, o, p, out var dv)) return dv;
            return null;
        }

        static bool IsHidden(GObject o) => o.Props.TryGetValue("handlevisibility", out var v) && IsString(v) && S(v, "get") != "on";

        static string MarkerName(Marker m) => m switch
        {
            Marker.None => "none", Marker.Circle => "o", Marker.Plus => "+", Marker.Star => "*", Marker.Point => ".", Marker.Cross => "x",
            Marker.Square => "s", Marker.Diamond => "d", Marker.TriangleUp => "^", Marker.TriangleDown => "v", Marker.TriangleLeft => "<",
            Marker.TriangleRight => ">", Marker.Pentagram => "p", Marker.Hexagram => "h", _ => "none"
        };

        static MatrixValue ColorOrderM()
        {
            var o = new MatrixValue(Rgb.DefaultOrder.Length, 3);
            for (int i = 0; i < Rgb.DefaultOrder.Length; i++)
            {
                var c = Rgb.DefaultOrder[i]; int r = o.R;
                o.Re[i] = c.R; o.Re[i + r] = c.G; o.Re[i + 2 * r] = c.B;
            }
            return o;
        }

        static double[] Ticks(double[] lim, bool log)
        {
            double a = lim[0], b = lim[1];
            if (!(b > a) || double.IsInfinity(a) || double.IsInfinity(b)) return new[] { a };
            if (log && a > 0) { var r = new List<double>(); for (double e = Math.Floor(Math.Log10(a)); e <= Math.Ceiling(Math.Log10(b)); e++) { var t = Math.Pow(10, e); if (t >= a * (1 - 1e-9) && t <= b * (1 + 1e-9)) r.Add(t); } return r.ToArray(); }
            double raw = (b - a) / 5, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / mag;
            double step = (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
            var res = new List<double>();
            for (double t = Math.Ceiling(a / step - 1e-9) * step; t <= b + step * 1e-9; t += step) res.Add(Math.Abs(t) < step * 1e-9 ? 0 : t);
            return res.ToArray();
        }
    }
}
