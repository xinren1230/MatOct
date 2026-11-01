// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    /// <summary>Reduce x over the given (1-based) dims; f receives the values of each group in column-major order.</summary>
    internal static MatrixValue GroupReduce(MatrixValue x, int[] dims, Func<double[], double> f, NumKind kind = NumKind.Double, double[]? im = null, Func<double[], double>? fim = null)
    {
        var xs = x.Dims; int nd = Math.Max(xs.Length, dims.Length == 0 ? 2 : dims.Max());
        var sz = new int[nd]; for (int k = 0; k < nd; k++) sz[k] = k < xs.Length ? xs[k] : 1;
        var osz = sz.ToArray(); foreach (var d in dims) osz[d - 1] = 1;
        int on = osz.Aggregate(1, (p, q) => p * q);
        var buckets = new List<double>[on]; var bim = im != null ? new List<double>[on] : null;
        for (int k = 0; k < on; k++) { buckets[k] = new List<double>(); if (bim != null) bim[k] = new List<double>(); }
        var sub = new int[nd]; int n = x.Numel;
        var ostr = new int[nd]; ostr[0] = 1; for (int k = 1; k < nd; k++) ostr[k] = ostr[k - 1] * osz[k - 1];
        for (int li = 0; li < n; li++)
        {
            int o = 0; for (int k = 0; k < nd; k++) if (osz[k] != 1 || sz[k] == 1) o += sub[k] * ostr[k];
            buckets[o].Add(x.Re[li]); bim?[o].Add(im![li]);
            for (int k = 0; k < nd; k++) { if (++sub[k] < sz[k]) break; sub[k] = 0; }
        }
        var re = new double[on]; double[]? ri = fim != null ? new double[on] : null;
        for (int k = 0; k < on; k++) { re[k] = f(buckets[k].ToArray()); if (ri != null) ri[k] = fim!(bim![k].ToArray()); }
        var r = new MatrixValue(osz[0], on / Math.Max(1, osz[0]) , re, ri, kind);
        if (osz[0] == 0) r.C = 0;
        r.SetDims(osz);
        return r;
    }

    static int DefaultDim(int[] sz) { for (int k = 0; k < sz.Length; k++) if (sz[k] != 1) return k + 1; return 1; }

    /// <summary>Parse a DIM / VECDIM / "all" argument. Returns null when it is not a dim argument.</summary>
    static int[] ParseDims(Value v, MatrixValue x, string fn, bool allowVec = true)
    {
        if (IsString(v) && Sx(v).ToLowerInvariant() == "all") return Enumerable.Range(1, Math.Max(2, x.Dims.Length)).ToArray();
        if (v is not MatrixValue dm || dm.IsChar || dm.Numel == 0 || dm.Re.Any(d => !(d >= 1) || d != Math.Floor(d) || double.IsInfinity(d)) || (dm.Numel > 1 && !(dm.R == 1 || dm.C == 1)))
            throw new OctaveException($"{fn}: DIM must be a positive integer scalar or vector");
        if (dm.Numel > 1)
        {
            if (!allowVec) throw new OctaveException($"{fn}: DIM must be a positive integer scalar");
            if (dm.Re.Distinct().Count() != dm.Numel) throw new OctaveException($"{fn}: VECDIM must contain non-repeating positive integers");
        }
        return dm.Re.Select(d => (int)d).ToArray();
    }

    static bool Is00(MatrixValue x) => x.ND == null && x.R == 0 && x.C == 0;

    static double MeanOf(double[] v, bool omit)
    {
        double s = 0; int c = 0;
        foreach (var d in v) { if (omit && double.IsNaN(d)) continue; s += d; c++; }
        return c == 0 ? double.NaN : s / c;
    }
    static double MedianOf(double[] v, bool omit)
    {
        var w = omit ? v.Where(d => !double.IsNaN(d)).ToArray() : v.ToArray();
        if (w.Length == 0 || w.Any(double.IsNaN)) return double.NaN;
        Array.Sort(w); int n = w.Length;
        if (n % 2 == 1) return w[n / 2];
        double a = w[n / 2 - 1], b = w[n / 2];
        if (double.IsInfinity(a) && double.IsInfinity(b)) return a == b ? a : double.NaN;
        return a + (b - a) / 2;
    }
    static double VarOf(double[] v, double[]? im, double w, bool omit)
    {
        var idx = Enumerable.Range(0, v.Length).Where(k => !(omit && (double.IsNaN(v[k]) || im != null && double.IsNaN(im[k])))).ToArray();
        int n = idx.Length;
        if (n == 0) return double.NaN;
        if (n == 1) return double.IsFinite(v[idx[0]]) && (im == null || double.IsFinite(im[idx[0]])) ? 0 : double.NaN;
        double mr = idx.Sum(k => v[k]) / n, mi = im != null ? idx.Sum(k => im[k]) / n : 0;
        double s = 0; foreach (var k in idx) { double dr = v[k] - mr, di = im != null ? im[k] - mi : 0; s += dr * dr + di * di; }
        return s / (w == 1 ? n : n - 1);
    }

    sealed class RedArgs { public int[] Dims = Array.Empty<int>(); public bool Omit; public string OutType = "default"; public bool HasDim; }

    static RedArgs ParseRed(Value[] rest, MatrixValue x, string fn)
    {
        var r = new RedArgs(); bool haveOut = false, haveNan = false;
        foreach (var v in rest)
        {
            if (IsString(v))
            {
                var s = Sx(v).ToLowerInvariant();
                if (s == "omitnan" || s == "includenan") { if (haveNan) Usage(fn); haveNan = true; r.Omit = s == "omitnan"; continue; }
                if (s == "default" || s == "double" || s == "native") { if (haveOut) throw new OctaveException($"{fn}: only one OUTTYPE can be specified"); haveOut = true; r.OutType = s; continue; }
                if (s == "all") { if (r.HasDim) throw new OctaveException($"{fn}: 'all' cannot be used with DIM or VECDIM options"); r.HasDim = true; r.Dims = ParseDims(v, x, fn); continue; }
                Usage(fn);
            }
            if (r.HasDim) Usage(fn);
            if (v is MatrixValue dm0 && dm0.Numel == 1 == false && dm0.Numel != 0 && !(dm0.R == 1 || dm0.C == 1)) throw new OctaveException($"{fn}: DIM must be a positive integer scalar or vector");
            r.Dims = ParseDims(v, x, fn); r.HasDim = true;
        }
        if (!r.HasDim) r.Dims = new[] { DefaultDim(x.Dims) };
        return r;
    }

    static void RegisterPhase7Stats()
    {
        Reg("mean", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("mean");
            if (!(IsNumL(a[0]))) throw new OctaveException("mean: X must be a numeric or logical array");
            var x = a[0] is Sparse.SparseValue ? M(Fc(it, "full", a[0]), "mean") : M(a[0], "mean");
            var ra = ParseRed(a.Skip(1).ToArray(), x, "mean");
            NumKind outk = x.Kind == NumKind.Single ? NumKind.Single : NumKind.Double;
            if (ra.OutType == "native") outk = x.Kind == NumKind.Logical ? NumKind.Double : x.Kind;
            else if (ra.OutType == "double") outk = NumKind.Double;
            if (!ra.HasDim && Is00(x)) return new Value[] { new MatrixValue(1, 1, new[] { double.NaN }, null, outk == NumKind.Single ? NumKind.Single : NumKind.Double) };
            bool omit = ra.Omit;
            var r = GroupReduce(x, ra.Dims, v => MeanOf(v, omit), NumKind.Double, x.Im, x.Im != null ? v => MeanOf(v, omit) : null);
            if (outk == NumKind.Single) return new Value[] { Fc(it, "single", r) };
            if (NumTypes.IsInt(outk)) return new Value[] { Fc(it, "cast", r, MatrixValue.Str(NumTypes.Name(outk))) };
            return new Value[] { r.Im != null ? r.Narrow() : r };
        });
        Reg("median", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("median");
            if (!(IsNumL(a[0]))) throw new OctaveException("median: X must be either numeric or logical");
            var x = a[0] is Sparse.SparseValue ? M(Fc(it, "full", a[0]), "median") : M(a[0], "median");
            var ra = ParseRed(a.Skip(1).ToArray(), x, "median");
            NumKind outk = x.Kind == NumKind.Logical ? NumKind.Double : x.Kind;
            if (ra.OutType == "double") outk = NumKind.Double;
            if (ra.OutType == "native" && x.Kind == NumKind.Logical) outk = NumKind.Logical;
            if (!ra.HasDim && Is00(x)) return new Value[] { new MatrixValue(1, 1, new[] { double.NaN }, null, outk == NumKind.Single ? NumKind.Single : NumKind.Double) };
            bool omit = ra.Omit;
            var r = GroupReduce(x, ra.Dims, v => MedianOf(v, omit));
            if (outk == NumKind.Double) return new Value[] { r };
            return new Value[] { Fc(it, "cast", r, MatrixValue.Str(NumTypes.Name(outk))) };
        });
        Value[] VarImpl(Interpreter it, Value[] a, int n, string fn, bool sqrt)
        {
            if (a.Length < 1 || a.Length > 4) Usage(fn);
            if (!(a[0] is MatrixValue xm0 && !xm0.IsChar && (xm0.Kind is NumKind.Double or NumKind.Single or NumKind.Logical) || a[0] is Sparse.SparseValue))
                throw new OctaveException($"{fn}: X must be a floating point numeric vector or matrix");
            var x = a[0] is Sparse.SparseValue ? M(Fc(it, "full", a[0]), fn) : M(a[0], fn);
            double w = 0; MatrixValue? wv = null;
            var rest = a.Skip(1).ToList();
            if (rest.Count > 0 && !IsString(rest[0]))
            {
                var wm = M(rest[0], fn); rest.RemoveAt(0);
                if (wm.Numel == 1) { if (wm.Re[0] < 0) throw new OctaveException($"{fn}: weights must not contain any negative values"); if (wm.Re[0] != 0 && wm.Re[0] != 1) throw new OctaveException($"{fn}: normalization scalar must be either 0 or 1"); w = wm.Re[0]; }
                else if (wm.Numel > 1) { if (wm.Re.Any(q => q < 0)) throw new OctaveException($"{fn}: weights must not contain any negative values"); wv = wm; }
            }
            if (rest.Count > 2 || rest.Count == 2 && !IsString(rest[1])) Usage(fn);
            var ra = ParseRed(rest.ToArray(), x, fn);
            NumKind outk = x.Kind == NumKind.Single ? NumKind.Single : NumKind.Double;
            if (!ra.HasDim && Is00(x)) { var nanv = new MatrixValue(1, 1, new[] { double.NaN }, null, outk); return new Value[] { nanv, nanv }; }
            bool omit = ra.Omit;
            MatrixValue r, mu;
            if (wv != null)
            {
                if (ra.Dims.Length != 1) throw new OctaveException($"{fn}: weight vector length does not match");
                var wts = wv.Re;
                int dlen = ra.Dims[0] <= x.Dims.Length ? x.Dims[ra.Dims[0] - 1] : 1;
                if (!(wv.R == 1 || wv.C == 1) || wts.Length != dlen) throw new OctaveException($"{fn}: weight vector length does not match size of operating dimension");
                double ws = wts.Sum();
                r = GroupReduce(x, ra.Dims, v => { double m = 0; for (int k = 0; k < v.Length; k++) m += wts[k] * v[k]; m /= ws; double s = 0; for (int k = 0; k < v.Length; k++) s += wts[k] * (v[k] - m) * (v[k] - m); return s / ws; });
                mu = GroupReduce(x, ra.Dims, v => { double m = 0; for (int k = 0; k < v.Length; k++) m += wts[k] * v[k]; return m / ws; });
            }
            else
            {
                var im = x.Im;
                if (im != null)
                {
                    // variance of complex: |x - mean|^2
                    var idxs = Enumerable.Range(0, x.Numel).Select(k => (double)k).ToArray();
                    var ix = new MatrixValue(x.R, x.C, idxs, null) { ND = x.ND?.ToArray() };
                    r = GroupReduce(ix, ra.Dims, v => VarOf(v.Select(k => x.Re[(int)k]).ToArray(), v.Select(k => im[(int)k]).ToArray(), w, omit));
                }
                else r = GroupReduce(x, ra.Dims, v => VarOf(v, null, w, omit));
                mu = GroupReduce(x, ra.Dims, v => MeanOf(v, omit), NumKind.Double, x.Im, x.Im != null ? v => MeanOf(v, omit) : null);
            }
            if (sqrt) for (int k = 0; k < r.Numel; k++) r.Re[k] = Math.Sqrt(r.Re[k]);
            Value rv = r, mv = mu.Im != null ? mu.Narrow() : mu;
            if (outk == NumKind.Single) { rv = Fc(it, "single", rv); mv = Fc(it, "single", mv); }
            return new[] { rv, mv };
        }
        Reg("var", (it, a, n) => VarImpl(it, a, n, "var", false));
        Reg("std", (it, a, n) => VarImpl(it, a, n, "std", true));

        void Simple(string fn, Func<Interpreter, Value, Value, Value> f, bool needNumeric = true)
        {
            Reg(fn, (it, a, n) =>
            {
                if (a.Length < 1 || a.Length > 3) Usage(fn);
                if (!IsNum(a[0])) throw new OctaveException($"{fn}: X must be a numeric array");
                Value dim = a.Length > 1 ? a[1] : Dn(DefaultDim(SizeOf(a[0])));
                return new[] { f(it, a[0], dim) };
            });
        }
        Simple("range", (it, x, d) => Bx("-", Fn(it, "max", 1, x, new MatrixValue(0, 0), d)[0], Fn(it, "min", 1, x, new MatrixValue(0, 0), d)[0]));
        Reg("meansq", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("meansq");
            if (!IsNum(a[0])) throw new OctaveException("meansq: X must be a numeric array");
            var ab = Fc(it, "abs", a[0]);
            return new[] { Fc(it, "mean", new[] { Bx(".*", ab, ab) }.Concat(a.Skip(1)).ToArray()) };
        });
        Reg("rms", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("rms");
            if (!IsNum(a[0])) throw new OctaveException("rms: X must be a numeric array");
            var ab = Fc(it, "abs", a[0]);
            return new[] { Fc(it, "sqrt", Fc(it, "mean", new[] { Bx(".*", ab, ab) }.Concat(a.Skip(1)).ToArray())) };
        });
        Reg("bounds", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("bounds");
            if (!IsNum(a[0])) throw new OctaveException("bounds: X must be a numeric array");
            var x = a[0]; Value dim = Dn(DefaultDim(SizeOf(x))); bool inc = false;
            foreach (var v in a.Skip(1)) { if (IsString(v)) inc = Sx(v).ToLowerInvariant() == "includenan"; else dim = v; }
            Value[] mx = { x, new MatrixValue(0, 0), dim };
            var s = Fc(it, "min", mx); var l = Fc(it, "max", mx);
            if (inc)
            {
                var nanm = Fc(it, "any", Fc(it, "isnan", x), dim);
                var sm = Copy(M(s, "bounds")); var lm = Copy(M(l, "bounds")); var nm = M(nanm, "bounds");
                for (int k = 0; k < sm.Numel; k++) if (nm.Re[k] != 0) { sm.Re[k] = double.NaN; lm.Re[k] = double.NaN; }
                s = sm; l = lm;
            }
            return new[] { s, l };
        });
        Reg("center", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("center");
            if (!IsNum(a[0])) throw new OctaveException("center: X must be a numeric array");
            var x = a[0];
            if (x is MatrixValue xm && xm.IsInteger) x = Fc(it, "double", x);
            if (x is ArrayValue av && av.Numel == 0) return new[] { x };
            var mu = Fc(it, "mean", new[] { x }.Concat(a.Skip(1)).ToArray());
            return new[] { Bx("-", x, mu) };
        });
        Reg("mad", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) Usage("mad");
            if (!IsNumL(a[0])) throw new OctaveException("mad: X must be a numeric or logical array");
            double opt = 0;
            if (a.Length > 1 && !(a[1] is ArrayValue e && e.Numel == 0))
            { if (!IsScal(a[1]) || (D(a[1], "mad") != 0 && D(a[1], "mad") != 1)) throw new OctaveException("mad: OPT must be 0 or 1"); opt = D(a[1], "mad"); }
            var x = a[0];
            Value dim;
            if (a.Length < 3)
            {
                if (x is MatrixValue xm && Is00(xm)) return new Value[] { xm.Kind == NumKind.Single ? Fc(it, "single", Dn(double.NaN)) : Dn(double.NaN) };
                dim = Dn(DefaultDim(SizeOf(x)));
            }
            else dim = a[2];
            var fcn = opt == 0 ? "mean" : "median";
            var c = Fc(it, fcn, x, dim, MatrixValue.Str("omitnan"));
            return new[] { Fc(it, fcn, Fc(it, "abs", Bx("-", x, c)), dim, MatrixValue.Str("omitnan")) };
        });
        Reg("quantile", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("quantile");
            return new[] { Quantile(it, a, "quantile", 1) };
        });
        Reg("prctile", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("prctile");
            if (!IsNum(a[0])) throw new OctaveException("prctile: X must be a numeric array");
            Value p = a.Length > 1 && !(a[1] is ArrayValue e && e.Numel == 0) ? a[1] : MatrixValue.Row(new double[] { 0, 25, 50, 75, 100 });
            if (!(IsNum(p) && p is MatrixValue pm && (pm.R == 1 || pm.C == 1))) throw new OctaveException("prctile: P must be a numeric vector");
            if (((MatrixValue)p).Re.Any(v => v < 0 || v > 100)) throw new OctaveException("prctile: P values must range from 0 to 100");
            var b = a.ToArray(); if (b.Length < 2) b = new[] { a[0], p }; b[1] = Bx("/", p, Dn(100));
            return new[] { Quantile(it, b, "quantile", 1) };
        });
        Reg("iqr", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("iqr");
            if (!IsNum(a[0])) throw new OctaveException("iqr: X must be a numeric array");
            var x = M(a[0], "iqr");
            int[] dims;
            if (a.Length > 1)
            {
                if (IsString(a[1]) && Sx(a[1]).ToLowerInvariant() == "all") dims = Enumerable.Range(1, Math.Max(2, x.Dims.Length)).ToArray();
                else if (a[1] is MatrixValue dm && !dm.IsChar && dm.Numel >= 1 && dm.Re.All(d => d >= 1 && d == Math.Floor(d) && double.IsFinite(d)) && (dm.R == 1 || dm.C == 1))
                { if (dm.Re.Distinct().Count() != dm.Numel) throw new OctaveException("iqr: VECDIM must contain non-repeating positive integers"); dims = dm.Re.Select(d => (int)d).ToArray(); }
                else throw new OctaveException("iqr: DIM must be a positive integer scalar, vector, or 'all'");
            }
            else
            {
                if (x.Numel == 0) { if (x.Dims.Length == 2 && Math.Max(x.R, x.C) <= 1) return new Value[] { Dn(double.NaN), MatrixValue.Col(new[] { double.NaN, double.NaN }) }; }
                dims = new[] { DefaultDim(x.Dims) };
            }
            var lo = GroupReduce(x, dims, v => QuantileOf(v, 0.25, 8));
            var hi = GroupReduce(x, dims, v => QuantileOf(v, 0.75, 8));
            var r = Bx("-", hi, lo);
            return new[] { r, Fc(it, "cat", Dn(dims.Min()), lo, hi) };
        });
        Reg("zscore", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("zscore");
            if (!IsNumL(a[0])) throw new OctaveException("zscore: X must be a numeric or logical array");
            Value opt = Dn(0);
            if (a.Length > 1 && !(a[1] is ArrayValue e && e.Numel == 0))
            { if (!IsScal(a[1]) || (D(a[1], "zscore") != 0 && D(a[1], "zscore") != 1)) throw new OctaveException("zscore: normalization OPT must be empty, 0, or 1"); opt = a[1]; }
            var x = a[0];
            if (SizeOf(x).Any(d => d == 0)) return new[] { x, x, x };
            if (x is MatrixValue xm && (xm.IsInteger || xm.Kind == NumKind.Logical)) x = Fc(it, "double", x);
            var rest = a.Skip(2).ToArray();
            var mu = Fc(it, "mean", new[] { x }.Concat(rest).ToArray());
            var sg = Fc(it, "std", new[] { x, opt }.Concat(rest).ToArray());
            var s = Copy(M(sg, "zscore")); for (int k = 0; k < s.Numel; k++) if (s.Re[k] == 0) s.Re[k] = 1;
            return new[] { Bx("./", Bx("-", x, mu), s), mu, sg };
        });
        Reg("moment", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("moment");
            if (!IsNum(a[0]) || ((ArrayValue)a[0]).Numel == 0) throw new OctaveException("moment: X must be a non-empty numeric array");
            if (!(IsNum(a[1]) && IsScal(a[1]))) throw new OctaveException("moment: P must be a numeric scalar");
            var x = a[0]; Value dim; string type = "c";
            if (a.Length < 3 || a[2] is ArrayValue e && e.Numel == 0 && !(IsString(a[2]))) dim = Dn(DefaultDim(SizeOf(x)));
            else if (IsString(a[2]) && Sx(a[2]) == "all") { x = Fc(it, "vec", x); dim = Dn(1); }
            else if (IsString(a[2])) { dim = Dn(DefaultDim(SizeOf(x))); type = Sx(a[2]); }
            else dim = a[2];
            if (a.Length == 4) { if (!(a[3] is MatrixValue tm && tm.IsChar)) throw new OctaveException("moment: TYPE must be a character vector"); type = Sx(a[3]); }
            var sz = SizeOf(x); int di = (int)D(dim, "moment"); double cnt = di <= sz.Length ? sz[di - 1] : 1;
            if (!type.Contains('r')) x = Fc(it, "center", x, dim);
            if (type.Contains('a')) x = Fc(it, "abs", x);
            return new[] { Bx("/", Fc(it, "sum", Bx(".^", x, a[1]), dim), Dn(cnt)) };
        });
        foreach (var kurt in new[] { false, true })
        {
            string fn = kurt ? "kurtosis" : "skewness";
            Reg(fn, (it, a, n) =>
            {
                if (a.Length < 1 || a.Length > 3) Usage(fn);
                if (!IsNum(a[0])) throw new OctaveException($"{fn}: X must be a numeric array");
                double flag = 1;
                if (a.Length > 1 && !(a[1] is ArrayValue e && e.Numel == 0))
                { if (!IsScal(a[1]) || (D(a[1], fn) != 0 && D(a[1], fn) != 1)) throw new OctaveException($"{fn}: FLAG must be 0 or 1"); flag = D(a[1], fn); }
                var x = M(a[0], fn);
                int[] dims = a.Length > 2 ? ParseDims(a[2], x, fn) : new[] { DefaultDim(x.Dims) };
                var r = GroupReduce(x, dims, v =>
                {
                    var w = v.Where(d => !double.IsNaN(d)).ToArray(); int cnt = w.Length;
                    if (cnt == 0) return double.NaN;
                    double m = w.Average(); double m2 = w.Sum(d => (d - m) * (d - m)) / cnt;
                    double y;
                    if (!kurt) { double s = Math.Sqrt(m2); y = w.Sum(d => Math.Pow(d - m, 3)) / (cnt * s * s * s); if (s == 0) return double.NaN; if (flag == 0) y = cnt > 2 ? y * Math.Sqrt(cnt * (cnt - 1.0)) / (cnt - 2) : double.NaN; }
                    else { y = w.Sum(d => Math.Pow(d - m, 4)) / (cnt * m2 * m2); if (m2 == 0) return double.NaN; if (flag == 0) { if (cnt > 3) { double C = (cnt - 1.0) / ((cnt - 2.0) * (cnt - 3.0)); y = 3 + C * ((cnt + 1) * y - 3 * (cnt - 1)); } else y = double.NaN; } }
                    return y;
                });
                return new Value[] { x.Kind == NumKind.Single ? Fc(it, "single", r) : r };
            });
        }
        Reg("cov", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 4) Usage("cov");
            if (!IsNumL(a[0])) throw new OctaveException("cov: X must be a numeric or logical vector or matrix");
            var x = a[0]; double opt = 0; string nanflag = "includenan"; Value? y = null;
            var va = a.Skip(1).ToArray();
            bool isOpt(Value v) => IsNumL(v) && IsScal(v) && (D(v, "cov") == 0 || D(v, "cov") == 1);
            switch (va.Length)
            {
                case 3:
                    if (IsString(va[0]) || IsString(va[1])) throw new OctaveException(IsString(va[2]) ? "cov: only one NANFLAG parameter may be specified" : "cov: NANFLAG parameter must be the last input");
                    y = va[0]; opt = D(va[1], "cov"); nanflag = Sx(va[2]).ToLowerInvariant(); break;
                case 2:
                    if (IsString(va[0])) throw new OctaveException("cov: NANFLAG parameter must be the last input");
                    if (IsString(va[1])) { nanflag = Sx(va[1]).ToLowerInvariant(); if (isOpt(va[0])) opt = D(va[0], "cov"); else y = va[0]; }
                    else { y = va[0]; if (!IsScal(va[1])) throw new OctaveException("cov: normalization parameter OPT must be 0 or 1"); opt = D(va[1], "cov"); }
                    break;
                case 1:
                    if (IsString(va[0])) nanflag = Sx(va[0]).ToLowerInvariant(); else if (isOpt(va[0])) opt = D(va[0], "cov"); else y = va[0];
                    break;
            }
            var xm = M(x is Sparse.SparseValue ? Fc(it, "full", x) : x, "cov");
            if (xm.R == 1 && xm.ND == null) xm = M(Fc(it, "transpose", xm), "cov");
            if (y != null)
            {
                if (!IsNumL(y)) throw new OctaveException("cov: Y must be a numeric or logical vector or matrix");
                if (((ArrayValue)y).Numel != xm.Numel) throw new OctaveException("cov: X and Y must have the same number of observations");
                xm = M(Fc(it, "horzcat", Fc(it, "vec", xm), Fc(it, "vec", y)), "cov");
            }
            if (!(nanflag is "includenan" or "omitrows" or "partialrows")) throw new OctaveException($"cov: unknown NANFLAG parameter '{nanflag}'");
            if (opt != 0 && opt != 1) throw new OctaveException("cov: normalization parameter OPT must be 0 or 1");
            if (xm.ND != null) throw new OctaveException("cov: X must be a 2-D matrix or vector");
            bool single = xm.Kind == NumKind.Single || y is MatrixValue ym && ym.Kind == NumKind.Single;
            int R = xm.R, C = xm.C;
            if (xm.Numel == 0)
            {
                Value e = R == 0 && C == 0 ? Dn(double.NaN) : C == 0 ? new MatrixValue(0, 0) : Fc(it, "NaN", Dn(C), Dn(C));
                return new[] { single ? Fc(it, "single", e) : e };
            }
            var rows = Enumerable.Range(0, R).ToList();
            if (nanflag != "includenan") rows = rows.Where(i => Enumerable.Range(0, C).All(j => !double.IsNaN(xm.Re[j * R + i]) && !(xm.Im != null && double.IsNaN(xm.Im[j * R + i])))).ToList();
            int N = rows.Count;
            var res = new MatrixValue(C, C); bool cx = xm.Im != null; if (cx) res.Im = new double[C * C];
            var mr = new double[C]; var mi = new double[C];
            for (int j = 0; j < C; j++) { foreach (var i in rows) { mr[j] += xm.Re[j * R + i]; if (cx) mi[j] += xm.Im![j * R + i]; } mr[j] /= N; mi[j] /= N; }
            double den = N == 1 ? 1 : (opt == 1 ? N : N - 1);
            for (int p = 0; p < C; p++) for (int q = 0; q < C; q++)
            {
                double sr = 0, si = 0;
                foreach (var i in rows)
                {
                    double ar = xm.Re[p * R + i] - mr[p], ai = cx ? xm.Im![p * R + i] - mi[p] : 0;
                    double br = xm.Re[q * R + i] - mr[q], bi = cx ? xm.Im![q * R + i] - mi[q] : 0;
                    sr += ar * br + ai * bi; si += ar * bi - ai * br;   // conj(a)*b
                }
                res.Re[q * C + p] = N == 0 ? double.NaN : sr / den; if (cx) res.Im![q * C + p] = N == 0 ? double.NaN : si / den;
            }
            Value rv = cx ? res.Narrow() : res;
            return new[] { single ? Fc(it, "single", rv) : rv };
        });
        Reg("corr", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("corr");
            if (!IsNumL(a[0])) throw new OctaveException("corr: X must be a numeric or logical vector or matrix");
            var x = M(a[0], "corr");
            bool single = x.Kind == NumKind.Single;
            if (a.Length == 2)
            {
                if (!IsNumL(a[1])) throw new OctaveException("corr: Y must be a numeric or logical vector or matrix");
                var y = M(a[1], "corr"); single |= y.Kind == NumKind.Single;
                if (x.R != y.R) throw new OctaveException("corr: X and Y must have the same number of rows");
                bool rowx = x.R == 1 && x.ND == null, rowy = y.R == 1 && y.ND == null;
                if ((!rowy && x.ND != null) || (!rowx && y.ND != null)) throw new OctaveException("corr: X and Y must be two-dimensional unless the other input is a scalar or row vector");
                if (rowx || rowy) { Value nn = Fc(it, "NaN", Dn(x.C), Dn(y.C)); return new[] { single ? Fc(it, "single", nn) : nn }; }
                var c = M(Fc(it, "cov", Fc(it, "horzcat", x, y)), "corr");
                var sub = IndexAny(it, c, new Value[] { Fc(it, "colon", Dn(1), Dn(x.C)), Fc(it, "colon", Dn(x.C + 1), Dn(x.C + y.C)) });
                var s = Bx("*", Fc(it, "ctranspose", Fc(it, "std", x, new MatrixValue(0, 0), Dn(1))), Fc(it, "std", y, new MatrixValue(0, 0), Dn(1)));
                return new[] { Bx("./", sub, s) };
            }
            if (x.R == 1 && x.ND == null) { Value nn = Fc(it, "NaN", Dn(x.C)); return new[] { single ? Fc(it, "single", nn) : nn }; }
            var cc = Fc(it, "cov", x);
            var sd = Fc(it, "sqrt", Fc(it, "diag", cc));
            return new[] { Bx("./", cc, Bx("*", sd, Fc(it, "ctranspose", sd))) };
        });
        Reg("corrcoef", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 6) Usage("corrcoef");
            var x = a[0]; var va = a.Skip(1).ToList(); string rows = "all";
            if (va.Count > 0 && IsNum(va[0]))
            {
                var y = va[0]; int nx = ((ArrayValue)x).Numel, ny = ((ArrayValue)y).Numel;
                if (nx > 0 && ny > 0 && nx != ny) throw new OctaveException("corrcoef: X and Y must be the same size");
                x = Fc(it, "horzcat", Fc(it, "vec", x), Fc(it, "vec", y)); va.RemoveAt(0);
            }
            for (int i = 0; i < va.Count; i += 2)
            {
                if (!IsString(va[i])) throw new OctaveException($"corrcoef: parameter {i + 1} must be a string");
                var par = Sx(va[i]);
                if (i + 1 >= va.Count) throw new OctaveException($"corrcoef: parameter \"{par}\" missing value");
                var val = va[i + 1];
                switch (par.ToLowerInvariant())
                {
                    case "alpha": if (!(IsNum(val) && IsScal(val) && D(val, "") >= 0 && D(val, "") <= 1)) throw new OctaveException("corrcoef: \"alpha\" must be a scalar between 0 and 1"); break;
                    case "rows":
                        if (!IsString(val)) throw new OctaveException("corrcoef: \"rows\" value must be a string");
                        rows = Sx(val).ToLowerInvariant();
                        if (!(rows is "all" or "complete" or "pairwise")) throw new OctaveException("corrcoef: \"rows\" must be \"all\", \"complete\", or \"pairwise\"");
                        break;
                    default: throw new OctaveException($"corrcoef: Unknown option \"{par}\"");
                }
            }
            var xm = M(x, "corrcoef");
            if (xm.R == 1) xm = M(Fc(it, "vec", xm), "corrcoef");
            if (rows == "complete")
            {
                var keep = Enumerable.Range(0, xm.R).Where(i => Enumerable.Range(0, xm.C).All(j => !double.IsNaN(xm.Re[j * xm.R + i]))).Select(i => (double)(i + 1)).ToArray();
                xm = M(IndexAny(it, xm, new Value[] { MatrixValue.Col(keep), MatrixValue.Str(":") }), "corrcoef");
            }
            if (xm.Numel <= 1) { var nn = Dn(double.NaN); return new[] { nn, nn, nn, nn }; }
            int m = xm.R, nc = xm.C; var r = new MatrixValue(nc, nc); var pv = new MatrixValue(nc, nc);
            for (int i = 0; i < nc; i++) { r.Re[i * nc + i] = 1; pv.Re[i * nc + i] = 1; }
            for (int i = 0; i < nc; i++)
            {
                if (rows != "pairwise" && Enumerable.Range(0, m).Any(k => double.IsNaN(xm.Re[i * m + k]))) { r.Re[i * nc + i] = double.NaN; pv.Re[i * nc + i] = double.NaN; }
                for (int j = i + 1; j < nc; j++)
                {
                    var idx = Enumerable.Range(0, m).Where(k => rows != "pairwise" || !double.IsNaN(xm.Re[i * m + k]) && !double.IsNaN(xm.Re[j * m + k])).ToArray();
                    var xi = idx.Select(k => xm.Re[i * m + k]).ToArray(); var xj = idx.Select(k => xm.Re[j * m + k]).ToArray();
                    double mi = xi.Average(), mj = xj.Average();
                    double sij = 0, sii = 0, sjj = 0;
                    for (int k = 0; k < xi.Length; k++) { sij += (xi[k] - mi) * (xj[k] - mj); sii += (xi[k] - mi) * (xi[k] - mi); sjj += (xj[k] - mj) * (xj[k] - mj); }
                    double rr = sij / Math.Sqrt(sii * sjj);
                    r.Re[j * nc + i] = r.Re[i * nc + j] = rr;
                    if (n > 1)
                    {
                        int df = xi.Length - 2; double T = rr * Math.Sqrt(df / (1 - rr * rr));
                        double cdf = D(Fc(it, "betainc", Dn(df / (df + T * T)), Dn(df / 2.0), Dn(0.5)), "corrcoef");
                        pv.Re[j * nc + i] = pv.Re[i * nc + j] = cdf;
                    }
                }
            }
            return new Value[] { r, pv };
        });
        RegErrMetric("rmse", false); RegErrMetric("mape", true);
        Reg("normalize", (it, a, n) =>
        {
            if (a.Length < 1) Usage("normalize");
            if (!IsNumL(a[0])) throw new OctaveException("normalize: X must be a numeric vector, matrix, or array");
            var x = a[0];
            if (x is MatrixValue xm0 && xm0.IsInteger || x is MatrixValue xl && xl.Kind == NumKind.Logical) throw new OctaveException("normalize: X must be a numeric vector, matrix, or array");
            int p = 1; Value dim = Dn(DefaultDim(SizeOf(x)));
            if (a.Length > 1 && !IsString(a[1]))
            {
                if (!(IsScal(a[1]) && D(a[1], "") >= 1 && D(a[1], "") == Math.Floor(D(a[1], "")))) throw new OctaveException("normalize: DIM must be a positive integer scalar");
                dim = a[1]; p = 2;
            }
            string method = "zscore"; Value? opt = null;
            Value? cen = null, scl = null;
            for (; p < a.Length; p++)
            {
                if (!IsString(a[p])) throw new OctaveException("normalize: unknown method or option");
                var s = Sx(a[p]).ToLowerInvariant();
                Value? nx = p + 1 < a.Length && !IsString(a[p + 1]) ? a[p + 1] : p + 1 < a.Length && IsString(a[p + 1]) && s is "zscore" or "scale" or "center" && Sx(a[p + 1]).ToLowerInvariant() is "std" or "robust" or "mad" or "first" or "iqr" or "mean" or "median" ? a[p + 1] : null;
                switch (s)
                {
                    case "zscore": case "norm": case "range": case "scale": case "center": case "medianiqr":
                        method = s; opt = nx; if (nx != null) p++; break;
                    default: throw new OctaveException($"normalize: unknown method or option '{s}'");
                }
            }
            Value mean(Value v) => Fc(it, "mean", v, dim, MatrixValue.Str("omitnan"));
            Value std(Value v) => Fc(it, "std", v, new MatrixValue(0, 0), dim, MatrixValue.Str("omitnan"));
            Value z0 = Dn(0), one = Dn(1);
            switch (method)
            {
                case "zscore":
                    {
                        var o = opt != null ? Sx(opt).ToLowerInvariant() : "std";
                        if (o == "robust") { var md = Fc(it, "median", x, dim, MatrixValue.Str("omitnan")); cen = md; scl = Fc(it, "median", Fc(it, "abs", Bx("-", x, md)), dim, MatrixValue.Str("omitnan")); }
                        else { cen = mean(x); scl = std(x); }
                        break;
                    }
                case "norm":
                    {
                        double pn = opt != null ? D(opt, "normalize") : 2; cen = z0;
                        scl = double.IsPositiveInfinity(pn) ? Fn(it, "max", 1, Fc(it, "abs", x), new MatrixValue(0, 0), dim)[0] : Bx(".^", Fc(it, "sum", Bx(".^", Fc(it, "abs", x), Dn(pn)), dim), Dn(1 / pn));
                        break;
                    }
                case "range":
                    {
                        double l = 0, u = 1; if (opt != null) { var om = M(opt, "normalize"); l = om.Re[0]; u = om.Re[1]; }
                        var mn = Fn(it, "min", 1, x, new MatrixValue(0, 0), dim)[0]; var mx = Fn(it, "max", 1, x, new MatrixValue(0, 0), dim)[0];
                        var t = Bx("./", Bx("-", x, mn), Bx("-", mx, mn));
                        return new[] { Bx("+", Bx("*", t, Dn(u - l)), Dn(l)), mn, Bx("/", Bx("-", mx, mn), Dn(u - l)) };
                    }
                case "scale":
                    {
                        cen = z0; var o = opt;
                        if (o == null || IsString(o) && Sx(o) == "std") scl = std(x);
                        else if (IsString(o) && Sx(o) == "mad") scl = Fc(it, "median", Fc(it, "abs", Bx("-", x, Fc(it, "median", x, dim, MatrixValue.Str("omitnan")))), dim, MatrixValue.Str("omitnan"));
                        else if (IsString(o) && Sx(o) == "first") scl = IndexFirst(it, x, dim);
                        else if (IsString(o) && Sx(o) == "iqr") scl = Fc(it, "iqr", x, dim);
                        else scl = o;
                        break;
                    }
                case "center":
                    {
                        scl = one; var o = opt;
                        if (o == null || IsString(o) && Sx(o) == "mean") cen = mean(x);
                        else if (IsString(o) && Sx(o) == "median") cen = Fc(it, "median", x, dim, MatrixValue.Str("omitnan"));
                        else cen = o;
                        break;
                    }
                case "medianiqr":
                    cen = Fc(it, "median", x, dim, MatrixValue.Str("omitnan")); scl = Fc(it, "iqr", x, dim); break;
            }
            var res = Bx("./", Bx("-", x, cen!), scl!);
            return new[] { res, cen!, scl! };
        });
    }

    static Value IndexFirst(Interpreter it, Value x, Value dim)
    {
        var sz = SizeOf(x); int d = (int)D(dim, ""); var idx = new Value[Math.Max(sz.Length, d)];
        for (int k = 0; k < idx.Length; k++) idx[k] = MatrixValue.Str(":");
        idx[d - 1] = Dn(1);
        return IndexAny(it, x, idx);
    }

    static void RegErrMetric(string fn, bool pct)
    {
        Reg(fn, (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 7) Usage(fn);
            bool fl(Value v) => v is MatrixValue m && !m.IsChar && (m.Kind is NumKind.Double or NumKind.Single);
            if (!fl(a[0])) throw new OctaveException($"{fn}: F must be a floating point numeric array");
            if (!fl(a[1])) throw new OctaveException($"{fn}: A must be a floating point numeric array");
            bool single = ((MatrixValue)a[0]).Kind == NumKind.Single || ((MatrixValue)a[1]).Kind == NumKind.Single;
            var F = Fc(it, "double", a[0]); var A = Fc(it, "double", a[1]);
            Value AF;
            try { AF = Bx("-", A, F); } catch (OctaveException) { throw new OctaveException($"{fn}: F and A must have compatible sizes"); }
            var va = a.Skip(2).ToList();
            Value? W = null;
            int wi = va.FindIndex(v => IsString(v) && Sx(v).ToLowerInvariant() == "weights");
            if (wi >= 0)
            {
                if (va.Count <= wi + 1) throw new OctaveException($"{fn}: paired input argument for 'Weights' is missing");
                W = va[wi + 1];
                if (!fl(W)) throw new OctaveException($"{fn}: WEIGHTS must be single or double");
                if (((MatrixValue)W).Re.Any(q => q < 0)) throw new OctaveException($"{fn}: WEIGHTS must be nonnegative");
                va.RemoveRange(wi, 2);
            }
            bool all = false, omit = false, omitzero = false;
            foreach (var v in va.Where(IsString).ToList())
            {
                switch (Sx(v).ToLowerInvariant())
                {
                    case "all": all = true; break;
                    case "omitnan": omit = true; break;
                    case "includenan": omit = false; break;
                    case "omitzero" when pct: omitzero = true; break;
                    case "includezero" when pct: omitzero = false; break;
                    default: Usage(fn); break;
                }
                va.Remove(v);
            }
            if (va.Count > 1 || va.Count == 1 && !IsNum(va[0])) Usage(fn);
            var afm = M(AF, fn); var Am = M(Bx(".*", A, Fc(it, "ones", new MatrixValue(1, afm.Dims.Length, afm.Dims.Select(q => (double)q).ToArray(), null))), fn);
            int[] dims = all ? Enumerable.Range(1, afm.Dims.Length).ToArray() : va.Count == 1 ? ParseDims(va[0], afm, fn) : new[] { DefaultDim(afm.Dims) };
            // compute per-element terms
            var term = new double[afm.Numel];
            for (int k = 0; k < term.Length; k++) term[k] = pct ? Math.Abs(afm.Re[k] / Am.Re[k]) : afm.Re[k] * afm.Re[k];
            var mask = new bool[term.Length];
            for (int k = 0; k < term.Length; k++) mask[k] = (omit && double.IsNaN(term[k])) || (omitzero && Am.Re[k] == 0);
            var wfull = new double[term.Length];
            if (W != null)
            {
                var wm = M(W, fn);
                if (wm.Numel == term.Length) Array.Copy(wm.Re, wfull, term.Length);
                else
                {
                    if (dims.Length != 1) throw new OctaveException($"{fn}: WEIGHTS vector must have the same length as the operating dimension");
                    var sz = afm.Dims; int d = dims[0]; int len = d <= sz.Length ? sz[d - 1] : 1;
                    if (!(wm.R == 1 || wm.C == 1) || wm.Numel != len) throw new OctaveException($"{fn}: WEIGHTS vector must have the same length as the operating dimension");
                    int stride = 1; for (int k = 0; k < d - 1; k++) stride *= sz[k];
                    for (int k = 0; k < term.Length; k++) wfull[k] = wm.Re[(k / stride) % len];
                }
            }
            else Array.Fill(wfull, 1.0);
            var idx = new MatrixValue(afm.R, afm.C, Enumerable.Range(0, term.Length).Select(k => (double)k).ToArray(), null) { ND = afm.ND?.ToArray() };
            var r = GroupReduce(idx, dims, g =>
            {
                double s = 0, ws = 0;
                foreach (var kd in g) { int k = (int)kd; if (mask[k]) continue; s += wfull[k] * term[k]; ws += wfull[k]; }
                double v = s / ws;
                return pct ? 100 * v : Math.Sqrt(v);
            });
            return new Value[] { single ? Fc(it, "single", r) : r };
        });
    }

    /// <summary>Octave quantile methods 1-9 on one sample (NaNs removed).</summary>
    static double QuantileOf(double[] v, double p, int method)
    {
        var x = v.Where(d => !double.IsNaN(d)).ToArray(); Array.Sort(x);
        int m = x.Length; if (m == 0) return double.NaN;
        if (m == 1) return x[0];
        double t;
        switch (method)
        {
            case 1: { t = Math.Ceiling(m * p); int i = (int)Math.Max(1, Math.Min(m, t)); return x[i - 1]; }
            case 2:
                {
                    double tt = m * p; int i = (int)Math.Max(1, Math.Min(m, Math.Ceiling(tt)));
                    if (tt == Math.Floor(tt) && tt >= 1 && tt < m) return (x[(int)tt - 1] + x[(int)tt]) / 2;
                    return x[i - 1];
                }
            case 3: { t = Math.Round(m * p - 0.5, MidpointRounding.ToEven); int i = (int)Math.Max(1, Math.Min(m, t)); return x[i - 1]; }
            case 4: t = m * p; break;
            case 5: t = m * p + 0.5; break;
            case 6: t = (m + 1) * p; break;
            case 7: t = (m - 1) * p + 1; break;
            case 8: t = (m + 1.0 / 3) * p + 1.0 / 3; break;
            case 9: t = (m + 0.25) * p + 0.375; break;
            default: throw new OctaveException("quantile: Unknown METHOD, '" + method + "'");
        }
        if (t <= 1) return x[0];
        if (t >= m) return x[m - 1];
        int lo = (int)Math.Floor(t); double f = t - lo;
        double a = x[lo - 1], b = x[lo];
        if (f == 0) return a;
        if (double.IsInfinity(a) || double.IsInfinity(b)) return a == b ? a : f < 0.5 ? a : b;
        return a + f * (b - a);
    }

    static Value Quantile(Interpreter it, Value[] a, string fn, int _)
    {
        if (!IsNum(a[0])) throw new OctaveException($"{fn}: X must be a numeric array");
        var x = M(a[0], fn);
        MatrixValue p = a.Length > 1 && !(a[1] is ArrayValue e && e.Numel == 0) ? M(a[1], fn) : MatrixValue.Row(new[] { 0, 0.25, 0.5, 0.75, 1 });
        if (!(IsNum(p) && (p.R == 1 || p.C == 1))) throw new OctaveException($"{fn}: P must be a numeric vector");
        double[] ps = p.Re;
        if (p.Numel == 1 && ps[0] == Math.Floor(ps[0]) && ps[0] > 1) { int np = (int)ps[0]; ps = Enumerable.Range(1, np).Select(k => k / (np + 1.0)).ToArray(); }
        else if (ps.Any(q => q < 0 || q > 1)) throw new OctaveException($"{fn}: P values must range from 0 to 1, unless specifying N evenly spaced cumulative probabilities");
        int method = 5;
        if (a.Length > 3)
        {
            if (!(IsScal(a[3]) && D(a[3], fn) == Math.Floor(D(a[3], fn)) && D(a[3], fn) >= 1 && D(a[3], fn) <= 9)) throw new OctaveException($"{fn}: Unknown METHOD, '{(IsScal(a[3]) ? D(a[3], fn).ToString() : "?")}'");
            method = (int)D(a[3], fn);
        }
        var sz = x.Dims; int dim;
        if (a.Length < 3)
        {
            dim = 1; for (int k = 0; k < sz.Length; k++) if (sz[k] > 1) { dim = k + 1; break; }
            if (x.Numel == 0)
            {
                if (sz.Length == 2 && Math.Max(sz[0], sz[1]) <= 1) return Fc(it, "NaN", Dn(p.R), Dn(p.C));
                var s2 = sz.ToArray(); s2[dim - 1] = ps.Length; return Fc(it, "NaN", new MatrixValue(1, s2.Length, s2.Select(q => (double)q).ToArray(), null));
            }
        }
        else
        {
            if (!(IsScal(a[2]) && D(a[2], fn) == Math.Floor(D(a[2], fn)) && D(a[2], fn) > 0)) throw new OctaveException($"{fn}: DIM must be a positive integer");
            dim = (int)D(a[2], fn);
        }
        int nd = Math.Max(sz.Length, dim); var s = new int[nd]; for (int k = 0; k < nd; k++) s[k] = k < sz.Length ? sz[k] : 1;
        int len = s[dim - 1]; int stride = 1; for (int k = 0; k < dim - 1; k++) stride *= s[k];
        int outer = x.Numel / Math.Max(1, len * stride);
        var os = s.ToArray(); os[dim - 1] = ps.Length;
        var res = new double[os.Aggregate(1, (q, r) => q * r)];
        var buf = new double[len];
        for (int o = 0; o < outer; o++) for (int st = 0; st < stride; st++)
        {
            for (int k = 0; k < len; k++) buf[k] = x.Re[o * len * stride + k * stride + st];
            for (int j = 0; j < ps.Length; j++) res[o * ps.Length * stride + j * stride + st] = len == 0 ? double.NaN : QuantileOf(buf, ps[j], method);
        }
        var r = new MatrixValue(os[0], os.Skip(1).Aggregate(1, (q, w) => q * w), res, null);
        r.SetDims(os);
        return x.Kind == NumKind.Single ? Fc(it, "single", r) : r;
    }
}
