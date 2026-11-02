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
            // surfc / meshc: surface plus contour lines on the floor (z = min Z)
            BuiltinFn withContour(bool mesh, string fn) => (it, a, n) =>
            {
                var r = surfFn(mesh, fn)(it, a, 1);
                var ax = it.Graphics.Gca();
                var sf = ax.Children.OfType<SurfaceSeries>().Last();
                var Z = sf.Z; int R = Z.GetLength(0), C = Z.GetLength(1);
                var xs = Enumerable.Range(0, C).Select(j => sf.X[0, j]).ToArray();
                var ys = Enumerable.Range(0, R).Select(i => sf.Y[i, 0]).ToArray();
                double zmin = double.PositiveInfinity; foreach (var v in Z) if (double.IsFinite(v)) zmin = Math.Min(zmin, v);
                var levels = Contour.AutoLevels(Z);
                var cs = new ContourSeries { X = xs, Y = ys, Z = Z, Levels = levels, Lines = Contour.All(xs, ys, Z, levels), ZPlane = zmin };
                it.Graphics.Add(ax, cs);
                return n > 0 ? new Value[] { MatrixValue.Col(new[] { sf.Handle, cs.Handle }) } : Array.Empty<Value>();
            };
            RegG("surfc", withContour(false, "surfc"));
            RegG("meshc", withContour(true, "meshc"));
            RegG("contour3", (it, a, n) =>
            {
                var r = ContourCall(it, a, Math.Max(n, 2), false, "contour3");
                var ax = it.Graphics.Gca();
                var cs = ax.Children.OfType<ContourSeries>().Last();
                cs.Contour3 = true;
                Set3DView(ax);
                return n switch { 0 => Array.Empty<Value>(), 1 => new[] { r[0] }, _ => r };
            });
            RegG("clabel", (it, a, n) =>
            {
                if (a.Length == 0) throw new OctaveException("Invalid call to clabel");
                var C = M(a[0], "clabel");
                if (C.R != 2) throw new OctaveException("clabel: CTBL must be a 2-row contour matrix");
                ContourSeries? owner = null;
                int k = 1;
                if (a.Length > 1 && !IsString(a[1]) && M(a[1], "clabel").Numel == 1 && it.Graphics.Lookup(D(a[1], "clabel")) is ContourSeries cso) { owner = cso; k = 2; }
                HashSet<double>? only = null;
                if (k < a.Length && !IsString(a[k])) { only = M(a[k], "clabel").Re.ToHashSet(); k++; }
                var ax = it.Graphics.Gca();
                var hs = new List<double>();
                int col = 0, total = C.C;
                while (col < total)
                {
                    double lev = C.Re[2 * col]; int np = (int)C.Re[2 * col + 1];
                    if (np <= 0 || col + np >= total + 1) break;
                    if (only == null || only.Contains(lev))
                    {
                        int mid = col + 1 + np / 2;
                        var t = new TextSeries { X = C.Re[2 * mid], Y = C.Re[2 * mid + 1], Text = lev.ToString("G4", System.Globalization.CultureInfo.InvariantCulture), FontSize = 8, HAlign = "center" };
                        if (owner != null && owner.Contour3) t.Z = lev;
                        else if (owner?.ZPlane is double zp) t.Z = zp;
                        it.Graphics.Add(ax, t); hs.Add(t.Handle);
                    }
                    col += np + 1;
                }
                if (k < a.Length) foreach (var h in hs) SetProps(it, it.Graphics.Lookup(h)!, a.Skip(k).ToArray(), "clabel");
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(MatrixValue.Col(hs)) : Array.Empty<Value>();
            });
            RegG("light", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                var lo = new LightObject { Az = -37.5 + 90 + 45, El = 45 };
                // default position [1 0 1]
                lo.Az = 90; lo.El = 45;
                it.Graphics.Add(ax, lo);
                if (a.Length > 0) SetProps(it, lo, a, "light");
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(H(lo)) : Array.Empty<Value>();
            });
            RegG("camlight", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                LightObject? lo = null;
                int k = 0;
                if (a.Length > 4 || a.Any(v => v is CellValue)) throw new OctaveException("Invalid call to camlight");
                if (a.Length > 0 && !IsString(a[0]) && a[0].Numel == 1 && it.Graphics.Lookup(D(a[0], "camlight")) is { } hobj)
                {
                    if (hobj is LightObject existing) lo = existing;
                    else if (hobj is Axes hax) ax = hax;
                    else throw new OctaveException("camlight: HL must be a handle to an axes or light object");
                    k++;
                }
                if (a.Length - k > 3) throw new OctaveException("Invalid call to camlight");
                if (k < a.Length && IsString(a[k]) && S(a[k], "camlight").ToLowerInvariant() is var p0 && p0 is not ("headlight" or "right" or "left" or "infinite" or "local"))
                    throw new OctaveException($"camlight: invalid light position '{S(a[k], "camlight")}'");
                lo ??= it.Graphics.Add(ax, new LightObject());
                lo.CameraRelative = true; lo.Az = 30; lo.El = 30; // 'right' is the default
                var nums = new List<double>();
                for (; k < a.Length; k++)
                {
                    if (IsString(a[k]))
                    {
                        var s = S(a[k], "camlight").ToLowerInvariant();
                        switch (s)
                        {
                            case "headlight": lo.Az = 0; lo.El = 0; break;
                            case "right": lo.Az = 30; lo.El = 30; break;
                            case "left": lo.Az = -30; lo.El = 30; break;
                            case "infinite" or "local": break;
                            default: throw new OctaveException($"camlight: invalid argument '{s}'");
                        }
                    }
                    else nums.AddRange(M(a[k], "camlight").Re);
                }
                if (nums.Count >= 2) { lo.Az = nums[0]; lo.El = nums[1]; }
                it.Graphics.Touch(ax.Figure);
                return n > 0 ? Out(H(lo)) : Array.Empty<Value>();
            });
            Reg("peaks", (it, a, n) =>
            {
                MatrixValue X, Y;
                if (a.Length >= 2) { X = M(a[0], "peaks"); Y = M(a[1], "peaks"); }
                else
                {
                    double[] v = a.Length == 1 && M(a[0], "peaks").Numel > 1 ? M(a[0], "peaks").Re.ToArray()
                        : Enumerable.Range(0, a.Length == 1 ? (int)D(a[0], "peaks") : 49).Select((k, _) => 0.0).ToArray();
                    if (!(a.Length == 1 && M(a[0], "peaks").Numel > 1)) { int cnt = v.Length; for (int k = 0; k < cnt; k++) v[k] = cnt == 1 ? -3 : -3 + 6.0 * k / (cnt - 1); }
                    int N = v.Length; X = new MatrixValue(N, N); Y = new MatrixValue(N, N);
                    for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) { X.Re[i + j * N] = v[j]; Y.Re[i + j * N] = v[i]; }
                }
                if (X.Numel != Y.Numel) throw new OctaveException("peaks: X and Y must be the same size");
                var Z = new MatrixValue(X.R, X.C);
                for (int k = 0; k < Z.Re.Length; k++)
                {
                    double x = X.Re[k], y = Y.Re[k];
                    Z.Re[k] = 3 * (1 - x) * (1 - x) * Math.Exp(-x * x - (y + 1) * (y + 1)) - 10 * (x / 5 - x * x * x - Math.Pow(y, 5)) * Math.Exp(-x * x - y * y) - 1.0 / 3 * Math.Exp(-(x + 1) * (x + 1) - y * y);
                }
                if (n <= 1 || a.Length >= 2) return Out(Z);
                return new Value[] { X, Y, Z };
            });
            RegG("material", (it, a, n) => Array.Empty<Value>()); // shiny/dull/metal: accepted, fixed material
            RegG("lighting", (it, a, n) =>
            {
                var ax = it.Graphics.Gca();
                var mode = S(a[^1], "lighting").ToLowerInvariant();
                if (mode == "phong") mode = "gouraud";
                if (mode is not ("none" or "flat" or "gouraud")) throw new OctaveException($"lighting: invalid lighting mode '{mode}'");
                ax.Lighting = mode;
                foreach (var sf in ax.Children.OfType<SurfaceSeries>()) sf.FaceLighting = mode;
                it.Graphics.Touch(ax.Figure);
                return Array.Empty<Value>();
            });
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
                var raw = a.TakeWhile(v => !IsString(v)).ToList();
                if (raw.Count < 2) throw new OctaveException("Invalid call to errorbar");
                for (int q = 0; q < raw.Count; q++) if (raw[q] is not MatrixValue && raw[q] is not Sparse.SparseValue) throw new OctaveException($"errorbar: data argument {q + 1} must be numeric");
                if (raw.Count > 6) throw new OctaveException("errorbar: too many arguments to plot");
                for (int q = 1; q < raw.Count; q++) if (raw[q].Numel != raw[0].Numel) throw new OctaveException($"errorbar: size of argument {q + 1} does not match others");
                string fmt0 = raw.Count < a.Length ? S(a[raw.Count], "errorbar") : "";
                if (raw.Count == 2 && fmt0.Contains("~>")) throw new OctaveException("errorbar: 2 column errorplot is only valid for xerr");
                if (raw.Count == 6 && !(fmt0.Contains("~>") || fmt0.Contains("#~>"))) throw new OctaveException("errorbar: 6 columns only valid for xyerr and boxxy");
                if (raw.Count == 5) throw new OctaveException("errorbar: error plot requires 2, 3, 4, or 6 arguments");
                var nums = raw.Select(v => M(v, "errorbar").Re.ToArray()).ToList();
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
                var ax = it.Graphics.Gca();
                if (a.Length == 0 || (a.Length % 2 == 0 && IsString(a[0])))
                {
                    var s0 = new TextSeries { X = 0, Y = 0 };
                    it.Graphics.Add(ax, s0);
                    if (a.Length > 0) SetProps(it, s0, a, "text");
                    return n > 0 ? Out(Num(s0.Handle)) : Array.Empty<Value>();
                }
                if (a.Length < 3) throw new OctaveException("Invalid call to text");
                bool has3 = a.Length >= 4 && !IsString(a[2]) && a[2] is MatrixValue;
                var xs = M(a[0], "text").Re; var ys = M(a[1], "text").Re;
                var zs = has3 ? M(a[2], "text").Re : null;
                var tv = a[has3 ? 3 : 2];
                if (xs.Length != ys.Length || zs != null && zs.Length != xs.Length) throw new OctaveException("text: X, Y, and Z coordinates must match");
                if ((a.Length - (has3 ? 4 : 3)) % 2 != 0) throw new OctaveException("Invalid call to text");
                if (!(tv is CellValue) && !(tv is MatrixValue tcm && (tcm.IsChar || tcm.Numel == 0))) throw new OctaveException("text: STRING must be a character string");
                { int nt = tv is CellValue tcc ? tcc.Numel : ((MatrixValue)tv).R; if (xs.Length > 1 && nt > 1 && nt != xs.Length) throw new OctaveException("text: invalid combination of points and text strings"); }
                static string CS(Value v) => v is MatrixValue mm && mm.Numel == 0 ? "" : v is MatrixValue nm && !nm.IsChar ? ((MatrixValue)Num2StrValue(nm, Str("%g"))).AsString() : S(v, "text");
                var made = new List<double>();
                for (int i = 0; i < xs.Length; i++)
                {
                    Value raw; string txt;
                    if (tv is CellValue cv)
                    {
                        if (xs.Length > 1 && cv.Numel == xs.Length) { txt = CS(cv.Data[i]); raw = Str(txt); }
                        else { var col = new CellValue(cv.Numel, 1); for (int q = 0; q < cv.Numel; q++) col.Data[q] = Str(CS(cv.Data[q])); raw = col; txt = string.Join("\n", col.Data.Select(v => S(v, "text"))); }
                    }
                    else
                    {
                        var tm = (MatrixValue)tv;
                        if (xs.Length > 1 && tm.R == xs.Length && tm.R > 1) { txt = tm.RowString(i).TrimEnd(); raw = Str(txt); }
                        else { raw = tm; txt = LabelText(new[] { tv }, "text"); }
                    }
                    var s = new TextSeries { X = xs[i], Y = ys[i], Z = zs?[i], Text = txt };
                    if (raw is CellValue || raw is MatrixValue { R: > 1 }) s.Props["__string"] = raw;
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
                if (a.Length > 0 && !IsString(a[0]) && a[0] is not CellValue) { if (it.Graphics.Lookup(D(a[0], "annotation")) is Figure ff) f = ff; k = 1; }
                if (k >= a.Length || !IsString(a[k])) throw new OctaveException("Invalid call to annotation");
                var kind = S(a[k++], "annotation").ToLowerInvariant();
                if (kind is not ("line" or "arrow" or "doublearrow" or "textarrow" or "textbox" or "rectangle" or "ellipse"))
                    throw new OctaveException($"annotation: unknown annotation TYPE {kind}");
                bool boxy = kind is "textbox" or "rectangle" or "ellipse";
                double[]? pos = null, ax1 = null, ay1 = null;
                if (k < a.Length && !IsString(a[k]))
                {
                    if (boxy)
                    {
                        var m1 = a[k] as MatrixValue;
                        if (m1 == null || m1.Numel != 4 || (m1.R != 1 && m1.C != 1)) throw new OctaveException("annotation: POS must be a 4-element vector");
                        pos = m1.Re.ToArray(); k++;
                    }
                    else
                    {
                        if (k + 1 >= a.Length) throw new OctaveException("Invalid call to annotation");
                        if (a[k] is not MatrixValue m1 || a[k + 1] is not MatrixValue m2 || m1.Numel != 2 || m2.Numel != 2) throw new OctaveException("annotation: X and Y must be 2-element vectors");
                        ax1 = m1.Re.ToArray(); ay1 = m2.Re.ToArray(); k += 2;
                    }
                }
                if ((a.Length - k) % 2 != 0) { it.Warning("annotation: couldn't parse PROP/VAL pairs, skipping"); a = a.Take(k).ToArray(); }
                var an = it.Graphics.Register(new Annotation { Kind = kind });
                if (pos != null) an.Position = pos;
                if (ax1 != null) { an.X = ax1; an.Y = ay1; }
                if (kind is "textbox" or "rectangle" or "ellipse" && an.Position == null) an.Position = new[] { 0.3, 0.3, 0.1, 0.1 };
                for (; k + 1 < a.Length; k += 2)
                {
                    var p = S(a[k], "annotation").ToLowerInvariant();
                    switch (p)
                    {
                        case "string": an.Text = a[k + 1] is CellValue c ? string.Join("\n", c.Data.Select(v => S(v, "annotation"))) : S(a[k + 1], "annotation"); break;
                        case "edgecolor" when IsString(a[k + 1]) && S(a[k + 1], "annotation") == "none": an.Props["linestyle"] = a[k + 1]; break;
                        case "color": case "edgecolor": an.Color = ParseColor(a[k + 1], "annotation"); break;
                        case "fontsize": an.FontSize = D(a[k + 1], "annotation"); break;
                        case "position": an.Position = M(a[k + 1], "annotation").Re.ToArray(); break;
                        case "x": an.X = M(a[k + 1], "annotation").Re.ToArray(); break;
                        case "y": an.Y = M(a[k + 1], "annotation").Re.ToArray(); break;
                        case "linestyle": case "horizontalalignment": an.Props[p] = a[k + 1]; break;
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
