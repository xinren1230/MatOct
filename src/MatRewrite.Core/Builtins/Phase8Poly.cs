// MatRewrite - GPL-3.0-or-later
// Piecewise polynomials (mkpp/unmkpp/ppval/spline/pchip) and unwrap, ported from Octave scripts.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static MatrixValue Dbl(Interpreter it, Value v, string fn)
    {
        if (v is MatrixValue m && m.Kind == NumKind.Double) return m;
        return M(Fc(it, "double", v), fn);
    }
    static MatrixValue RowOf(double[] d) => new MatrixValue(1, d.Length, d, null);

    static StructValue MkPP(double[] breaks, MatrixValue coefs, int[] d)
    {
        int len = breaks.Length - 1, D = d.Aggregate(1, (p, q) => p * q);
        int order = coefs.Numel / len / Math.Max(D, 1);
        if (D == 0) order = 0;
        var c = new MatrixValue(len * D, order, (double[])coefs.Re.Clone(), coefs.Im == null ? null : (double[])coefs.Im.Clone());
        var s = new StructValue();
        s.Set("form", MatrixValue.Str("pp"));
        s.Set("breaks", RowOf(breaks));
        s.Set("coefs", c);
        s.Set("pieces", Dn(len));
        s.Set("order", Dn(order));
        s.Set("dim", d.Length == 1 ? Dn(d[0]) : RowOf(d.Select(z => (double)z).ToArray()));
        return s;
    }

    static Value PPVal(Interpreter it, StructValue pp, Value xiv)
    {
        var x = Dbl(it, pp.Get("breaks"), "ppval").Re;
        var P = Dbl(it, pp.Get("coefs"), "ppval");
        int n = (int)D(pp.Get("pieces"), "ppval"), k = (int)D(pp.Get("order"), "ppval");
        var dv = Dbl(it, pp.Get("dim"), "ppval").Re.Select(z => (int)z).ToArray();
        int Dd = dv.Aggregate(1, (p, q) => p * q);
        var xm = Dbl(it, xiv, "ppval");
        var sxi = xm.Dims.ToArray();
        bool vec = IsVec(xiv);
        int xn = xm.Numel;
        bool cplx = P.Im != null || xm.Im != null;
        var re = new double[Dd * xn]; var im = cplx ? new double[Dd * xn] : null;
        int rows = n * Dd;
        bool desc = x.Length > 1 && x[^1] < x[0];
        for (int q = 0; q < xn; q++)
        {
            double xv = xm.Re[q];
            // lookup(x, xi, "lr")
            int idx = 1;
            if (!double.IsNaN(xv)) { int lo = 0, hi = x.Length - 1; while (lo < hi) { int mid = (lo + hi + 1) / 2; if (desc ? x[mid] >= xv : x[mid] <= xv) lo = mid; else hi = mid - 1; } idx = Math.Clamp(lo + 1, 1, x.Length - 1); }
            if (idx > n) idx = n;
            double dx = xv - x[idx - 1];
            for (int c = 0; c < Dd; c++)
            {
                int row = c + Dd * (idx - 1);
                double yr = 0, yi = 0;
                for (int j = 0; j < k; j++)
                {
                    yr = yr * dx + P.Re[row + rows * j];
                    if (cplx) yi = yi * dx + (P.Im != null ? P.Im[row + rows * j] : 0);
                }
                if (k == 0) { yr = 0; }
                re[c + Dd * q] = yr; if (cplx) im![c + Dd * q] = yi;
            }
        }
        int[] dims;
        if (Dd == 1 && dv.Length == 1) dims = sxi;
        else if (vec) dims = dv.Concat(new[] { xn }).ToArray();
        else dims = dv.Concat(sxi).ToArray();
        var r = new MatrixValue(1, re.Length, re, im);
        if (dims.Length < 2) dims = new[] { dims[0], 1 };
        r.SetDims(dims);
        if (pp.HasField("orient") && pp.Get("orient") is MatrixValue om && om.IsChar && om.AsString() == "first" && !(Dd == 1 && dv.Length == 1))
            return Fc(it, "shiftdim", r, Dn(dv.Length));
        return r.IsComplex ? r.Narrow() : r;
    }

    static double[] Tridiag(double[] sub, double[] dg, double[] sup, double[] rhs)
    {
        int n = dg.Length; var c = new double[n]; var d = new double[n]; var x = new double[n];
        // Gaussian elimination with partial pivoting would be safer; use general dense solve for small n, Thomas otherwise
        if (n <= 400)
        {
            var A = new double[n, n];
            for (int i = 0; i < n; i++) { A[i, i] = dg[i]; if (i + 1 < n) { A[i + 1, i] = sub[i]; A[i, i + 1] = sup[i]; } }
            var b = (double[])rhs.Clone();
            for (int col = 0; col < n; col++)
            {
                int p = col; for (int r2 = col + 1; r2 < n; r2++) if (Math.Abs(A[r2, col]) > Math.Abs(A[p, col])) p = r2;
                if (p != col) { for (int j = 0; j < n; j++) (A[p, j], A[col, j]) = (A[col, j], A[p, j]); (b[p], b[col]) = (b[col], b[p]); }
                for (int r2 = col + 1; r2 < n; r2++) { double f = A[r2, col] / A[col, col]; if (f == 0) continue; for (int j = col; j < n; j++) A[r2, j] -= f * A[col, j]; b[r2] -= f * b[col]; }
            }
            for (int i = n - 1; i >= 0; i--) { double s = b[i]; for (int j = i + 1; j < n; j++) s -= A[i, j] * x[j]; x[i] = s / A[i, i]; }
            return x;
        }
        c[0] = sup.Length > 0 ? sup[0] / dg[0] : 0; d[0] = rhs[0] / dg[0];
        for (int i = 1; i < n; i++) { double m = dg[i] - sub[i - 1] * c[i - 1]; c[i] = i < n - 1 ? sup[i] / m : 0; d[i] = (rhs[i] - sub[i - 1] * d[i - 1]) / m; }
        x[n - 1] = d[n - 1]; for (int i = n - 2; i >= 0; i--) x[i] = d[i] - c[i] * x[i + 1];
        return x;
    }

    // a: n(+2) x cols, column-major (rows = data points). Returns (breaks, coefs rows = comp + cols*piece, order)
    static (double[] br, double[] coef, int order) SplineCore(double[] x, double[][] A)
    {
        int n = x.Length, D = A.Length;
        bool complete = A.Length > 0 && A[0].Length == n + 2;
        var dfs = new double[D]; var dfe = new double[D];
        var a = new double[D][];
        for (int c = 0; c < D; c++)
        {
            if (complete) { dfs[c] = A[c][0]; dfe[c] = A[c][n + 1]; a[c] = A[c].Skip(1).Take(n).ToArray(); }
            else a[c] = (double[])A[c].Clone();
        }
        x = (double[])x.Clone();
        bool sorted = true; for (int i = 1; i < n; i++) if (x[i] < x[i - 1]) sorted = false;
        if (!sorted)
        {
            var ord = Enumerable.Range(0, n).OrderBy(i => x[i]).ToArray();
            x = ord.Select(i => x[i]).ToArray();
            for (int c = 0; c < D; c++) { var o = a[c]; a[c] = ord.Select(i => o[i]).ToArray(); }
        }
        var h = new double[n - 1]; for (int i = 0; i < n - 1; i++) h[i] = x[i + 1] - x[i];
        if (complete && n == 2)
        {
            var co = new double[D * 4]; double hh = x[1] - x[0];
            for (int c = 0; c < D; c++)
            {
                co[c] = (dfs[c] + dfe[c]) / (hh * hh) + 2 * (a[c][0] - a[c][1]) / (hh * hh * hh);
                co[c + D] = (-2 * dfs[c] - dfe[c]) / hh - 3 * (a[c][0] - a[c][1]) / (hh * hh);
                co[c + 2 * D] = dfs[c]; co[c + 3 * D] = a[c][0];
            }
            return (x, co, 4);
        }
        if (!complete && n == 2)
        {
            var co = new double[D * 2];
            for (int c = 0; c < D; c++) { co[c] = (a[c][1] - a[c][0]) / (x[1] - x[0]); co[c + D] = a[c][0]; }
            return (x, co, 2);
        }
        if (!complete && n == 3)
        {
            var co = new double[D * 3];
            for (int c = 0; c < D; c++)
            {
                var y = a[c];
                co[c] = (y[0] - y[2]) / ((x[2] - x[0]) * (x[1] - x[2])) + (y[1] - y[0]) / ((x[1] - x[0]) * (x[1] - x[2]));
                co[c + D] = (y[1] - y[0]) * (x[2] - x[0]) / ((x[1] - x[0]) * (x[2] - x[1])) + (y[0] - y[2]) * (x[1] - x[0]) / ((x[2] - x[0]) * (x[2] - x[1]));
                co[c + 2 * D] = y[0];
            }
            return (new[] { x.Min(), x.Max() }, co, 3);
        }
        int np = n - 1;
        var coef = new double[np * D * 4];
        for (int c = 0; c < D; c++)
        {
            var y = a[c]; var cc = new double[n];
            if (complete)
            {
                var g = new double[n];
                g[0] = (y[1] - y[0]) / h[0] - dfs[c];
                for (int i = 1; i < n - 1; i++) g[i] = (y[i + 1] - y[i]) / h[i] - (y[i] - y[i - 1]) / h[i - 1];
                g[n - 1] = dfe[c] - (y[n - 1] - y[n - 2]) / h[n - 2];
                var dg = new double[n]; dg[0] = h[0] / 3; dg[n - 1] = h[n - 2] / 3;
                for (int i = 1; i < n - 1; i++) dg[i] = (h[i - 1] + h[i]) / 3;
                var off = h.Select(v => v / 6).ToArray();
                cc = Tridiag(off, dg, off, g.Select(v => v / 2).ToArray());
            }
            else
            {
                var g = new double[n - 2];
                g[0] = 3 / (h[0] + h[1]) * (y[2] - y[1] - h[1] / h[0] * (y[1] - y[0]));
                g[n - 3] = 3 / (h[n - 2] + h[n - 3]) * (h[n - 3] / h[n - 2] * (y[n - 1] - y[n - 2]) - (y[n - 2] - y[n - 3]));
                double[] dg, ldg, udg;
                if (n > 4)
                {
                    for (int i = 1; i <= n - 4; i++) g[i] = 3 * (y[i + 2] - y[i + 1]) / h[i + 1] - 3 * (y[i + 1] - y[i]) / h[i];
                    dg = new double[n - 2]; for (int i = 0; i < n - 2; i++) dg[i] = 2 * (h[i] + h[i + 1]);
                    dg[0] -= h[0]; dg[n - 3] -= h[n - 2];
                    ldg = h.Skip(1).Take(n - 3).ToArray(); udg = (double[])ldg.Clone();
                    udg[0] -= h[0]; ldg[n - 4] -= h[n - 2];
                }
                else { dg = new[] { h[0] + 2 * h[1], 2 * h[1] + h[2] }; ldg = new[] { h[1] - h[2] }; udg = new[] { h[1] - h[0] }; }
                var s = Tridiag(ldg, dg, udg, g);
                for (int i = 0; i < n - 2; i++) cc[i + 1] = s[i];
                cc[0] = cc[1] + h[0] / h[1] * (cc[1] - cc[2]);
                cc[n - 1] = cc[n - 2] + h[n - 2] / h[n - 3] * (cc[n - 2] - cc[n - 3]);
            }
            for (int i = 0; i < np; i++)
            {
                double b = (y[i + 1] - y[i]) / h[i] - h[i] / 3 * (cc[i + 1] + 2 * cc[i]);
                double d = (cc[i + 1] - cc[i]) / (3 * h[i]);
                int row = c + D * i, R = np * D;
                coef[row] = d; coef[row + R] = cc[i]; coef[row + 2 * R] = b; coef[row + 3 * R] = y[i];
            }
        }
        return (x, coef, 4);
    }

    static double[] SplineEval1(double[] x, double[] y, double[] xi)
    {
        var (br, co, ord) = SplineCore(x, new[] { y });
        int np = br.Length - 1; var r = new double[xi.Length];
        for (int q = 0; q < xi.Length; q++)
        {
            int lo = 0; while (lo + 1 < np && br[lo + 1] <= xi[q]) lo++;
            double dx = xi[q] - br[lo], v = 0;
            for (int j = 0; j < ord; j++) v = v * dx + co[lo + np * j];
            r[q] = v;
        }
        return r;
    }

    static bool IsFlt(Value v) => v is MatrixValue m && !m.IsChar && (m.Kind == NumKind.Double || m.Kind == NumKind.Single) || v is Sparse.SparseValue;

    static void RegisterPhase8Poly()
    {
        Reg("polyval", (it, a, n) =>
        {
            if (a.Length < 2 || (n == 2 && a.Length < 3)) Usage("polyval");
            if (a.Length > 4) TooMany("polyval");
            if (!IsFlt(a[0]) || (!IsVec(a[0]) && NumelOf(a[0]) != 0)) throw new OctaveException("polyval: P must be a numeric floating point vector");
            if (!IsFlt(a[1])) throw new OctaveException("polyval: X must be numeric floating point");
            Value s = a.Length >= 3 ? a[2] : new MatrixValue(0, 0);
            if (n > 1)
            {
                if (NumelOf(s) == 0 && s is not StructValue) throw new OctaveException("polyval: S input is required for DY output argument");
                if (s is StructValue ss) { if (!(ss.HasField("R") && ss.HasField("normr") && ss.HasField("df"))) throw new OctaveException("polyval: S input is missing required fields"); }
                else throw new OctaveException("polyval: S input must be a structure");
            }
            if (a.Length == 4 && (!IsFlt(a[3]) || NumelOf(a[3]) < 2)) throw new OctaveException("polyval: MU must be numeric floating point with 2 values");
            Value x = a[1], p = a[0];
            if (NumelOf(p) == 0 || NumelOf(x) == 0)
            {
                bool sg = (p is MatrixValue pm && pm.IsSingle) || (x is MatrixValue xm && xm.IsSingle);
                var z = new MatrixValue(1, NumelOf(x), new double[NumelOf(x)], null); z.SetDims(SizeOf(x).Length < 2 ? new[] { 0, 0 } : SizeOf(x));
                if (sg) z.CastInPlace(NumKind.Single);
                return new Value[] { z };
            }
            if (a.Length == 4) x = Bx("/", Bx("-", x, IndexAny(it, a[3], new[] { Dn(1) })), IndexAny(it, a[3], new[] { Dn(2) }));
            int N = NumelOf(p) - 1;
            Value y = Bx(".*", IndexAny(it, p, new[] { Dn(1) }), Fc(it, "ones", RowOf(SizeOf(x).Select(z => (double)z).ToArray()), MatrixValue.Str(x.ClassName)));
            for (int i = 2; i <= N + 1; i++) y = Bx("+", Bx(".*", y, x), IndexAny(it, p, new[] { Dn(i) }));
            if (n > 1)
            {
                var ss = (StructValue)s; int k = NumelOf(x);
                var xc = Col(it, x);
                var A = Bx(".^", Bx("*", xc, Fc(it, "ones", Dn(1), Dn(N + 1))), Bx("*", Fc(it, "ones", Dn(k), Dn(1)), RowOf(Enumerable.Range(0, N + 1).Select(z => (double)(N - z)).ToArray())));
                var dy = Bx("/", Bx("*", Fc(it, "sqrt", Bx("+", Dn(1), Fc(it, "sumsq", Bx("/", A, ss.Get("R")), Dn(2)))), ss.Get("normr")), Fc(it, "sqrt", ss.Get("df")));
                dy = Fc(it, "reshape", dy, RowOf(SizeOf(x).Select(z => (double)z).ToArray()));
                return new[] { y, dy };
            }
            return new[] { y };
        });
        Reg("polyvalm", (it, a, n) =>
        {
            if (a.Length != 2) Usage("polyvalm");
            if (!(IsVec(a[0]) || NumelOf(a[0]) == 0)) throw new OctaveException("polyvalm: first argument must be a vector");
            var sz = SizeOf(a[1]);
            if (sz.Length != 2 || sz[0] != sz[1]) throw new OctaveException("polyvalm: second argument must be a square matrix");
            int N = NumelOf(a[0]);
            var cls = MatrixValue.Str(a[1].ClassName);
            if (N == 0) return new[] { Fc(it, "zeros", Dn(sz[0]), cls) };
            var id = Fc(it, "eye", Dn(sz[0]), cls);
            Value y = Bx("*", IndexAny(it, a[0], new[] { Dn(1) }), id);
            for (int i = 2; i <= N; i++) y = Bx("+", Bx("*", y, a[1]), Bx("*", IndexAny(it, a[0], new[] { Dn(i) }), id));
            return new[] { y };
        });
        Reg("polyfit", (it, a, n) =>
        {
            if (a.Length < 3) Usage("polyfit");
            if (a.Length > 3) TooMany("polyfit");
            bool scale = n == 3;
            bool yrow = SizeOf(a[1]).Length == 2 && SizeOf(a[1])[0] == 1;
            var xv = Dbl(it, a[0], "polyfit").Re.ToArray(); var yv = Dbl(it, a[1], "polyfit").Re;
            int nx = xv.Length;
            if (nx != yv.Length) throw new OctaveException("polyfit: X and Y must have the same number of points");
            double[] mu = null!;
            if (scale)
            {
                double mean = xv.Average(), sd = nx > 1 ? Math.Sqrt(xv.Sum(z => (z - mean) * (z - mean)) / (nx - 1)) : 0;
                mu = new[] { mean, sd }; for (int i = 0; i < nx; i++) xv[i] = (xv[i] - mean) / sd;
            }
            bool[] mask; int N, m; bool pad;
            if (a[2] is MatrixValue lm && lm.IsLogical)
            { mask = lm.Re.Select(z => z != 0).ToArray(); N = mask.Length - 1; m = mask.Count(z => z) - 1; pad = true; }
            else
            {
                if (!(IsScal(a[2]) && a[2] is MatrixValue nm && nm.Numel == 1 && nm.Re[0] >= 0 && !double.IsInfinity(nm.Re[0]) && nm.Re[0] == Math.Floor(nm.Re[0])))
                    throw new OctaveException("polyfit: N must be a non-negative integer");
                N = (int)((MatrixValue)a[2]).Re[0]; mask = Enumerable.Repeat(true, N + 1).ToArray(); m = N; pad = false;
            }
            if (m >= nx)
            {
                it.Warning("polyfit: degree of polynomial N is >= number of data points; solution is not unique.", "");
                m = nx; pad = true;
                if (scale && nx < 3) throw new OctaveException("polyfit: number of data points must be > 2 when scaling input X");
                for (int i = 0; i < mask.Length; i++) mask[i] = false;
                if (m == 1) mask[0] = true; else { for (int i = 0; i < m - 1; i++) mask[i] = true; mask[^1] = true; }
            }
            var cols = Enumerable.Range(0, N + 1).Where(j => mask[j]).ToArray();
            var V = new CMat(nx, cols.Length, false);
            for (int c = 0; c < cols.Length; c++) for (int i = 0; i < nx; i++) V[i, c] = Math.Pow(xv[i], N - cols[c]);
            var (Q, R, perm) = HouseQR(V, true, true, true);
            int kk = R.R, nc = cols.Length;
            var qty = new double[kk]; for (int i = 0; i < kk; i++) { double s = 0; for (int r = 0; r < nx; r++) s += Q[r, i].Real * yv[r]; qty[i] = s; }
            // solve r \ qty (r is kk x nc upper)
            var pz = new double[nc];
            if (kk == nc) { for (int i = nc - 1; i >= 0; i--) { double s = qty[i]; for (int j = i + 1; j < nc; j++) s -= R[i, j].Real * pz[j]; pz[i] = s / R[i, i].Real; } }
            else { var sol = Dbl(it, Bx("\\", R.ToMV(), new MatrixValue(kk, 1, qty, null)), "polyfit").Re; Array.Copy(sol, pz, nc); }
            var p = new double[nc]; for (int i = 0; i < nc; i++) p[perm[i]] = pz[i];
            var outs = new List<Value>();
            double[] full;
            if (pad) { full = new double[N + 1]; for (int i = 0; i < nc; i++) full[cols[i]] = p[i]; } else full = p;
            outs.Add(RowOf(full));
            if (n > 1)
            {
                var yf = new double[nx]; for (int i = 0; i < nx; i++) { double s = 0; for (int c = 0; c < nc; c++) s += V[i, c].Real * p[c]; yf[i] = s; }
                var st = new StructValue();
                st.Set("yf", yrow ? RowOf(yf) : new MatrixValue(nx, 1, yf, null));
                Value C;
                try
                {
                    var Rr = R.ToMV(); var RtR = Bx("*", Fc(it, "transpose", Rr), Rr);
                    var ci = Fc(it, "cholinv", RtR);
                    var kidx = RowOf(perm.Select(z => (double)z + 1).ToArray());
                    // C = cholinv(r'*r)(k,k)
                    C = IndexAny(it, ci, new Value[] { kidx, kidx });
                }
                catch (OctaveException) { var nn = new double[m * m]; Array.Fill(nn, double.NaN); C = new MatrixValue(m, m, nn, null); }
                var Vm = V.ToMV(); var Rm = R.ToMV();
                Value Pad(Value src)
                {
                    if (!pad) return src;
                    int rr = SizeOf(src)[0]; var z = new double[rr * (N + 1)]; var sm = (MatrixValue)src;
                    for (int c = 0; c < Math.Min(nc, sm.C); c++) for (int r = 0; r < rr; r++) z[r + rr * cols[c]] = sm.Re[r + rr * c];
                    return new MatrixValue(rr, N + 1, z, null);
                }
                // NB: Octave pads R with columns from r which is pivoted; mirror that literally
                st.Set("V", Pad(Vm)); st.Set("X", Pad(Vm)); st.Set("R", Pad(Rm)); st.Set("C", Pad(C));
                st.Set("df", Dn(Math.Max(0, nx - m - 1)));
                double resid = 0; for (int i = 0; i < nx; i++) resid += (yf[i] - yv[i]) * (yf[i] - yv[i]);
                st.Set("normr", Dn(Math.Sqrt(resid)));
                double ym = yv.Average(), tot = yv.Sum(z => (z - ym) * (z - ym));
                st.Set("rsquared", Dn(1 - resid / tot));
                outs.Add(st);
            }
            if (n > 2) outs.Add(RowOf(mu));
            return outs.ToArray();
        });
        Reg("shiftdim", (it, a, n) =>
        {
            if (a.Length < 1) Usage("shiftdim");
            if (a.Length > 2) TooMany("shiftdim");
            var sz = SizeOf(a[0]).ToList();
            int nd = sz.Count, ns;
            if (a.Length == 1) { ns = 0; while (ns < nd && sz[ns] == 1) ns++; if (ns == nd) ns = 0; }
            else
            {
                if (!IsScal(a[1]) || D(a[1], "shiftdim") != Math.Floor(D(a[1], "shiftdim"))) throw new OctaveException("shiftdim: NS must be a scalar integer");
                ns = I(a[1], "shiftdim");
            }
            Value y;
            if (ns == 0) y = a[0];
            else if (ns > 0)
            {
                int k = ns % nd;
                var perm = Enumerable.Range(0, nd).Select(i => (double)((i + k) % nd + 1)).ToArray();
                y = Fc(it, "permute", a[0], RowOf(perm));
            }
            else
            {
                var nsz = Enumerable.Repeat(1.0, -ns).Concat(sz.Select(z => (double)z)).ToArray();
                y = Fc(it, "reshape", a[0], RowOf(nsz));
            }
            return new[] { y, Dn(ns) };
        });
        Reg("mkpp", (it, a, n) =>
        {
            if (a.Length < 2) Usage("mkpp");
            if (a.Length > 3) TooMany("mkpp");
            if (!IsVec(a[0])) throw new OctaveException("mkpp: BREAKS must be a vector");
            if (NumelOf(a[0]) < 2) throw new OctaveException("mkpp: BREAKS must have at least one interval");
            var br = Dbl(it, a[0], "mkpp").Re;
            int[] d = a.Length == 3 ? Dbl(it, a[2], "mkpp").Re.Select(z => (int)z).ToArray() : new[] { 1 };
            return new Value[] { MkPP(br, Dbl(it, a[1], "mkpp"), d) };
        });
        Reg("unmkpp", (it, a, n) =>
        {
            if (a.Length < 1) Usage("unmkpp");
            if (a.Length > 1) TooMany("unmkpp");
            if (!(a[0] is StructValue s && s.HasField("form") && s.Get("form") is MatrixValue f && f.IsChar && f.AsString() == "pp"))
                throw new OctaveException("unmkpp: PP must be a piecewise polynomial structure");
            return new[] { s.Get("breaks"), s.Get("coefs"), s.Get("pieces"), s.Get("order"), s.Get("dim") };
        });
        Reg("ppval", (it, a, n) =>
        {
            if (a.Length != 2) Usage("ppval");
            if (!(a[0] is StructValue s && s.HasField("form") && s.Get("form") is MatrixValue f && f.IsChar && f.AsString() == "pp"))
                throw new OctaveException("ppval: first argument must be a pp-form structure");
            return new[] { PPVal(it, s, a[1]) };
        });
        Reg("spline", (it, a, n) =>
        {
            if (a.Length < 2) Usage("spline");
            if (a.Length > 3) TooMany("spline");
            var x = Dbl(it, a[0], "spline").Re;
            int N = x.Length;
            if (N < 2) throw new OctaveException("spline: requires at least 2 points");
            var ym = Dbl(it, a[1], "spline");
            var szy = ym.Dims.ToArray();
            // build columns (components) as arrays over points
            int rowsA, D; Func<int, int, int> at; // (point, comp) -> linear index into y
            if (szy.Length == 2 && (szy.Contains(N) || szy.Contains(N + 2)))
            {
                if (szy[1] == N || szy[1] == N + 2) { rowsA = szy[1]; D = szy[0]; int r0 = szy[0]; at = (p, c) => c + r0 * p; }
                else { rowsA = szy[0]; D = szy[1]; int r0 = szy[0]; at = (p, c) => p + r0 * c; szy = new[] { szy[1], szy[0] }; }
            }
            else { rowsA = szy[^1]; D = ym.Numel / Math.Max(rowsA, 1); int d0 = D; at = (p, c) => c + d0 * p; }
            Value Build(double[] src)
            {
                var cols = new double[D][];
                for (int c = 0; c < D; c++)
                {
                    var col = new double[rowsA]; for (int p = 0; p < rowsA; p++) col[p] = src[at(p, c)];
                    if (col.Any(double.IsNaN))
                    {
                        var ok = Enumerable.Range(0, rowsA).Where(p => !double.IsNaN(col[p])).ToArray();
                        var bad = Enumerable.Range(0, rowsA).Where(p => double.IsNaN(col[p])).ToArray();
                        var fill = SplineEval1(ok.Select(p => x[p]).ToArray(), ok.Select(p => col[p]).ToArray(), bad.Select(p => x[p]).ToArray());
                        for (int q = 0; q < bad.Length; q++) col[bad[q]] = fill[q];
                    }
                    cols[c] = col;
                }
                var (br, co, ord) = SplineCore(x, cols);
                int np = br.Length - 1;
                return MkPP(br, new MatrixValue(np * D, ord, co, null), szy.Take(szy.Length - 1).ToArray());
            }
            var pp = (StructValue)Build(ym.Re);
            if (ym.Im != null)
            {
                var pi = (StructValue)Build(ym.Im);
                var cr = (MatrixValue)pp.Get("coefs"); var ci = (MatrixValue)pi.Get("coefs");
                pp.Set("coefs", new MatrixValue(cr.R, cr.C, cr.Re, ci.Re).Narrow());
            }
            if (a.Length == 3) return new[] { PPVal(it, pp, a[2]) };
            return new Value[] { pp };
        });
        Reg("pchip", (it, a, n) =>
        {
            if (a.Length < 2) Usage("pchip");
            if (a.Length > 3) TooMany("pchip");
            if (a[1] is MatrixValue yc && yc.Im != null && yc.Im.Any(v => v != 0))
            {
                var ar = (Value[])a.Clone(); ar[1] = Fc(it, "real", yc); var ai = (Value[])a.Clone(); ai[1] = Fc(it, "imag", yc);
                var pr = Fc(it, "pchip", ar); var pi = Fc(it, "pchip", ai);
                if (pr is StructValue sr && pi is StructValue si)
                {
                    sr.Set("coefs", Bx("+", sr.Get("coefs"), Bx("*", new MatrixValue(1, 1, new[] { 0.0 }, new[] { 1.0 }), si.Get("coefs"))));
                    return new[] { (Value)sr };
                }
                return new[] { Bx("+", pr, Bx("*", new MatrixValue(1, 1, new[] { 0.0 }, new[] { 1.0 }), pi)) };
            }
            var x = Dbl(it, a[0], "pchip").Re.ToArray();
            int N = x.Length;
            var ym = Dbl(it, a[1], "pchip");
            int[] szy; int D;
            double[] y;
            if (IsVec(a[1]))
            {
                szy = new[] { 1, ym.Numel };
                if (ym.Numel != N) throw new OctaveException("pchip: length of X and Y must match");
                D = 1; y = ym.Re;
            }
            else
            {
                szy = ym.Dims.ToArray();
                if (N != szy[^1]) throw new OctaveException("pchip: length of X and last dimension of Y must match");
                D = ym.Numel / N; y = ym.Re;
            }
            var h = new double[N - 1]; for (int i = 0; i < N - 1; i++) h[i] = x[i + 1] - x[i];
            if (h.Length > 0 && h.All(v => v < 0))
            {
                Array.Reverse(x); var y2 = new double[y.Length];
                for (int p = 0; p < N; p++) for (int c = 0; c < D; c++) y2[c + D * p] = y[c + D * (N - 1 - p)];
                y = y2; for (int i = 0; i < N - 1; i++) h[i] = x[i + 1] - x[i];
            }
            else if (h.Any(v => v <= 0)) throw new OctaveException("pchip: X must be strictly monotonic");
            var Y = new MatrixValue(D, N, y, null);
            var dd = Dbl(it, Fc(it, "__pchip_deriv__", RowOf(x), Y, Dn(2)), "pchip").Re;
            int np = N - 1; var co = new double[np * D * 4]; int R = np * D;
            for (int i = 0; i < np; i++) for (int c = 0; c < D; c++)
            {
                double f1 = y[c + D * i], d1 = dd[c + D * i], d2 = dd[c + D * (i + 1)];
                double delta = (y[c + D * (i + 1)] - f1) / h[i];
                double del1 = (d1 - delta) / h[i], del2 = (d2 - delta) / h[i];
                double c3 = del1 + del2, c2 = -c3 - del1; c3 /= h[i];
                int row = c + D * i;
                co[row] = c3; co[row + R] = c2; co[row + 2 * R] = d1; co[row + 3 * R] = f1;
            }
            var pp = MkPP(x, new MatrixValue(R, 4, co, null), szy.Take(szy.Length - 1).ToArray());
            if (a.Length == 3) return new[] { PPVal(it, pp, a[2]) };
            return new Value[] { pp };
        });
        Reg("unwrap", (it, a, n) =>
        {
            if (a.Length < 1) Usage("unwrap");
            if (a.Length > 3) TooMany("unwrap");
            if (!(a[0] is MatrixValue xm0 && !xm0.IsChar && !(xm0.Im != null && false)) || (a[0] is MatrixValue cm && cm.IsChar))
                throw new OctaveException("unwrap: X must be numeric");
            double tol = Math.PI;
            if (a.Length >= 2 && NumelOf(a[1]) > 0) tol = Math.Abs(D(a[1], "unwrap"));
            var sz = SizeOf(a[0]);
            int dim;
            if (a.Length == 3)
            {
                if (!(IsNum(a[2]) && IsScal(a[2]) && D(a[2], "unwrap") == Math.Floor(D(a[2], "unwrap"))) || D(a[2], "unwrap") < 1)
                    throw new OctaveException("unwrap: DIM must be an integer and a valid dimension");
                dim = I(a[2], "unwrap");
            }
            else dim = FirstGt1(a[0]);
            if (dim > sz.Length || sz[dim - 1] == 1) return new[] { a[0] };
            var xm = Dbl(it, a[0], "unwrap");
            var re = (double[])xm.Re.Clone();
            int len = sz[dim - 1], stride = 1; for (int k = 0; k < dim - 1; k++) stride *= sz[k];
            int outer = re.Length / (len * stride);
            double rng = 2 * Math.PI;
            for (int o = 0; o < outer; o++) for (int s = 0; s < stride; s++)
            {
                int b = o * len * stride + s; double acc = 0; double prev = double.NaN; bool have = false;
                for (int k = 0; k < len; k++)
                {
                    int ix = b + k * stride; double v = re[ix];
                    if (!double.IsFinite(v)) continue;
                    if (have)
                    {
                        double d = prev - v;
                        double p = Math.Round(Math.Abs(d) / rng, MidpointRounding.AwayFromZero) * rng * ((d > tol ? 1 : 0) - (d < -tol ? 1 : 0));
                        acc += p;
                    }
                    prev = v; have = true;
                    re[ix] = v + acc;
                }
            }
            var r = new MatrixValue(1, re.Length, re, null); r.SetDims(sz);
            return new Value[] { r };
        });
    }
}
