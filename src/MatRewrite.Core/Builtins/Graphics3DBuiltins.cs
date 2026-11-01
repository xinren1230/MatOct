// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static double[,] Grid2(MatrixValue m)
        {
            var g = new double[m.R, m.C];
            for (int i = 0; i < m.R; i++) for (int j = 0; j < m.C; j++) g[i, j] = m.Re[i + j * m.R];
            return g;
        }

        static MatrixValue FromGrid(double[,] g)
        {
            int R = g.GetLength(0), C = g.GetLength(1);
            var m = new MatrixValue(R, C);
            for (int i = 0; i < R; i++) for (int j = 0; j < C; j++) m.Re[i + j * R] = g[i, j];
            return m;
        }

        /// <summary>Expand X/Y (vectors or matrices) to full grids matching Z's size.</summary>
        static (double[,] X, double[,] Y) MeshXY(MatrixValue? x, MatrixValue? y, int R, int C, string fn)
        {
            var X = new double[R, C]; var Y = new double[R, C];
            for (int i = 0; i < R; i++)
                for (int j = 0; j < C; j++)
                {
                    X[i, j] = x == null ? j + 1 : (x.R == 1 || x.C == 1) ? (x.Numel == C ? x.Re[j] : throw new OctaveException($"{fn}: X must have length columns(Z)")) : (x.R == R && x.C == C ? x.Re[i + j * R] : throw new OctaveException($"{fn}: X and Z must have the same size"));
                    Y[i, j] = y == null ? i + 1 : (y.R == 1 || y.C == 1) ? (y.Numel == R ? y.Re[i] : throw new OctaveException($"{fn}: Y must have length rows(Z)")) : (y.R == R && y.C == C ? y.Re[i + j * R] : throw new OctaveException($"{fn}: Y and Z must have the same size"));
                }
            return (X, Y);
        }

        /// <summary>Vector grid coordinates for contour (from vectors or meshgrid matrices).</summary>
        static double[] GridVec(MatrixValue? v, int n, bool cols, string fn)
        {
            if (v == null) return Enumerable.Range(1, n).Select(i => (double)i).ToArray();
            if (v.R == 1 || v.C == 1) return v.Numel == n ? v.Re.ToArray() : throw new OctaveException($"{fn}: X and Y must match the size of Z");
            return cols ? Enumerable.Range(0, v.C).Select(j => v.Re[j * v.R]).ToArray() : Enumerable.Range(0, v.R).Select(i => v.Re[i]).ToArray();
        }

        static Value ContourMatrix(List<(double level, double[] x, double[] y)> lines)
        {
            int total = lines.Sum(l => l.x.Length + 1);
            var m = new MatrixValue(2, total);
            int c = 0;
            foreach (var (lv, x, y) in lines)
            {
                m.Re[2 * c] = lv; m.Re[2 * c + 1] = x.Length; c++;
                for (int k = 0; k < x.Length; k++, c++) { m.Re[2 * c] = x[k]; m.Re[2 * c + 1] = y[k]; }
            }
            return m;
        }

        static void Set3DView(Axes ax) { if (!ax.Is3D) { ax.Az = -37.5; ax.El = 30; } }

        static void RegisterGraphics3D()
        {
            RegG("plot3", (it, a, n) =>
            {
                var ax = PrepareAxes(it);
                var hs = new List<double>();
                var made = new List<LineSeries>();
                int k = 0;
                while (k + 2 < a.Length + 0 && !IsString(a[k]))
                {
                    var x = M(a[k], "plot3"); var y = M(a[k + 1], "plot3"); var z = M(a[k + 2], "plot3"); k += 3;
                    LineStyle? st = null; Marker? mk = null; Rgb? col = null;
                    if (k < a.Length && IsString(a[k]) && !LineProps.Contains(S(a[k], "plot3")))
                    {
                        var spec = S(a[k++], "plot3");
                        if (!ParseFmt(spec, out st, out mk, out col)) throw new OctaveException($"plot3: invalid line specification '{spec}'");
                    }
                    bool vec = x.R == 1 || x.C == 1;
                    var xs = vec ? new List<double[]> { x.Re.ToArray() } : Columns(x).ToList();
                    var ys = vec ? new List<double[]> { y.Re.ToArray() } : Columns(y).ToList();
                    var zs = vec ? new List<double[]> { z.Re.ToArray() } : Columns(z).ToList();
                    for (int c = 0; c < xs.Count; c++)
                    {
                        if (xs[c].Length != ys[c].Length || ys[c].Length != zs[c].Length) throw new OctaveException("plot3: X, Y, and Z must have the same size");
                        var s = new LineSeries { X = xs[c], Y = ys[c], Z = zs[c], Color = col ?? ax.NextColor() };
                        if (mk != null) { s.Marker = mk.Value; s.Style = st ?? LineStyle.None; } else s.Style = st ?? LineStyle.Solid;
                        it.Graphics.Add(ax, s); made.Add(s); hs.Add(s.Handle);
                    }
                }
                if (made.Count == 0) throw new OctaveException("Invalid call to plot3");
                if (k < a.Length) foreach (var s in made) SetProps(it, s, a.Skip(k).ToArray(), "plot3");
                Set3DView(ax);
                return n > 0 ? Out(MatrixValue.Col(hs)) : Array.Empty<Value>();
            });

            BuiltinFn surfFn(bool mesh, string fn) => (it, a, n) =>
            {
                var nums = a.TakeWhile(v => !IsString(v)).Select(v => M(v, fn)).ToList();
                MatrixValue? x = null, y = null, z, c = null;
                switch (nums.Count)
                {
                    case 1: z = nums[0]; break;
                    case 2: z = nums[0]; c = nums[1]; break;
                    case 3: x = nums[0]; y = nums[1]; z = nums[2]; break;
                    case 4: x = nums[0]; y = nums[1]; z = nums[2]; c = nums[3]; break;
                    default: throw new OctaveException($"Invalid call to {fn}");
                }
                if (z.R < 2 || z.C < 2) throw new OctaveException($"{fn}: Z must be a matrix with at least 2 rows and columns");
                var ax = PrepareAxes(it);
                var (X, Y) = MeshXY(x, y, z.R, z.C, fn);
                var Z = Grid2(z);
                var s = new SurfaceSeries { X = X, Y = Y, Z = Z, C = c != null ? Grid2(c) : Z, Mesh = mesh };
                if (c != null && (c.R != z.R || c.C != z.C)) throw new OctaveException($"{fn}: C must have the same size as Z");
                it.Graphics.Add(ax, s);
                var rest = a.Skip(nums.Count).ToArray();
                if (rest.Length > 0) SetProps(it, s, rest, fn);
                Set3DView(ax);
                return n > 0 ? Out(H(s)) : Array.Empty<Value>();
            };
            RegG("surf", surfFn(false, "surf"));
            RegG("mesh", surfFn(true, "mesh"));
            RegG("shading", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                var mode = S(a[^1], "shading").ToLowerInvariant();
                if (mode is not ("flat" or "faceted" or "interp")) throw new OctaveException($"shading: unknown argument '{mode}'");
                foreach (var s in ax.Children.OfType<SurfaceSeries>()) s.Shading = mode;
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });

            Value[] ContourCall(Interpreter it, Value[] a, int n, bool filled, string fn)
            {
                var nums = a.TakeWhile(v => !IsString(v)).Select(v => M(v, fn)).ToList();
                MatrixValue? x = null, y = null, z, lv = null;
                switch (nums.Count)
                {
                    case 1: z = nums[0]; break;
                    case 2: z = nums[0]; lv = nums[1]; break;
                    case 3: x = nums[0]; y = nums[1]; z = nums[2]; break;
                    case 4: x = nums[0]; y = nums[1]; z = nums[2]; lv = nums[3]; break;
                    default: throw new OctaveException($"Invalid call to {fn}");
                }
                if (z.R < 2 || z.C < 2) throw new OctaveException($"{fn}: Z must be a 2-D matrix of at least 2x2");
                var Z = Grid2(z);
                var xs = GridVec(x, z.C, true, fn); var ys = GridVec(y, z.R, false, fn);
                double[] levels = lv == null ? Contour.AutoLevels(Z)
                    : lv.Numel == 1 ? (lv.Re[0] == Math.Floor(lv.Re[0]) && lv.Re[0] > 0 ? LevelsN(Z, (int)lv.Re[0]) : new[] { lv.Re[0] })
                    : lv.Numel == 2 && lv.Re[0] == lv.Re[1] ? new[] { lv.Re[0] } : lv.Re.OrderBy(v => v).ToArray();
                var lines = Contour.All(xs, ys, Z, levels);
                if (fn == "contourc") return Out(ContourMatrix(lines));
                var ax = PrepareAxes(it);
                var s = new ContourSeries { X = xs, Y = ys, Z = Z, Levels = levels, Filled = filled, Lines = lines };
                int k = nums.Count;
                if (k < a.Length && IsString(a[k]) && ParseFmt(S(a[k], fn), out _, out _, out var col) && S(a[k], fn).Length <= 4) { s.LineColor = col; k++; }
                it.Graphics.Add(ax, s);
                if (k < a.Length) SetProps(it, s, a.Skip(k).ToArray(), fn);
                return n switch { 0 => Array.Empty<Value>(), 1 => Out(ContourMatrix(lines)), _ => new Value[] { ContourMatrix(lines), H(s) } };
            }
            static double[] LevelsN(double[,] Z, int nl)
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                foreach (var v in Z) if (double.IsFinite(v)) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                // Octave: n levels evenly spaced strictly inside [min, max]
                return Enumerable.Range(1, nl).Select(i => lo + i * (hi - lo) / (nl + 1)).ToArray();
            }
            RegG("contour", (it, a, n) => ContourCall(it, a, n, false, "contour"));
            RegG("contourf", (it, a, n) => ContourCall(it, a, n, true, "contourf"));
            Reg("contourc", (it, a, n) => ContourCall(it, a, n, false, "contourc"));

            RegG("view", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length == 0)
                {
                    if (n >= 2) return new Value[] { Num(ax.Az), Num(ax.El) };
                    return Out(MatrixValue.Row(new[] { ax.Az, ax.El }));
                }
                if (a.Length == 1)
                {
                    var m = M(a[0], "view");
                    if (m.Numel == 1 && m.Re[0] == 2) { ax.Az = 0; ax.El = 90; }
                    else if (m.Numel == 1 && m.Re[0] == 3) { ax.Az = -37.5; ax.El = 30; }
                    else if (m.Numel == 2) { ax.Az = m.Re[0]; ax.El = m.Re[1]; }
                    else throw new OctaveException("view: VIEW must be [AZ EL], 2, or 3");
                }
                else { ax.Az = D(a[0], "view"); ax.El = D(a[1], "view"); }
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });
            RegG("rotate3d", (it, a, n) => Array.Empty<Value>()); // figures are always rotatable by mouse
            RegG("zlabel", (it, a, n) => { var ax = it.Graphics.Gca(); ax.ZLabel = LabelText(a, "zlabel"); it.Graphics.Touch(ax.Figure); return Array.Empty<Value>(); });
            RegG("zlim", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                if (a.Length == 0) return Out(MatrixValue.Row(ZLimits(ax)));
                if (IsString(a[0])) { if (S(a[0], "zlim") == "auto") ax.ZLim = null; }
                else
                {
                    var m = M(a[0], "zlim");
                    if (m.Numel != 2) throw new OctaveException("zlim: LIMITS must be a 2-element vector");
                    ax.ZLim = m.Re.ToArray();
                }
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });

            BuiltinFn logFn(bool lx, bool ly, string fn) => (it, a, n) =>
            {
                var hs = Plot(it, a, fn);
                var ax = it.Graphics.Gca();
                if (ax.Hold) { ax.XLog |= lx; ax.YLog |= ly; } else { ax.XLog = lx; ax.YLog = ly; }
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(MatrixValue.Col(hs)) : Array.Empty<Value>();
            };
            RegG("semilogx", logFn(true, false, "semilogx"));
            RegG("semilogy", logFn(false, true, "semilogy"));
            RegG("loglog", logFn(true, true, "loglog"));

            RegG("errorbar", (it, a, n) =>
            {
                var nums = a.TakeWhile(v => !IsString(v)).Select(v => M(v, "errorbar").Re.ToArray()).ToList();
                double[] x, y, l, u;
                switch (nums.Count)
                {
                    case 2: y = nums[0]; x = Enumerable.Range(1, y.Length).Select(i => (double)i).ToArray(); l = u = nums[1]; break;
                    case 3: x = nums[0]; y = nums[1]; l = u = nums[2]; break;
                    case 4: x = nums[0]; y = nums[1]; l = nums[2]; u = nums[3]; break;
                    default: throw new OctaveException("Invalid call to errorbar");
                }
                if (x.Length != y.Length || l.Length != y.Length || u.Length != y.Length) throw new OctaveException("errorbar: X, Y, and error arrays must have the same size");
                var ax = PrepareAxes(it);
                var s = new ErrorBarSeries { X = x, Y = y, L = l, U = u, Color = ax.NextColor() };
                for (int k = nums.Count; k < a.Length; k++)
                {
                    var spec = S(a[k], "errorbar");
                    if (spec is "horizontal" or "~>") { s.Horizontal = true; continue; }
                    if (spec is "vertical" or "~") continue;
                    if (!ParseFmt(spec, out var st, out var mk, out var col)) throw new OctaveException($"errorbar: invalid format '{spec}'");
                    if (st != null) s.Style = st.Value; if (mk != null) { s.Marker = mk.Value; if (st == null) s.Style = LineStyle.None; } if (col != null) s.Color = col.Value;
                }
                it.Graphics.Add(ax, s);
                return n > 0 ? Out(H(s)) : Array.Empty<Value>();
            });

            RegG("text", (it, a, n) =>
            {
                if (a.Length < 3) throw new OctaveException("Invalid call to text");
                var ax = it.Graphics.Gca();
                bool has3 = a.Length >= 4 && !IsString(a[2]) && a[2] is MatrixValue;
                var xs = M(a[0], "text").Re; var ys = M(a[1], "text").Re;
                var zs = has3 ? M(a[2], "text").Re : null;
                var tv = a[has3 ? 3 : 2];
                var texts = tv is CellValue cv ? cv.Data.Select(v => S(v, "text")).ToArray() : new[] { LabelText(new[] { tv }, "text") };
                var made = new List<double>();
                for (int i = 0; i < xs.Length; i++)
                {
                    var s = new TextSeries { X = xs[i], Y = ys[i], Z = zs?[i], Text = texts[Math.Min(i, texts.Length - 1)] };
                    it.Graphics.Add(ax, s);
                    var rest = a.Skip(has3 ? 4 : 3).ToArray();
                    if (rest.Length > 0) SetProps(it, s, rest, "text");
                    made.Add(s.Handle);
                }
                return n > 0 ? Out(MatrixValue.Col(made)) : Array.Empty<Value>();
            });

            RegG("annotation", (it, a, n) =>
            {
                int k = 0;
                var f = it.Graphics.Gcf();
                if (a.Length > 0 && !IsString(a[0])) { if (it.Graphics.Lookup(D(a[0], "annotation")) is Figure ff) f = ff; k = 1; }
                if (k >= a.Length) throw new OctaveException("Invalid call to annotation");
                var kind = S(a[k++], "annotation").ToLowerInvariant();
                if (kind is not ("line" or "arrow" or "doublearrow" or "textarrow" or "textbox" or "rectangle" or "ellipse"))
                    throw new OctaveException($"annotation: unknown annotation type '{kind}'");
                var an = it.Graphics.Register(new Annotation { Kind = kind });
                if (k < a.Length && !IsString(a[k]))
                {
                    var m1 = M(a[k++], "annotation");
                    if (m1.Numel == 4 && (kind is "textbox" or "rectangle" or "ellipse")) an.Position = m1.Re.ToArray();
                    else
                    {
                        if (m1.Numel != 2 || k >= a.Length) throw new OctaveException("annotation: X and Y must be 2-element vectors");
                        var m2 = M(a[k++], "annotation");
                        an.X = m1.Re.ToArray(); an.Y = m2.Re.ToArray();
                    }
                }
                if (kind is "textbox" or "rectangle" or "ellipse" && an.Position == null) an.Position = new[] { 0.3, 0.3, 0.1, 0.1 };
                for (; k + 1 < a.Length; k += 2)
                {
                    var p = S(a[k], "annotation").ToLowerInvariant();
                    switch (p)
                    {
                        case "string": an.Text = a[k + 1] is CellValue c ? string.Join("\n", c.Data.Select(v => S(v, "annotation"))) : S(a[k + 1], "annotation"); break;
                        case "color": case "edgecolor": an.Color = ParseColor(a[k + 1], "annotation"); break;
                        case "fontsize": an.FontSize = D(a[k + 1], "annotation"); break;
                        case "position": an.Position = M(a[k + 1], "annotation").Re.ToArray(); break;
                        case "x": an.X = M(a[k + 1], "annotation").Re.ToArray(); break;
                        case "y": an.Y = M(a[k + 1], "annotation").Re.ToArray(); break;
                        default: break; // linewidth, headstyle, fitboxtotext, ... accepted and ignored
                    }
                }
                f.Annotations.Add(an);
                it.Graphics.Touch(f);
                return n > 0 ? Out(H(an)) : Array.Empty<Value>();
            });
        }

        /// <summary>Data limits along Z (3-D axes).</summary>
        public static double[] ZLimits(Axes ax)
        {
            if (ax.ZLim != null) return ax.ZLim;
            double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
            foreach (var s in ax.Children)
            {
                IEnumerable<double> zs = s switch
                {
                    LineSeries l when l.Z != null => l.Z,
                    SurfaceSeries sf => sf.Z.Cast<double>(),
                    TextSeries t when t.Z != null => new[] { t.Z.Value },
                    _ => Array.Empty<double>()
                };
                foreach (var v in zs) if (double.IsFinite(v)) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            }
            if (lo > hi) { lo = 0; hi = 1; }
            if (lo == hi) { lo -= 1; hi += 1; }
            var (a, b) = Nice(lo, hi);
            return new[] { a, b };
        }
    }
}
