// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Runtime
{
    public sealed partial class Interpreter
    {
        public readonly GraphicsState Graphics = new();
    }
}

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        /// <summary>Register a graphics builtin: the backend is refreshed after the call.</summary>
        static void RegG(string name, BuiltinFn f) => Reg(name, (it, a, n) =>
        {
            try { return f(it, a, n); }
            finally { it.Graphics.Flush(); }
        });

        static Value H(GObject o) => Num(o.Handle);

        static void RegisterGraphics()
        {
            RegG("figure", (it, a, n) =>
            {
                var g = it.Graphics;
                Figure f;
                int k = 0;
                if (a.Length > 0 && a[0] is MatrixValue m && !m.IsChar)
                {
                    int id = (int)m.ScalarDouble("figure");
                    if (id < 1) throw new OctaveException("figure: N must be a positive integer");
                    f = g.Figures.TryGetValue(id, out var ex) ? ex : g.NewFigure(id);
                    g.Current = f; k = 1;
                }
                else f = g.NewFigure();
                SetProps(it, f, a.Skip(k).ToArray(), "figure");
                g.Touch(f);
                return n > 0 ? Out(H(f)) : Array.Empty<Value>();
            });
            RegG("gcf", (it, a, n) => Out(H(it.Graphics.Gcf())));
            RegG("gca", (it, a, n) => Out(H(it.Graphics.Gca())));
            RegG("clf", (it, a, n) =>
            {
                var f = it.Graphics.Gcf();
                f.Axes.Clear(); f.CurrentAxes = null; it.Graphics.Touch(f);
                return n > 0 ? Out(H(f)) : Array.Empty<Value>();
            });
            RegG("cla", (it, a, n) => { var ax = it.Graphics.Gca(); ax.Reset(); ax.Title = ax.XLabel = ax.YLabel = ""; it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("close", (it, a, n) =>
            {
                var g = it.Graphics;
                if (a.Length == 0) { if (g.Current != null) g.Close(g.Current); }
                else if (IsString(a[0]))
                {
                    var s = S(a[0], "close");
                    if (s == "all") g.CloseAll();
                    else { var f = g.Figures.Values.FirstOrDefault(x => x.Name == s) ?? throw new OctaveException($"close: no figure named '{s}'"); g.Close(f); }
                }
                else
                    foreach (var h in M(a[0], "close").Re)
                        if (g.Lookup(h) is Figure f) g.Close(f);
                        else throw new OctaveException("close: first argument must be \"all\", a figure handle, or a figure name");
                return n > 0 ? Out(Num(1)) : Array.Empty<Value>();
            });
            RegG("drawnow", (it, a, n) => { foreach (var f in it.Graphics.Figures.Values) it.Graphics.Touch(f); return Array.Empty<Value>(); });
            RegG("ishandle", (it, a, n) =>
            {
                var m = M(a[0], "ishandle");
                var r = new MatrixValue(m.R, m.C, NumKind.Logical);
                for (int i = 0; i < m.Numel; i++) r.Re[i] = it.Graphics.Lookup(m.Re[i]) != null ? 1 : 0;
                return Out(r);
            });
            RegG("isfigure", (it, a, n) => Out(Bool(a[0] is MatrixValue m && m.Numel == 1 && it.Graphics.Lookup(m.Re[0]) is Figure)));

            RegG("hold", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length == 0) ax.Hold = !ax.Hold;
                else
                {
                    var s = S(a[^1], "hold");
                    ax.Hold = s switch { "on" => true, "off" => false, "all" => true, _ => throw new OctaveException("hold: invalid hold STATE") };
                }
                return Array.Empty<Value>();
            });
            RegG("ishold", (it, a, n) => Out(Bool(it.Graphics.Gca().Hold)));

            RegG("plot", (it, a, n) => { var hs = Plot(it, a, "plot"); return n > 0 ? Out(MatrixValue.Col(hs)) : Array.Empty<Value>(); });
            RegG("line", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length < 2) throw new OctaveException("Invalid call to line");
                var x = M(a[0], "line").Re; var y = M(a[1], "line").Re;
                var s = it.Graphics.Add(ax, new LineSeries { X = x, Y = y, Color = ax.NextColor() });
                SetProps(it, s, a.Skip(2).ToArray(), "line");
                return n > 0 ? Out(H(s)) : Array.Empty<Value>();
            });
            RegG("scatter", (it, a, n) =>
            {
                if (a.Length < 2) throw new OctaveException("Invalid call to scatter");
                var ax = PrepareAxes(it);
                var x = M(a[0], "scatter").Re; var y = M(a[1], "scatter").Re;
                if (x.Length != y.Length) throw new OctaveException("scatter: X and Y must be vectors of the same length");
                var s = new ScatterSeries { X = x, Y = y, Color = ax.NextColor() };
                int k = 2;
                if (k < a.Length && a[k] is MatrixValue sz && !sz.IsChar) { if (sz.Numel > 0) s.Sizes = sz.Re.ToArray(); k++; }
                if (k < a.Length && !(IsString(a[k]) && S(a[k], "scatter") is var cs && (cs == "filled" || cs.Length > 1 && Rgb.FromName(cs) == null)))
                {
                    if (a[k] is MatrixValue cm && !cm.IsChar)
                    {
                        if (cm.Numel == 3 && x.Length != 3 || cm.R == 1 && cm.C == 3 && x.Length == 3) s.Color = new Rgb(cm.Re[0], cm.Re[1], cm.Re[2]);
                        else if (cm.Numel == x.Length) s.CData = cm.Re.ToArray();
                        else throw new OctaveException("scatter: C must be a single color or a vector of the same length as X");
                    }
                    else s.Color = Rgb.FromName(S(a[k], "scatter")) ?? s.Color;
                    k++;
                }
                for (; k < a.Length; k++)
                {
                    if (!IsString(a[k])) continue;
                    var opt = S(a[k], "scatter");
                    if (opt == "filled") s.Filled = true;
                    else if (opt.Length <= 2 && ParseFmt(opt, out _, out var mk, out var col)) { if (mk != null) s.Marker = mk.Value; if (col != null) s.Color = col.Value; }
                    else { SetProps(it, s, a.Skip(k).ToArray(), "scatter"); break; }
                }
                it.Graphics.Add(ax, s);
                return n > 0 ? Out(H(s)) : Array.Empty<Value>();
            });
            RegG("bar", (it, a, n) => Out(Bar(it, a, n, false, "bar")).Take(n > 0 ? 1 : 0).ToArray());
            RegG("barh", (it, a, n) => Out(Bar(it, a, n, true, "barh")).Take(n > 0 ? 1 : 0).ToArray());
            RegG("hist", (it, a, n) =>
            {
                var y = M(a[0], "hist");
                var data = y.Re.Where(v => !double.IsNaN(v)).ToArray();
                double[] centers;
                if (a.Length > 1 && M(a[1], "hist").Numel > 1) centers = M(a[1], "hist").Re.ToArray();
                else
                {
                    int nb = a.Length > 1 ? I(a[1], "hist") : 10;
                    if (nb < 1) throw new OctaveException("hist: number of bins NBINS must be positive");
                    double lo = data.Length > 0 ? data.Min() : 0, hi = data.Length > 0 ? data.Max() : 0;
                    if (lo == hi) { lo -= 0.5; hi += 0.5; }
                    double w = (hi - lo) / nb;
                    centers = Enumerable.Range(0, nb).Select(i => lo + w * (i + 0.5)).ToArray();
                }
                var counts = new double[centers.Length];
                foreach (var v in data)
                {
                    int best = 0;
                    for (int i = 1; i < centers.Length; i++)
                    {
                        double edge = (centers[i - 1] + centers[i]) / 2;
                        if (v >= edge) best = i; else break;
                    }
                    counts[best]++;
                }
                if (a.Length > 2) { double norm = D(a[2], "hist"); double tot = counts.Sum(); if (tot > 0) for (int i = 0; i < counts.Length; i++) counts[i] *= norm / tot; }
                bool rowOut = y.R == 1 || y.C == 1;
                if (n > 0)
                    return Out(rowOut ? MatrixValue.Row(counts) : MatrixValue.Col(counts), rowOut ? MatrixValue.Row(centers) : MatrixValue.Col(centers));
                var ax = PrepareAxes(it);
                it.Graphics.Add(ax, new BarSeries { X = centers, Y = counts, Width = 1.0, Color = ax.NextColor() });
                return Array.Empty<Value>();
            });
            RegG("imagesc", (it, a, n) =>
            {
                Value cv; double[]? xr = null, yr = null;
                if (a.Length >= 3) { xr = M(a[0], "imagesc").Re; yr = M(a[1], "imagesc").Re; cv = a[2]; }
                else cv = a[0];
                var c = M(cv, "imagesc");
                if (c.NDims > 2) throw new OctaveException("imagesc: C must be a 2-D matrix");
                var ax = PrepareAxes(it);
                var img = new ImageSeries { C = new double[c.R, c.C] };
                for (int i = 0; i < c.R; i++) for (int j = 0; j < c.C; j++) img.C[i, j] = c.Re[i + j * c.R];
                var fin = c.Re.Where(double.IsFinite).ToArray();
                img.CMin = fin.Length > 0 ? fin.Min() : 0; img.CMax = fin.Length > 0 ? fin.Max() : 1;
                if (a.Length >= (xr != null ? 4 : 2) && a[xr != null ? 3 : 1] is MatrixValue lim && !lim.IsChar && lim.Numel == 2) { img.CMin = lim.Re[0]; img.CMax = lim.Re[1]; ax.CLim = lim.Re.ToArray(); }
                img.X0 = xr != null ? xr[0] : 1; img.X1 = xr != null ? xr[^1] : c.C;
                img.Y0 = yr != null ? yr[0] : 1; img.Y1 = yr != null ? yr[^1] : c.R;
                ax.YDirReverse = true; ax.Tight = true;
                it.Graphics.Add(ax, img);
                return n > 0 ? Out(H(img)) : Array.Empty<Value>();
            });
            RegG("colorbar", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                ax.Colorbar = !(a.Length > 0 && IsString(a[0]) && S(a[0], "colorbar") is "off" or "hide");
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(H(ax)) : Array.Empty<Value>();
            });
            RegG("colormap", (it, a, n) =>
            {
                var f = it.Graphics.Gcf();
                if (a.Length > 0)
                {
                    var name = IsString(a[^1]) ? S(a[^1], "colormap").ToLowerInvariant() : throw new OctaveException("colormap: only named colormaps are supported");
                    if (!(name is "default" or "viridis" or "parula" or "jet" or "gray" or "hot" or "cool" or "turbo" or "magma" or "inferno" or "plasma"))
                        throw new OctaveException($"colormap: invalid MAP name '{name}'");
                    f.Colormap = name == "default" ? "viridis" : name;
                    it.Graphics.Touch(f);
                }
                return n > 0 || a.Length == 0 ? Out(Str(f.Colormap)) : Array.Empty<Value>();
            });

            RegG("title", (it, a, n) => { var ax = it.Graphics.Gca(); ax.Title = LabelText(a, "title"); it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("xlabel", (it, a, n) => { var ax = it.Graphics.Gca(); ax.XLabel = LabelText(a, "xlabel"); it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("ylabel", (it, a, n) => { var ax = it.Graphics.Gca(); ax.YLabel = LabelText(a, "ylabel"); it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("legend", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                var labels = new List<string>();
                for (int k = 0; k < a.Length; k++)
                {
                    if (a[k] is CellValue c) { labels.AddRange(c.Data.Select(x => S(x, "legend"))); continue; }
                    var s = S(a[k], "legend");
                    if (s is "show" or "on") { ax.LegendVisible = true; labels = null!; break; }
                    if (s is "off" or "hide") { ax.LegendVisible = false; it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); }
                    if (s is "boxoff" or "boxon") continue;
                    if (s.Equals("location", StringComparison.OrdinalIgnoreCase) && k + 1 < a.Length) { ax.LegendLocation = S(a[++k], "legend").ToLowerInvariant(); continue; }
                    labels.Add(s);
                }
                ax.LegendVisible = true;
                if (labels != null && labels.Count > 0)
                {
                    ax.LegendLabels = labels.ToArray();
                    for (int i = 0; i < ax.Children.Count && i < labels.Count; i++) ax.Children[i].DisplayName = labels[i];
                }
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(H(ax)) : Array.Empty<Value>();
            });
            RegG("grid", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length == 0) ax.Grid = !ax.Grid;
                else switch (S(a[^1], "grid"))
                {
                    case "on": ax.Grid = true; break;
                    case "off": ax.Grid = false; ax.MinorGrid = false; break;
                    case "minor": ax.MinorGrid = !ax.MinorGrid; if (ax.MinorGrid) ax.Grid = true; break;
                    default: throw new OctaveException("grid: argument must be \"on\", \"off\" or \"minor\"");
                }
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });
            RegG("box", (it, a, n) => { var ax = it.Graphics.Gca(); ax.Box = a.Length == 0 ? !ax.Box : S(a[0], "box") == "on"; it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("subplot", (it, a, n) =>
            {
                int rows, cols; int[] cells;
                if (a.Length == 1)
                {
                    int code = I(a[0], "subplot");
                    rows = code / 100; cols = code / 10 % 10; cells = new[] { code % 10 };
                }
                else
                {
                    if (a.Length < 3) throw new OctaveException("Invalid call to subplot");
                    rows = I(a[0], "subplot"); cols = I(a[1], "subplot");
                    cells = M(a[2], "subplot").Re.Select(x => (int)x).ToArray();
                }
                if (rows < 1 || cols < 1 || cells.Length == 0 || cells.Any(c => c < 1 || c > rows * cols))
                    throw new OctaveException("subplot: INDEX value must be >= 1 and <= ROWS*COLS");
                var ax = it.Graphics.Subplot(rows, cols, cells.OrderBy(x => x).ToArray());
                return n > 0 ? Out(H(ax)) : Array.Empty<Value>();
            });
            RegG("axis", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length == 0)
                {
                    var (xl, yl) = Limits(ax);
                    return Out(MatrixValue.Row(new[] { xl[0], xl[1], yl[0], yl[1] }));
                }
                foreach (var v in a)
                {
                    if (v is MatrixValue m && !m.IsChar)
                    {
                        if (m.Numel < 4) throw new OctaveException("axis: LIMITS vector must have 4, 6, or 8 elements");
                        ax.XLim = new[] { m.Re[0], m.Re[1] }; ax.YLim = new[] { m.Re[2], m.Re[3] };
                        continue;
                    }
                    switch (S(v, "axis"))
                    {
                        case "equal": case "image": ax.Equal = true; if (S(v, "axis") == "image") ax.Tight = true; break;
                        case "square": ax.Square = true; break;
                        case "normal": ax.Equal = ax.Square = false; break;
                        case "tight": ax.Tight = true; ax.XLim = ax.YLim = null; break;
                        case "auto": ax.Tight = false; ax.XLim = ax.YLim = null; break;
                        case "on": ax.Visible = true; break;
                        case "off": ax.Visible = false; break;
                        case "ij": ax.YDirReverse = true; break;
                        case "xy": ax.YDirReverse = false; break;
                        case "manual": case "fill": case "vis3d": break;
                        default: throw new OctaveException($"axis: unrecognized option '{S(v, "axis")}'");
                    }
                }
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });
            RegG("xlim", (it, a, n) => AxLim(it, a, n, true));
            RegG("ylim", (it, a, n) => AxLim(it, a, n, false));
            RegG("set", (it, a, n) =>
            {
                if (a.Length < 1) throw new OctaveException("Invalid call to set");
                foreach (var h in M(a[0], "set").Re)
                {
                    var o = it.Graphics.Lookup(h) ?? throw new OctaveException("set: invalid graphics handle");
                    SetProps(it, o, a.Skip(1).ToArray(), "set");
                }
                return Array.Empty<Value>();
            });
            RegG("get", (it, a, n) =>
            {
                if (a.Length >= 2 && a[0] is MatrixValue rm && !rm.IsChar && rm.Numel == 1 && rm.Re[0] == 0)
                {
                    var hs = it.Graphics.Figures.Keys.OrderByDescending(k => k).Select(k => (double)k).ToArray();
                    return S(a[1], "get").ToLowerInvariant() switch
                    {
                        "children" => Out(new MatrixValue(hs.Length, hs.Length == 0 ? 0 : 1, hs)),
                        "currentfigure" => Out(it.Graphics.Current == null ? new MatrixValue(0, 0, Array.Empty<double>()) : new MatrixValue(1, 1, new[] { it.Graphics.Current.Handle })),
                        _ => throw new OctaveException("get: unknown root property")
                    };
                }
                var o = it.Graphics.Lookup(D(a[0], "get")) ?? throw new OctaveException("get: invalid graphics handle");
                if (a.Length < 2) throw new OctaveException("get: property name required");
                return Out(GetProp(it, o, S(a[1], "get")));
            });
            RegG("print", (it, a, n) =>
            {
                Figure f = it.Graphics.Gcf();
                string? file = null; string? dev = null; int w = 0, h = 0;
                foreach (var v in a)
                {
                    if (v is MatrixValue m && !m.IsChar) { f = it.Graphics.Lookup(m.Re[0]) as Figure ?? throw new OctaveException("print: invalid figure handle"); continue; }
                    var s = S(v, "print");
                    if (s.StartsWith("-d")) dev = s[2..];
                    else if (s.StartsWith("-S")) { var p = s[2..].Split(','); w = int.Parse(p[0]); h = int.Parse(p[1]); }
                    else if (s.StartsWith("-r") || s.StartsWith("-")) { }
                    else file = s;
                }
                if (file == null) throw new OctaveException("print: no output file specified");
                if (!Path.HasExtension(file)) file += "." + (dev switch { null => "png", "jpeg" or "jpg" => "jpg", "svg" => "svg", "bmp" => "bmp", "png" => "png", _ => dev });
                Export(it, f, file, w, h);
                return Array.Empty<Value>();
            });
            RegG("saveas", (it, a, n) =>
            {
                var f = it.Graphics.Lookup(D(a[0], "saveas")) as Figure ?? throw new OctaveException("saveas: invalid figure handle");
                var file = S(a[1], "saveas");
                if (a.Length > 2 && !Path.HasExtension(file)) file += "." + S(a[2], "saveas");
                Export(it, f, file, 0, 0);
                return Array.Empty<Value>();
            });
        }

        static void Export(Interpreter it, Figure f, string file, int w, int h)
        {
            if (it.Graphics.Backend == null) throw new OctaveException("print: no graphics toolkit available (start MatRewrite with graphics enabled)");
            it.Graphics.Backend.Export(f, Path.GetFullPath(file), w > 0 ? w : f.Width * 2, h > 0 ? h : f.Height * 2);
        }

        static string LabelText(Value[] a, string fn)
        {
            if (a.Length == 0) throw new OctaveException($"Invalid call to {fn}");
            var v = a[0] is MatrixValue m && !m.IsChar && m.Numel == 1 && a.Length > 1 ? a[1] : a[0];
            if (v is CellValue c) return string.Join("\n", c.Data.Select(x => S(x, fn)));
            var mv = M(v, fn);
            return string.Join("\n", Enumerable.Range(0, mv.R).Select(mv.RowString));
        }

        static Value[] AxLim(Interpreter it, Value[] a, int n, bool x)
        {
            var ax = it.Graphics.Gca();
            if (a.Length == 0) { var (xl, yl) = Limits(ax); return Out(MatrixValue.Row(x ? xl : yl)); }
            if (IsString(a[0]))
            {
                var s = S(a[0], x ? "xlim" : "ylim");
                if (s is "auto") { if (x) ax.XLim = null; else ax.YLim = null; }
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            }
            var m = M(a[0], x ? "xlim" : "ylim");
            if (m.Numel != 2) throw new OctaveException($"{(x ? "xlim" : "ylim")}: LIMITS must be a 2-element vector");
            if (x) ax.XLim = m.Re.ToArray(); else ax.YLim = m.Re.ToArray();
            it.Graphics.Touch(ax.Figure);
            return Array.Empty<Value>();
        }

        /// <summary>Current axis limits: explicit, or computed from the data like Octave's auto mode (tight for images).</summary>
        public static (double[] x, double[] y) Limits(Axes ax)
        {
            double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = x0, y1 = x1;
            void Acc(IEnumerable<double> xs, IEnumerable<double> ys)
            {
                foreach (var v in xs.Where(double.IsFinite)) { x0 = Math.Min(x0, v); x1 = Math.Max(x1, v); }
                foreach (var v in ys.Where(double.IsFinite)) { y0 = Math.Min(y0, v); y1 = Math.Max(y1, v); }
            }
            foreach (var s in ax.Children)
                switch (s)
                {
                    case LineSeries l: Acc(l.X, l.Y); break;
                    case ScatterSeries sc: Acc(sc.X, sc.Y); break;
                    case BarSeries b: Acc(b.X.Select(v => v - b.Width / 2).Concat(b.X.Select(v => v + b.Width / 2)), b.Y.Append(0)); break;
                    case ImageSeries im: Acc(new[] { im.X0 - 0.5, im.X1 + 0.5 }, new[] { im.Y0 - 0.5, im.Y1 + 0.5 }); break;
                }
            if (x0 > x1) { x0 = 0; x1 = 1; }
            if (y0 > y1) { y0 = 0; y1 = 1; }
            if (x0 == x1) { x0 -= 1; x1 += 1; }
            if (y0 == y1) { y0 -= 1; y1 += 1; }
            if (!ax.Tight && !ax.Children.Any(c => c is ImageSeries))
            {
                (x0, x1) = Nice(x0, x1); (y0, y1) = Nice(y0, y1);
            }
            return (ax.XLim ?? new[] { x0, x1 }, ax.YLim ?? new[] { y0, y1 });
        }

        static (double, double) Nice(double lo, double hi)
        {
            double span = hi - lo;
            double step = Math.Pow(10, Math.Floor(Math.Log10(span)));
            if (span / step < 2.5) step /= 5; else if (span / step < 5) step /= 2;
            return (Math.Floor(lo / step + 1e-9) * step, Math.Ceiling(hi / step - 1e-9) * step);
        }

        static Axes PrepareAxes(Interpreter it)
        {
            var ax = it.Graphics.Gca();
            if (!ax.Hold) ax.Reset();
            return ax;
        }

        /// <summary>Parse a line spec like "r--o"; false if it is not a valid format.</summary>
        static bool ParseFmt(string s, out LineStyle? style, out Marker? marker, out Rgb? color)
        {
            style = null; marker = null; color = null;
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '-') { style = LineStyle.Dashed; i += 2; continue; }
                if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '.') { style = LineStyle.DashDot; i += 2; continue; }
                if (s[i] == '-') { style = LineStyle.Solid; i++; continue; }
                if (s[i] == ':') { style = LineStyle.Dotted; i++; continue; }
                var c = Rgb.FromChar(s[i]);
                if (c != null) { color = c; i++; continue; }
                Marker? m = s[i] switch
                {
                    'o' => Marker.Circle, '+' => Marker.Plus, '*' => Marker.Star, '.' => Marker.Point, 'x' => Marker.Cross,
                    's' => Marker.Square, 'd' => Marker.Diamond, '^' => Marker.TriangleUp, 'v' => Marker.TriangleDown,
                    '<' => Marker.TriangleLeft, '>' => Marker.TriangleRight, 'p' => Marker.Pentagram, 'h' => Marker.Hexagram, _ => null
                };
                if (m == null) return false;
                marker = m; i++;
            }
            return true;
        }

        static readonly HashSet<string> LineProps = new(StringComparer.OrdinalIgnoreCase)
        { "color", "linewidth", "linestyle", "marker", "markersize", "displayname", "markerfacecolor", "markeredgecolor" };

        static List<double> Plot(Interpreter it, Value[] a, string fn)
        {
            var ax = PrepareAxes(it);
            var handles = new List<double>();
            int k = 0;
            var made = new List<LineSeries>();
            while (k < a.Length)
            {
                if (IsString(a[k]) && LineProps.Contains(S(a[k], fn))) break;
                var first = M(a[k], fn); k++;
                MatrixValue? second = null;
                if (k < a.Length && !IsString(a[k])) { second = M(a[k], fn); k++; }
                string? spec = null;
                if (k < a.Length && IsString(a[k]) && !LineProps.Contains(S(a[k], fn))) { spec = S(a[k], fn); k++; }
                LineStyle? st = null; Marker? mk = null; Rgb? col = null;
                if (spec != null && !ParseFmt(spec, out st, out mk, out col)) throw new OctaveException($"{fn}: invalid line specification '{spec}'");
                foreach (var (x, y) in Pairs(first, second, fn))
                {
                    var s = new LineSeries { X = x, Y = y, Color = col ?? ax.NextColor() };
                    if (mk != null) { s.Marker = mk.Value; s.Style = st ?? LineStyle.None; }
                    else s.Style = st ?? LineStyle.Solid;
                    it.Graphics.Add(ax, s);
                    made.Add(s);
                    handles.Add(s.Handle);
                }
            }
            if (k < a.Length) foreach (var s in made) SetProps(it, s, a.Skip(k).ToArray(), fn);
            return handles;
        }

        static IEnumerable<(double[] x, double[] y)> Pairs(MatrixValue first, MatrixValue? second, string fn)
        {
            if (second == null)
            {
                var y = first;
                if (y.IsComplex) { foreach (var col in Columns(y)) yield return (col, Columns(new MatrixValue(y.R, y.C, y.Im!)).First()); yield break; }
                bool vec = y.R == 1 || y.C == 1;
                if (vec) { yield return (Enumerable.Range(1, y.Numel).Select(i => (double)i).ToArray(), y.Re.ToArray()); yield break; }
                var xs = Enumerable.Range(1, y.R).Select(i => (double)i).ToArray();
                foreach (var c in Columns(y)) yield return (xs, c);
                yield break;
            }
            var X = first; var Y = second;
            bool xv = X.R == 1 || X.C == 1, yv = Y.R == 1 || Y.C == 1;
            if (xv && yv)
            {
                if (X.Numel != Y.Numel) throw new OctaveException($"{fn}: vector lengths must match");
                yield return (X.Re.ToArray(), Y.Re.ToArray());
            }
            else if (xv)
            {
                var ycols = X.Numel == Y.R ? Columns(Y) : X.Numel == Y.C ? Columns(Transpose(Y)) : throw new OctaveException($"{fn}: vector lengths must match");
                foreach (var c in ycols) yield return (X.Re.ToArray(), c);
            }
            else if (yv)
            {
                var xcols = Y.Numel == X.R ? Columns(X) : Y.Numel == X.C ? Columns(Transpose(X)) : throw new OctaveException($"{fn}: vector lengths must match");
                foreach (var c in xcols) yield return (c, Y.Re.ToArray());
            }
            else
            {
                if (X.R != Y.R || X.C != Y.C) throw new OctaveException($"{fn}: matrix dimensions must match");
                foreach (var (xc, yc) in Columns(X).Zip(Columns(Y))) yield return (xc, yc);
            }
        }

        static MatrixValue Transpose(MatrixValue m)
        {
            var r = new MatrixValue(m.C, m.R);
            for (int i = 0; i < m.R; i++) for (int j = 0; j < m.C; j++) r.Re[j + i * m.C] = m.Re[i + j * m.R];
            return r;
        }

        static IEnumerable<double[]> Columns(MatrixValue m)
        {
            for (int j = 0; j < m.C; j++) yield return m.Re.Skip(j * m.R).Take(m.R).ToArray();
        }

        static Value Bar(Interpreter it, Value[] a, int n, bool horiz, string fn)
        {
            if (a.Length == 0) throw new OctaveException($"Invalid call to {fn}");
            MatrixValue? xm = null, ym;
            int k = 0;
            var m0 = M(a[0], fn); k = 1;
            if (k < a.Length && a[k] is MatrixValue m1 && !m1.IsChar && !(m1.Numel == 1 && m0.Numel > 1)) { xm = m0; ym = m1; k++; }
            else ym = m0;
            double width = 0.8;
            if (k < a.Length && a[k] is MatrixValue wm && !wm.IsChar && wm.Numel == 1) { width = wm.Re[0]; k++; }
            Rgb? col = null;
            for (; k < a.Length; k++)
                if (IsString(a[k]))
                {
                    var s = S(a[k], fn);
                    if (s.Length == 1 && Rgb.FromChar(s[0]) is Rgb c) col = c;
                    else if (s.Equals("facecolor", StringComparison.OrdinalIgnoreCase) && k + 1 < a.Length) { col = ParseColor(a[++k], fn); }
                }
            var ax = PrepareAxes(it);
            bool vec = ym.R == 1 || ym.C == 1;
            var cols = vec ? new List<double[]> { ym.Re.ToArray() } : Columns(ym).ToList();
            int rows = cols[0].Length;
            var xs = xm?.Re.ToArray() ?? Enumerable.Range(1, rows).Select(i => (double)i).ToArray();
            if (xs.Length != rows) throw new OctaveException($"{fn}: length of X and Y must be equal");
            double last = 0;
            for (int g = 0; g < cols.Count; g++)
            {
                var s = it.Graphics.Add(ax, new BarSeries { X = xs, Y = cols[g], Width = width, Color = col ?? ax.NextColor(), GroupIndex = g, GroupCount = cols.Count, Horizontal = horiz });
                last = s.Handle;
            }
            return Num(last);
        }

        static Rgb ParseColor(Value v, string fn)
        {
            if (v is MatrixValue m && !m.IsChar && m.Numel == 3) return new Rgb(m.Re[0], m.Re[1], m.Re[2]);
            return Rgb.FromName(S(v, fn)) ?? throw new OctaveException($"{fn}: invalid color specification");
        }

        static void SetProps(Interpreter it, GObject o, Value[] a, string fn)
        {
            if (a.Length % 2 != 0) throw new OctaveException($"{fn}: invalid number of PROPERTY/VALUE arguments");
            for (int k = 0; k < a.Length; k += 2)
            {
                var p = S(a[k], fn).ToLowerInvariant();
                var v = a[k + 1];
                switch (o, p)
                {
                    case (_, "displayname"): o.DisplayName = S(v, fn); break;
                    case (LineSeries l, "color"): l.Color = ParseColor(v, fn); break;
                    case (LineSeries l, "linewidth"): l.LineWidth = D(v, fn); break;
                    case (LineSeries l, "markersize"): l.MarkerSize = D(v, fn); break;
                    case (LineSeries l, "markerfacecolor"): l.MarkerFilled = !(IsString(v) && S(v, fn) == "none"); break;
                    case (LineSeries, "markeredgecolor"): break;
                    case (LineSeries l, "linestyle"):
                        l.Style = S(v, fn) switch { "-" => LineStyle.Solid, "--" => LineStyle.Dashed, ":" => LineStyle.Dotted, "-." => LineStyle.DashDot, "none" => LineStyle.None, var s => throw new OctaveException($"{fn}: invalid LineStyle '{s}'") };
                        break;
                    case (LineSeries l, "marker"):
                    {
                        var s = S(v, fn);
                        if (s == "none") l.Marker = Marker.None;
                        else if (ParseFmt(s, out _, out var mk, out _) && mk != null) l.Marker = mk.Value;
                        else throw new OctaveException($"{fn}: invalid Marker '{s}'");
                        break;
                    }
                    case (LineSeries l, "xdata"): l.X = M(v, fn).Re.ToArray(); break;
                    case (LineSeries l, "ydata"): l.Y = M(v, fn).Re.ToArray(); break;
                    case (ScatterSeries s, "markerfacecolor"): s.Filled = !(IsString(v) && S(v, fn) == "none"); if (!IsString(v) || S(v, fn).Length == 1) s.Color = ParseColor(v, fn); break;
                    case (ScatterSeries s, "markeredgecolor" or "color"): s.Color = ParseColor(v, fn); break;
                    case (ScatterSeries, "linewidth"): break;
                    case (BarSeries b, "facecolor"): b.Color = ParseColor(v, fn); break;
                    case (BarSeries b, "barwidth"): b.Width = D(v, fn); break;
                    case (Figure f, "name"): f.Name = S(v, fn); break;
                    case (Figure f, "numbertitle"): f.NumberTitle = S(v, fn) == "on"; break;
                    case (Figure f, "visible"): f.Visible = S(v, fn) == "on"; break;
                    case (Figure f, "position"): { var m = M(v, fn); if (m.Numel == 4) { f.Width = (int)m.Re[2]; f.Height = (int)m.Re[3]; } break; }
                    case (Figure f, "colormap"): f.Colormap = S(v, fn); break;
                    case (Axes ax, "title"): ax.Title = S(v, fn); break;
                    case (Axes ax, "xlim"): ax.XLim = M(v, fn).Re.ToArray(); break;
                    case (Axes ax, "ylim"): ax.YLim = M(v, fn).Re.ToArray(); break;
                    case (Axes ax, "xgrid" or "ygrid"): ax.Grid = S(v, fn) == "on"; break;
                    case (Axes ax, "box"): ax.Box = S(v, fn) == "on"; break;
                    case (Axes ax, "ydir"): ax.YDirReverse = S(v, fn) == "reverse"; break;
                    case (Axes ax, "nextplot"): ax.Hold = S(v, fn) == "add"; break;
                    case (_, "fontsize"): case (_, "fontname"): case (_, "fontweight"): case (_, "interpreter"): break;
                    default: throw new OctaveException($"{fn}: unknown property '{p}' for {o.GetType().Name.Replace("Series", "").ToLowerInvariant()} object");
                }
            }
            switch (o)
            {
                case Series s: it.Graphics.Touch(s.Parent.Figure); break;
                case Axes ax: it.Graphics.Touch(ax.Figure); break;
                case Figure f: it.Graphics.Touch(f); break;
            }
        }

        static Value GetProp(Interpreter it, GObject o, string p)
        {
            static Value C(Rgb c) => MatrixValue.Row(new[] { c.R, c.G, c.B });
            return (o, p.ToLowerInvariant()) switch
            {
                (_, "type") => Str(o switch { Figure => "figure", Axes => "axes", LineSeries => "line", ScatterSeries => "scatter", BarSeries => "bar", ImageSeries => "image", _ => "unknown" }),
                (_, "displayname") => Str(o.DisplayName),
                (LineSeries l, "color") => C(l.Color),
                (LineSeries l, "linewidth") => Num(l.LineWidth),
                (LineSeries l, "xdata") => MatrixValue.Row(l.X),
                (LineSeries l, "ydata") => MatrixValue.Row(l.Y),
                (LineSeries l, "linestyle") => Str(l.Style switch { LineStyle.Solid => "-", LineStyle.Dashed => "--", LineStyle.Dotted => ":", LineStyle.DashDot => "-.", _ => "none" }),
                (Series s, "parent") => Num(s.Parent.Handle),
                (Figure f, "name") => Str(f.Name),
                (Figure f, "children") => MatrixValue.Col(f.Axes.Select(x => x.Handle).Reverse()),
                (Figure f, "number") => Num(f.Handle),
                (Axes ax, "children") => MatrixValue.Col(ax.Children.Select(x => x.Handle).Reverse()),
                (Axes ax, "parent") => Num(ax.Figure.Handle),
                (Axes ax, "title") => Str(ax.Title),
                (Axes ax, "xlabel") => Str(ax.XLabel),
                (Axes ax, "ylabel") => Str(ax.YLabel),
                (Axes ax, "xlim") => MatrixValue.Row(Limits(ax).x),
                (Axes ax, "ylim") => MatrixValue.Row(Limits(ax).y),
                (Axes ax, "nextplot") => Str(ax.Hold ? "add" : "replace"),
                (Axes ax, "xgrid") => Str(ax.Grid ? "on" : "off"),
                _ => throw new OctaveException($"get: unknown property '{p}'")
            };
        }
    }
}
