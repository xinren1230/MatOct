// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static ArrayValue A(Value v, string fn) => v as ArrayValue ?? throw new OctaveException($"{fn}: wrong type argument '{v.ClassName}'");

    static void RegisterArray()
    {
        Reg("size", (it, a, n) =>
        {
            NArgs(a, 1, 2, "size");
            var v = a[0];
            if (a.Length == 2) { int d = I(a[1], "size"); return Out(Num(d == 1 ? v.Rows : d == 2 ? v.Cols : 1)); }
            if (n <= 1) return Out(MatrixValue.Row(new double[] { v.Rows, v.Cols }));
            var res = new Value[n];
            res[0] = Num(v.Rows); res[1] = Num(v.Cols);
            for (int k = 2; k < n; k++) res[k] = Num(1);
            return res;
        });
        Reg1("numel", a => Num(a[0].Numel));
        Reg1("length", a => Num(a[0].Numel == 0 ? 0 : Math.Max(a[0].Rows, a[0].Cols)));
        Reg1("ndims", a => Num(2));
        Reg1("rows", a => Num(a[0].Rows));
        Reg1("columns", a => Num(a[0].Cols));
        Reg1("isempty", a => { NArgs(a, 1, 1, "isempty"); return Bool(a[0].Numel == 0); });
        Reg1("isnull", a => Bool(a[0].Numel == 0));
        Reg1("isscalar", a => Bool(a[0].Rows == 1 && a[0].Cols == 1));
        Reg1("isvector", a => Bool((a[0].Rows == 1 || a[0].Cols == 1) && a[0].Numel >= 1));
        Reg1("isrow", a => Bool(a[0].Rows == 1));
        Reg1("iscolumn", a => Bool(a[0].Cols == 1));
        Reg1("ismatrix", a => Bool(true));
        Reg1("issquare", a => Bool(a[0].Rows == a[0].Cols));

        Reg1("zeros", a => { var (r, c) = Dims(a, "zeros"); return Fill(r, c, 0); });
        Reg1("ones", a => { var (r, c) = Dims(a, "ones"); return Fill(r, c, 1); });
        Reg1("cell", a => { var (r, c) = Dims(a, "cell"); return new CellValue(r, c); });
        Reg1("eye", a => { var (r, c) = Dims(a, "eye"); var m = new MatrixValue(r, c); for (int k = 0; k < Math.Min(r, c); k++) m.Re[k + k * r] = 1; return m; });
        RegI("rand", (it, a) =>
        {
            if (a.Length >= 1 && IsString(a[0])) { if (a.Length > 1) it.Rng = new Random((int)D(a[1], "rand")); return Num(0); }
            var (r, c) = Dims(a, "rand"); var m = new MatrixValue(r, c); for (int k = 0; k < m.Numel; k++) m.Re[k] = it.Rng.NextDouble(); return m;
        });
        RegI("randn", (it, a) =>
        {
            if (a.Length >= 1 && IsString(a[0])) { if (a.Length > 1) it.Rng = new Random((int)D(a[1], "randn")); return Num(0); }
            var (r, c) = Dims(a, "randn"); var m = new MatrixValue(r, c);
            for (int k = 0; k < m.Numel; k++) { double u1 = 1 - it.Rng.NextDouble(), u2 = it.Rng.NextDouble(); m.Re[k] = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2); }
            return m;
        });
        RegI("randi", (it, a) =>
        {
            var lim = M(a[0], "randi"); double lo = lim.Numel > 1 ? lim.Re[0] : 1, hi = lim.Numel > 1 ? lim.Re[1] : lim.Re[0];
            var (r, c) = Dims(a, "randi", 1); var m = new MatrixValue(r, c);
            for (int k = 0; k < m.Numel; k++) m.Re[k] = Math.Floor(lo + it.Rng.NextDouble() * (hi - lo + 1));
            return m;
        });
        RegI("randperm", (it, a) => { int n = I(a[0], "randperm"); var p = Enumerable.Range(1, n).Select(x => (double)x).ToArray(); it.Rng.Shuffle(p); return MatrixValue.Row(p); });
        Reg1("linspace", a =>
        {
            double lo = D(a[0], "linspace"), hi = D(a[1], "linspace"); int n = a.Length > 2 ? I(a[2], "linspace") : 100;
            if (n < 1) n = 1;
            var m = new MatrixValue(1, n);
            for (int k = 0; k < n; k++) m.Re[k] = n == 1 ? hi : lo + (hi - lo) * k / (n - 1);
            return m;
        });
        Reg1("logspace", a =>
        {
            double lo = D(a[0], "logspace"), hi = D(a[1], "logspace"); int n = a.Length > 2 ? I(a[2], "logspace") : 50;
            return MatrixValue.Row(Enumerable.Range(0, n).Select(k => Math.Pow(10, n == 1 ? hi : lo + (hi - lo) * k / (n - 1))));
        });
        Reg1("colon", a =>
        {
            double lo = D(a[0], "colon"), st = a.Length > 2 ? D(a[1], "colon") : 1, hi = D(a[^1], "colon");
            long n = Interpreter.RangeCount(lo, st, hi);
            var m = new MatrixValue(1, (int)n); for (int k = 0; k < n; k++) m.Re[k] = lo + k * st; return m;
        });
        Reg1("repmat", a =>
        {
            var src = A(a[0], "repmat");
            int rm, cm;
            if (a.Length == 2) { var d = M(a[1], "repmat"); rm = (int)d.Re[0]; cm = d.Numel > 1 ? (int)d.Re[1] : rm; }
            else { rm = I(a[1], "repmat"); cm = I(a[2], "repmat"); }
            rm = Math.Max(rm, 0); cm = Math.Max(cm, 0);
            int R = src.R * rm, C = src.C * cm;
            var lin = new int[R * C];
            for (int j = 0; j < C; j++) for (int i = 0; i < R; i++) lin[i + j * R] = (i % src.R) + (j % src.C) * src.R;
            return src.Gather(lin, R, C);
        });
        Reg1("reshape", a =>
        {
            var src = A(a[0], "reshape");
            int r, c;
            if (a.Length == 2) { var d = M(a[1], "reshape"); r = (int)d.Re[0]; c = (int)d.Re[1]; for (int k = 2; k < d.Numel; k++) c *= (int)d.Re[k]; }
            else
            {
                var rv = M(a[1], "reshape"); var cv = M(a[2], "reshape");
                for (int k = 3; k < a.Length; k++) if (D(a[k], "reshape") != 1) throw new OctaveException("reshape: N-d arrays are not supported yet");
                r = rv.Numel == 0 ? -1 : (int)rv.Re[0]; c = cv.Numel == 0 ? -1 : (int)cv.Re[0];
                if (r < 0) r = c == 0 ? 0 : src.Numel / c;
                if (c < 0) c = r == 0 ? 0 : src.Numel / r;
            }
            if (r * c != src.Numel) throw new OctaveException($"reshape: can't reshape {src.R}x{src.C} array to {r}x{c} array");
            return src.Gather(Enumerable.Range(0, src.Numel).ToArray(), r, c);
        });
        Reg1("diag", a =>
        {
            var m = M(a[0], "diag"); int k = a.Length > 1 ? I(a[1], "diag") : 0;
            if (m.R == 1 || m.C == 1)
            {
                int n = m.Numel + Math.Abs(k);
                var r = new MatrixValue(n, n, NumKind.Double, m.IsComplex);
                for (int t = 0; t < m.Numel; t++)
                {
                    int i = k >= 0 ? t : t - k, j = k >= 0 ? t + k : t;
                    r.Re[i + j * n] = m.Re[t]; if (m.Im != null) r.Im![i + j * n] = m.Im[t];
                }
                return r;
            }
            var lin = new List<int>();
            for (int t = 0; ; t++)
            {
                int i = k >= 0 ? t : t - k, j = k >= 0 ? t + k : t;
                if (i >= m.R || j >= m.C) break;
                lin.Add(i + j * m.R);
            }
            return m.Gather(lin.ToArray(), lin.Count, 1);
        });
        Reg1("horzcat", a => Concat.Horz(a.ToList()));
        Reg1("vertcat", a => Concat.Vert(a.ToList()));
        Reg1("cat", a => { int d = I(a[0], "cat"); var rest = a.Skip(1).ToList(); return d == 1 ? Concat.Vert(rest) : d == 2 ? Concat.Horz(rest) : throw new OctaveException("cat: N-d arrays are not supported yet"); });

        // reductions
        Reg1("sum", a =>
        {
            var m = M(a[0], "sum"); int d = DimArg(a, 1, m, "sum");
            if (m.IsComplex) return ReduceC(m, d, v => v.Aggregate(Complex.Zero, (x, y) => x + y));
            return Reduce(m, d, v => { double s = 0; foreach (var x in v) s += x; return s; });
        });
        Reg1("prod", a =>
        {
            var m = M(a[0], "prod"); int d = DimArg(a, 1, m, "prod");
            if (m.IsComplex) return ReduceC(m, d, v => v.Aggregate(Complex.One, (x, y) => x * y));
            return Reduce(m, d, v => { double s = 1; foreach (var x in v) s *= x; return s; });
        });
        Reg1("sumsq", a => { var m = M(a[0], "sumsq"); return Reduce(m, DimArg(a, 1, m, "sumsq"), v => v.Sum(x => x * x)); });
        Reg1("mean", a =>
        {
            var m = M(a[0], "mean"); int d = DimArg(a, 1, m, "mean");
            if (m.R == 0 && m.C == 0) return Num(double.NaN);
            if (m.IsComplex) return ReduceC(m, d, v => v.Aggregate(Complex.Zero, (x, y) => x + y) / v.Length);
            return Reduce(m, d, v => v.Length == 0 ? double.NaN : v.Sum() / v.Length);
        });
        Reg1("median", a =>
        {
            var m = M(a[0], "median"); int d = DimArg(a, 1, m, "median");
            if (m.R == 0 && m.C == 0) return Num(double.NaN);
            return Reduce(m, d, v =>
            {
                if (v.Length == 0 || v.Any(double.IsNaN)) return double.NaN;
                var s = v.OrderBy(x => x).ToArray(); int n = s.Length;
                return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
            });
        });
        Reg1("mode", a =>
        {
            var m = M(a[0], "mode"); int d = DimArg(a, 1, m, "mode");
            return Reduce(m, d, v => v.Length == 0 ? double.NaN : v.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key);
        });
        Func<double[], int, double> variance = (v, opt) =>
        {
            int n = v.Length; if (n == 0) return double.NaN; if (n == 1) return 0;
            double mu = v.Average(); double s = v.Sum(x => (x - mu) * (x - mu));
            return s / (opt == 1 ? n : n - 1);
        };
        Reg1("var", a => { var m = M(a[0], "var"); int opt = a.Length > 1 && a[1].Numel > 0 ? I(a[1], "var") : 0; return Reduce(m, DimArg(a, 2, m, "var"), v => variance(v, opt)); });
        Reg1("std", a => { var m = M(a[0], "std"); int opt = a.Length > 1 && a[1].Numel > 0 ? I(a[1], "std") : 0; return Reduce(m, DimArg(a, 2, m, "std"), v => Math.Sqrt(variance(v, opt))); });
        Reg1("any", a =>
        {
            var m = M(a[0], "any"); int d = a.Length > 1 ? I(a[1], "any") : (m.R == 1 || m.C == 1) && a.Length == 1 ? (m.R == 1 ? 2 : 1) : 1;
            if (m.Numel == 0 && a.Length == 1) return Bool(false);
            return Reduce(m, d, v => v.Any(x => x != 0 && !double.IsNaN(x)) ? 1 : 0, NumKind.Logical);
        });
        Reg1("all", a =>
        {
            var m = M(a[0], "all"); int d = a.Length > 1 ? I(a[1], "all") : (m.R == 1 || m.C == 1) && a.Length == 1 ? (m.R == 1 ? 2 : 1) : 1;
            if (m.Numel == 0 && a.Length == 1) return Bool(true);
            return Reduce(m, d, v => v.All(x => x != 0) ? 1 : 0, NumKind.Logical);
        });
        Reg1("cumsum", a => { var m = M(a[0], "cumsum"); return AlongDim(m, DimArg(a, 1, m, "cumsum"), v => { var o = new double[v.Length]; double s = 0; for (int k = 0; k < v.Length; k++) o[k] = s += v[k]; return o; }); });
        Reg1("cumprod", a => { var m = M(a[0], "cumprod"); return AlongDim(m, DimArg(a, 1, m, "cumprod"), v => { var o = new double[v.Length]; double s = 1; for (int k = 0; k < v.Length; k++) o[k] = s *= v[k]; return o; }); });
        Reg("max", (it, a, n) => MinMax(a, n, "max", (x, y) => x > y));
        Reg("min", (it, a, n) => MinMax(a, n, "min", (x, y) => x < y));
        Reg("sort", (it, a, n) =>
        {
            var src = a[0];
            bool desc = a.Skip(1).Any(v => IsString(v) && S(v, "sort").Equals("descend", StringComparison.OrdinalIgnoreCase));
            if (src is CellValue cv)
            {
                var strs = cv.Data.Select((v, k) => (s: S(v, "sort"), k)).ToList();
                var ord = desc ? strs.OrderByDescending(p => p.s, StringComparer.Ordinal).ToList() : strs.OrderBy(p => p.s, StringComparer.Ordinal).ToList();
                var lin = ord.Select(p => p.k).ToArray();
                return Out(cv.Gather(lin, cv.R, cv.C), new MatrixValue(cv.R, cv.C, lin.Select(k => k + 1.0).ToArray()));
            }
            var m = M(src, "sort");
            var numArg = a.Skip(1).FirstOrDefault(v => !IsString(v));
            int d = numArg != null ? I(numArg, "sort") : DefDim(m);
            var idx = new MatrixValue(m.R, m.C);
            var res = m.Copy();
            int len = d == 1 ? m.R : m.C, cnt = d == 1 ? m.C : m.R;
            for (int t = 0; t < cnt; t++)
            {
                var pos = Enumerable.Range(0, len).Select(k => d == 1 ? k + t * m.R : t + k * m.R).ToArray();
                Func<int, double> key = p => m.IsComplex ? m.At(p).Magnitude : m.Re[p];
                var order = Enumerable.Range(0, len).ToArray();
                // stable sort, NaN last (first for descend)
                Comparison<int> cmp = (x, y) =>
                {
                    double kx = key(pos[x]), ky = key(pos[y]);
                    int c;
                    if (double.IsNaN(kx) || double.IsNaN(ky)) c = double.IsNaN(kx) && double.IsNaN(ky) ? 0 : double.IsNaN(kx) ? 1 : -1;
                    else c = kx.CompareTo(ky);
                    if (desc) c = -c;
                    return c != 0 ? c : x.CompareTo(y);
                };
                Array.Sort(order, cmp);
                for (int k = 0; k < len; k++)
                {
                    res.Re[pos[k]] = m.Re[pos[order[k]]];
                    if (m.Im != null) res.Im![pos[k]] = m.Im[pos[order[k]]];
                    idx.Re[pos[k]] = order[k] + 1;
                }
            }
            return Out(res, idx);
        });
        Reg("find", (it, a, n) =>
        {
            var m = M(a[0], "find"); int limit = a.Length > 1 ? I(a[1], "find") : int.MaxValue;
            bool last = a.Length > 2 && S(a[2], "find") == "last";
            var hits = new List<int>();
            for (int k = 0; k < m.Numel; k++) if (m.Re[k] != 0 || (m.Im != null && m.Im[k] != 0)) hits.Add(k);
            if (hits.Count > limit) hits = last ? hits.Skip(hits.Count - limit).ToList() : hits.Take(limit).ToList();
            bool row = m.R == 1 && m.C != 1;
            MatrixValue Mk(IEnumerable<double> xs) { var arr = xs.ToArray(); return row ? new MatrixValue(1, arr.Length, arr) : new MatrixValue(arr.Length, m.Numel == 0 && m.R == 0 && m.C == 0 ? 0 : 1, arr); }
            if (n <= 1) return Out(Mk(hits.Select(k => k + 1.0)));
            var r = Mk(hits.Select(k => (double)(k % m.R + 1)));
            var c = Mk(hits.Select(k => (double)(k / m.R + 1)));
            if (n == 2) return Out(r, c);
            return Out(r, c, m.Gather(hits.ToArray(), row ? 1 : hits.Count, row ? hits.Count : 1));
        });
        Reg("unique", (it, a, n) =>
        {
            if (a[0] is CellValue cv)
            {
                var strs = cv.Data.Select(v => S(v, "unique")).ToArray();
                var u = strs.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();
                var iIdx = u.Select(s => Array.LastIndexOf(strs, s) + 1.0);
                var jIdx = strs.Select(s => Array.IndexOf(u, s) + 1.0);
                var res = cv.R == 1 && cv.Numel > 0 ? CellValue.RowOf(u.Select(Str)) : CellValue.ColOf(u.Select(Str));
                return Out(res, MatrixValue.Col(iIdx), MatrixValue.Col(jIdx));
            }
            var m = M(a[0], "unique");
            var vals = m.Re.Distinct().OrderBy(x => x).ToArray();
            bool row = m.R == 1 && m.C >= 1;
            var outv = row ? new MatrixValue(1, vals.Length, vals, null, m.Kind) : new MatrixValue(vals.Length, m.Numel == 0 ? 0 : 1, vals, null, m.Kind);
            var ii = vals.Select(v => Array.LastIndexOf(m.Re, v) + 1.0);
            var jj = m.Re.Select(v => Array.IndexOf(vals, v) + 1.0);
            return Out(outv, row ? MatrixValue.Row(ii) : MatrixValue.Col(ii), row ? MatrixValue.Row(jj) : MatrixValue.Col(jj));
        });
        Reg1("fliplr", a => Flip(A(a[0], "fliplr"), 2));
        Reg1("flipud", a => Flip(A(a[0], "flipud"), 1));
        Reg1("flip", a => { var x = A(a[0], "flip"); return Flip(x, a.Length > 1 ? I(a[1], "flip") : DefDim(x)); });
        Reg1("rot90", a =>
        {
            var x = A(a[0], "rot90"); int k = a.Length > 1 ? ((I(a[1], "rot90") % 4) + 4) % 4 : 1;
            for (int t = 0; t < k; t++) x = Flip(Ops.Transpose(x, false), 1);
            return x;
        });
        Reg1("circshift", a =>
        {
            var x = A(a[0], "circshift"); var sh = M(a[1], "circshift");
            int dim = a.Length > 2 ? I(a[2], "circshift") : (sh.Numel > 1 ? 1 : DefDim(x));
            int sr = 0, sc = 0;
            if (sh.Numel > 1) { sr = (int)sh.Re[0]; sc = (int)sh.Re[1]; } else if (dim == 1) sr = (int)sh.Re[0]; else sc = (int)sh.Re[0];
            var lin = new int[x.Numel];
            for (int j = 0; j < x.C; j++) for (int i = 0; i < x.R; i++)
                {
                    int si = ((i - sr) % x.R + x.R) % x.R, sj = ((j - sc) % x.C + x.C) % x.C;
                    lin[i + j * x.R] = si + sj * x.R;
                }
            return x.Gather(lin, x.R, x.C);
        });
        Reg1("nnz", a => { var m = M(a[0], "nnz"); return Num(m.Re.Count(x => x != 0)); });
        Reg1("triu", a => Tri(M(a[0], "triu"), a.Length > 1 ? I(a[1], "triu") : 0, true));
        Reg1("tril", a => Tri(M(a[0], "tril"), a.Length > 1 ? I(a[1], "tril") : 0, false));
        Reg1("diff", a =>
        {
            var m = M(a[0], "diff"); int order = a.Length > 1 ? I(a[1], "diff") : 1;
            for (int t = 0; t < order; t++)
            {
                int d = DefDim(m);
                if (d == 1) { if (m.R == 0) break; var r = new MatrixValue(m.R - 1, m.C); for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R - 1; i++) r.Re[i + j * r.R] = m.Re[i + 1 + j * m.R] - m.Re[i + j * m.R]; m = r; }
                else { if (m.C == 0) break; var r = new MatrixValue(m.R, m.C - 1); for (int j = 0; j < m.C - 1; j++) for (int i = 0; i < m.R; i++) r.Re[i + j * m.R] = m.Re[i + (j + 1) * m.R] - m.Re[i + j * m.R]; m = r; }
            }
            return m;
        });
        Reg1("isequal", a => { for (int k = 1; k < a.Length; k++) if (!ValuesEqual(a[0], a[k])) return Bool(false); return Bool(true); });
        Reg("ismember", (it, a, n) =>
        {
            if (a[1] is CellValue set)
            {
                var strs = set.Data.Select(v => v is MatrixValue mm ? mm.AsString() : null).ToList();
                if (a[0] is CellValue q)
                {
                    var r = new MatrixValue(q.R, q.C, NumKind.Logical); var ix = new MatrixValue(q.R, q.C);
                    for (int k = 0; k < q.Numel; k++) { int p = strs.IndexOf(S(q.Data[k], "ismember")); r.Re[k] = p >= 0 ? 1 : 0; ix.Re[k] = p + 1; }
                    return Out(r, ix);
                }
                int pos = strs.IndexOf(S(a[0], "ismember"));
                return Out(Bool(pos >= 0), Num(pos + 1));
            }
            var x = M(a[0], "ismember"); var s = M(a[1], "ismember");
            var res = new MatrixValue(x.R, x.C, NumKind.Logical); var idx = new MatrixValue(x.R, x.C);
            for (int k = 0; k < x.Numel; k++) { int p = Array.IndexOf(s.Re, x.Re[k]); res.Re[k] = p >= 0 ? 1 : 0; idx.Re[k] = p + 1; }
            return Out(res, idx);
        });
        Reg1("magic", a => Magic(I(a[0], "magic")));
        Reg("meshgrid", (it, a, n) =>
        {
            var x = M(a[0], "meshgrid"); var y = a.Length > 1 ? M(a[1], "meshgrid") : x;
            int r = y.Numel, c = x.Numel;
            var X = new MatrixValue(r, c); var Y = new MatrixValue(r, c);
            for (int j = 0; j < c; j++) for (int i = 0; i < r; i++) { X.Re[i + j * r] = x.Re[j]; Y.Re[i + j * r] = y.Re[i]; }
            return Out(X, Y);
        });
        Reg1("trapz", a =>
        {
            if (a.Length == 1) { var y = M(a[0], "trapz"); double s = 0; for (int k = 1; k < y.Numel; k++) s += (y.Re[k] + y.Re[k - 1]) / 2; return Num(s); }
            var xv = M(a[0], "trapz"); var yv = M(a[1], "trapz"); double t = 0;
            for (int k = 1; k < yv.Numel; k++) t += (xv.Re[k] - xv.Re[k - 1]) * (yv.Re[k] + yv.Re[k - 1]) / 2;
            return Num(t);
        });
        Reg1("histc", a =>
        {
            var x = M(a[0], "histc"); var ed = M(a[1], "histc"); var r = new MatrixValue(ed.R, ed.C);
            foreach (var v in x.Re) { for (int k = ed.Numel - 1; k >= 0; k--) if (k == ed.Numel - 1 ? v == ed.Re[k] : v >= ed.Re[k] && v < ed.Re[k + 1]) { r.Re[k]++; break; } }
            return r;
        });
        Reg1("accumarray", a =>
        {
            var subs = M(a[0], "accumarray"); var vals = M(a[1], "accumarray");
            int n = (int)subs.Re.Max(); var r = new MatrixValue(n, 1);
            for (int k = 0; k < subs.Numel; k++) r.Re[(int)subs.Re[k] - 1] += vals.Numel == 1 ? vals.Re[0] : vals.Re[k];
            return r;
        });

        // types
        Reg1("double", a => { var m = M(a[0], "double"); return new MatrixValue(m.R, m.C, (double[])m.Re.Clone(), (double[]?)m.Im?.Clone()); });
        Reg1("single", a => { var m = M(a[0], "single"); return new MatrixValue(m.R, m.C, (double[])m.Re.Clone(), (double[]?)m.Im?.Clone()); });
        foreach (var iname in new[] { "int8", "uint8", "int16", "uint16", "int32", "uint32", "int64", "uint64" })
        {
            string nm = iname;
            (double lo, double hi) range = nm switch
            {
                "int8" => (-128, 127), "uint8" => (0, 255), "int16" => (-32768, 32767), "uint16" => (0, 65535),
                "int32" => (int.MinValue, int.MaxValue), "uint32" => (0, uint.MaxValue), "int64" => (long.MinValue, long.MaxValue), _ => (0, ulong.MaxValue)
            };
            // integer types are emulated as rounded/saturated doubles (phase 2: real integer classes)
            Reg1(nm, a => Ops.Map(M(a[0], nm).AsDouble(), x => double.IsNaN(x) ? 0 : Math.Clamp(Math.Round(x, MidpointRounding.AwayFromZero), range.lo, range.hi)));
        }
        Reg1("logical", a =>
        {
            var m = M(a[0], "logical");
            if (m.Re.Any(double.IsNaN)) throw new OctaveException("logical: NaN can't be converted to logical value");
            var r = new MatrixValue(m.R, m.C, NumKind.Logical); for (int k = 0; k < m.Numel; k++) r.Re[k] = m.Re[k] != 0 ? 1 : 0; return r;
        });
        Reg1("class", a => Str(a[0].ClassName));
        Reg1("isa", a =>
        {
            string c = S(a[1], "isa"), k = a[0].ClassName;
            return Bool(c == k || (c == "numeric" && (k == "double")) || (c == "float" && k == "double"));
        });
        Reg1("isnumeric", a => Bool(a[0] is MatrixValue m && m.Kind == NumKind.Double));
        Reg1("isreal", a => Bool(a[0] is MatrixValue m && !m.IsComplex));
        Reg1("iscomplex", a => Bool(a[0] is MatrixValue m && m.IsComplex));
        Reg1("isfloat", a => Bool(a[0] is MatrixValue m && m.Kind == NumKind.Double));
        Reg1("isinteger", a => Bool(false));
        Reg1("ischar", a => Bool(a[0] is MatrixValue m && m.IsChar));
        Reg1("islogical", a => Bool(a[0] is MatrixValue m && m.IsLogical));
        Reg1("isbool", a => Bool(a[0] is MatrixValue m && m.IsLogical));
        Reg1("iscell", a => Bool(a[0] is CellValue));
        Reg1("isstruct", a => Bool(a[0] is StructValue));
        Reg1("is_function_handle", a => Bool(a[0] is FunctionHandleValue));
        Reg1("isdigit", a => CharPred(a[0], char.IsDigit));
        Reg1("isspace", a => CharPred(a[0], char.IsWhiteSpace));
        Reg1("isletter", a => CharPred(a[0], char.IsLetter));
        Reg1("isalpha", a => CharPred(a[0], char.IsLetter));
        Reg1("isupper", a => CharPred(a[0], char.IsUpper));
        Reg1("islower", a => CharPred(a[0], char.IsLower));
    }

    static Value CharPred(Value v, Func<char, bool> f)
    {
        var m = M(v, "ischar");
        var r = new MatrixValue(m.R, m.C, NumKind.Logical);
        if (m.IsChar) for (int k = 0; k < m.Numel; k++) r.Re[k] = f((char)(int)m.Re[k]) ? 1 : 0;
        return r;
    }

    static Value[] MinMax(Value[] a, int nargout, string fn, Func<double, double, bool> better)
    {
        if (a.Length >= 2 && a[1].Numel > 0)
        {
            var x = M(a[0], fn); var y = M(a[1], fn);
            return Out(Ops.Elementwise(x, y, fn, (p, q) => double.IsNaN(p) ? q : double.IsNaN(q) ? p : better(q, p) ? q : p, null));
        }
        var m = M(a[0], fn);
        int d = DimArg(a, 2, m, fn);
        if (m.Numel == 0) return Out(new MatrixValue(0, 0), new MatrixValue(0, 0));
        Func<int, double> key = k => m.IsComplex ? m.At(k).Magnitude : m.Re[k];
        int len = d == 1 ? m.R : m.C, cnt = d == 1 ? m.C : m.R;
        var val = d == 1 ? new MatrixValue(1, cnt, m.Kind == NumKind.Char ? NumKind.Double : m.Kind, m.IsComplex) : new MatrixValue(cnt, 1, m.Kind == NumKind.Char ? NumKind.Double : m.Kind, m.IsComplex);
        var idx = new MatrixValue(val.R, val.C);
        for (int t = 0; t < cnt; t++)
        {
            int best = -1;
            for (int k = 0; k < len; k++)
            {
                int p = d == 1 ? k + t * m.R : t + k * m.R;
                if (double.IsNaN(key(p))) continue;
                if (best < 0 || better(key(p), key(best))) best = p;
            }
            if (best < 0) { best = d == 1 ? t * m.R : t; }
            val.Re[t] = m.Re[best]; if (m.Im != null) val.Im![t] = m.Im[best];
            idx.Re[t] = (d == 1 ? best - t * m.R : (best - t) / m.R) + 1;
        }
        return Out(val.Narrow(), idx);
    }

    static ArrayValue Flip(ArrayValue x, int dim)
    {
        var lin = new int[x.Numel];
        for (int j = 0; j < x.C; j++) for (int i = 0; i < x.R; i++)
                lin[i + j * x.R] = dim == 1 ? (x.R - 1 - i) + j * x.R : i + (x.C - 1 - j) * x.R;
        return x.Gather(lin, x.R, x.C);
    }

    static MatrixValue Tri(MatrixValue m, int k, bool upper)
    {
        var r = m.Copy();
        for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++)
            {
                bool keep = upper ? j - i >= k : j - i <= k;
                if (!keep) { r.Re[i + j * m.R] = 0; if (r.Im != null) r.Im[i + j * m.R] = 0; }
            }
        return r;
    }

    public static bool ValuesEqual(Value a, Value b)
    {
        if (a.Rows != b.Rows || a.Cols != b.Cols) return false;
        switch (a)
        {
            case MatrixValue ma when b is MatrixValue mb:
                for (int k = 0; k < ma.Numel; k++)
                    if (ma.Re[k] != mb.Re[k] || (ma.Im?[k] ?? 0) != (mb.Im?[k] ?? 0)) return false;
                return true;
            case CellValue ca when b is CellValue cb:
                for (int k = 0; k < ca.Numel; k++) if (!ValuesEqual(ca.Data[k], cb.Data[k])) return false;
                return true;
            case StructValue sa when b is StructValue sb:
                if (sa.Fields.Count != sb.Fields.Count || sa.Fields.Any(f => !sb.HasField(f))) return false;
                for (int k = 0; k < sa.Numel; k++) foreach (var f in sa.Fields) if (!ValuesEqual(sa.Elems[k][f], sb.Elems[k][f])) return false;
                return true;
            case FunctionHandleValue fa when b is FunctionHandleValue fb:
                return fa.ToString() == fb.ToString();
        }
        return false;
    }

    static MatrixValue Magic(int n)
    {
        var m = new MatrixValue(n, n);
        if (n < 1) return new MatrixValue(0, 0);
        if (n == 1) { m.Re[0] = 1; return m; }
        if (n == 2) return new MatrixValue(2, 2, new double[] { 4, 3, 1, 2 });
        void Set(int i, int j, double v) => m.Re[i + j * n] = v;
        double Get(int i, int j) => m.Re[i + j * n];
        if (n % 2 == 1)
        {
            // Octave: shift = floor ((0:n*n-1)/n); c = mod ([1:n*n] - shift + (n-3)/2, n); r = mod ([n*n:-1:1] + 2*shift, n); A(c*n+r+1) = 1:n*n;
            for (int k = 0; k < n * n; k++)
            {
                int shift = k / n;
                int c = (((k + 1) - shift + (n - 3) / 2) % n + n) % n;
                int r = (((n * n - k) + 2 * shift) % n + n) % n;
                m.Re[c * n + r] = k + 1;
            }
        }
        else if (n % 4 == 0)
        {
            for (int k = 0; k < n * n; k++) m.Re[k] = k + 1; // column-major 1:n*n then reshape(n,n)'
            var t = new double[n * n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) t[i + j * n] = m.Re[j + i * n];
            m.Re = t;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    bool fi = (i % 4 == 0 || i % 4 == 3), fj = (j % 4 == 0 || j % 4 == 3);
                    if (fi == fj) Set(i, j, n * n + 1 - Get(i, j));
                }
        }
        else
        {
            int mm = n / 2;
            var A = Magic(mm);
            for (int i = 0; i < mm; i++) for (int j = 0; j < mm; j++)
                {
                    double v = A.Re[i + j * mm];
                    Set(i, j, v); Set(i + mm, j + mm, v + mm * mm); Set(i, j + mm, v + 2 * mm * mm); Set(i + mm, j, v + 3 * mm * mm);
                }
            int k = (mm - 1) / 2;
            var ndx = Enumerable.Range(0, k).Concat(Enumerable.Range(n - k + 1, k - 1)).ToList();
            for (int i = 0; i < mm; i++) foreach (int j in ndx)
                {
                    double t = Get(i, j); Set(i, j, Get(i + mm, j)); Set(i + mm, j, t);
                }
            foreach (int j in new[] { k, 0 })
            {
                int i = k;
                double t = Get(i, j); Set(i, j, Get(i + mm, j)); Set(i + mm, j, t);
            }
        }
        return m;
    }
}
