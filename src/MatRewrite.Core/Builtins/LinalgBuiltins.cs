// MatRewrite - GPL-3.0-or-later
using Complex = System.Numerics.Complex;
using MathNet.Numerics.LinearAlgebra;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static MatrixValue Square(Value v, string fn)
    {
        var m = M(v, fn);
        if (m.R != m.C) throw new OctaveException($"{fn}: A must be a square matrix");
        return m;
    }

    static MatrixValue FromC(Vector<Complex> v)
    {
        var r = new MatrixValue(v.Count, 1, NumKind.Double, true);
        for (int k = 0; k < v.Count; k++) { r.Re[k] = v[k].Real; r.Im![k] = v[k].Imaginary; }
        return r.Narrow();
    }

    static void RegisterLinalg()
    {
        Reg1("det", a =>
        {
            var m = Square(a[0], "det");
            if (m.Numel == 0) return Num(1);
            if (m.IsComplex) return MatrixValue.Cplx(Ops.ToMathNetC(m).Determinant());
            return Num(Ops.ToMathNet(m).Determinant());
        });
        Reg1("inv", a => Ops.Inv(Square(a[0], "inverse")));
        Reg1("inverse", a => Ops.Inv(Square(a[0], "inverse")));
        Reg1("pinv", a => Ops.FromMathNet(Ops.ToMathNet(M(a[0], "pinv")).PseudoInverse()));
        Reg1("trace", a => { var m = Square(a[0], "trace"); double s = 0; for (int k = 0; k < m.R; k++) s += m.Re[k + k * m.R]; return Num(s); });
        Reg1("rank", a => { var m = M(a[0], "rank"); if (m.Numel == 0) return Num(0); return Num(Ops.ToMathNet(m).Rank()); });
        Reg1("norm", a =>
        {
            var m = M(a[0], "norm");
            if (m.Numel == 0) return Num(0);
            string p = a.Length > 1 ? (IsString(a[1]) ? S(a[1], "norm") : D(a[1], "norm").ToString(System.Globalization.CultureInfo.InvariantCulture)) : "2";
            bool vec = m.R == 1 || m.C == 1;
            var abs = Enumerable.Range(0, m.Numel).Select(k => m.At(k).Magnitude).ToArray();
            if (vec)
            {
                if (p == "Inf" || p == "inf" || p == "∞") return Num(abs.Max());
                if (p == "-Inf") return Num(abs.Min());
                if (p == "fro") p = "2";
                double pp = double.Parse(p, System.Globalization.CultureInfo.InvariantCulture);
                if (pp == 2) { double s = 0, sc = abs.Max(); if (sc == 0) return Num(0); foreach (var x in abs) s += (x / sc) * (x / sc); return Num(sc * Math.Sqrt(s)); }
                if (pp == 1) return Num(abs.Sum());
                return Num(Math.Pow(abs.Sum(x => Math.Pow(x, pp)), 1 / pp));
            }
            var A = Ops.ToMathNet(m);
            return p switch
            {
                "1" => Num(A.L1Norm()),
                "Inf" or "inf" => Num(A.InfinityNorm()),
                "fro" => Num(A.FrobeniusNorm()),
                _ => Num(A.L2Norm()),
            };
        });
        Reg1("cond", a => { var s = Ops.ToMathNet(M(a[0], "cond")).Svd(false).S; return Num(s[0] / s[s.Count - 1]); });
        Reg1("kron", a =>
        {
            var x = M(a[0], "kron"); var y = M(a[1], "kron");
            var r = new MatrixValue(x.R * y.R, x.C * y.C);
            for (int i = 0; i < x.R; i++) for (int j = 0; j < x.C; j++) for (int p = 0; p < y.R; p++) for (int q = 0; q < y.C; q++)
                            r.Re[(i * y.R + p) + (j * y.C + q) * r.R] = x.Re[i + j * x.R] * y.Re[p + q * y.R];
            return r;
        });
        Reg1("dot", a =>
        {
            var x = M(a[0], "dot"); var y = M(a[1], "dot");
            if (x.Numel != y.Numel) throw new OctaveException("dot: sizes of X and Y must match");
            if ((x.R == 1 || x.C == 1) && (y.R == 1 || y.C == 1))
            {
                Complex s = 0; for (int k = 0; k < x.Numel; k++) s += Complex.Conjugate(x.At(k)) * y.At(k);
                return MatrixValue.Cplx(s);
            }
            var prod = (MatrixValue)Ops.Binary(".*", x, y);
            return Reduce(prod, 1, v => v.Sum());
        });
        Reg1("cross", a =>
        {
            var x = M(a[0], "cross"); var y = M(a[1], "cross");
            if (x.Numel != 3 || y.Numel != 3) throw new OctaveException("cross: both X and Y must have 3 elements");
            var r = new[] { x.Re[1] * y.Re[2] - x.Re[2] * y.Re[1], x.Re[2] * y.Re[0] - x.Re[0] * y.Re[2], x.Re[0] * y.Re[1] - x.Re[1] * y.Re[0] };
            return x.R == 1 ? MatrixValue.Row(r) : MatrixValue.Col(r);
        });
        Reg("eig", (it, a, n) =>
        {
            var m = Square(a[0], "eig");
            if (m.Numel == 0) return Out(new MatrixValue(0, 1));
            var A = Ops.ToMathNet(m);
            bool sym = !m.IsComplex && A.IsSymmetric();
            var evd = A.Evd(sym ? Symmetricity.Symmetric : Symmetricity.Unknown);
            // sort ascending (by real part) as Octave does for symmetric problems
            var vals = evd.EigenValues;
            var order = Enumerable.Range(0, vals.Count).OrderBy(k => vals[k].Real).ThenBy(k => vals[k].Imaginary).ToArray();
            if (!sym) order = Enumerable.Range(0, vals.Count).ToArray();
            var lam = Vector<Complex>.Build.Dense(order.Select(k => vals[k]).ToArray());
            if (n <= 1) return Out(FromC(lam));
            var V = new MatrixValue(m.R, m.C);
            var ev = evd.EigenVectors;
            for (int j = 0; j < m.C; j++)
            {
                int src = order[j];
                // normalise sign so that the largest component is positive-ish like LAPACK output
                for (int i = 0; i < m.R; i++) V.Re[i + j * m.R] = ev[i, src];
            }
            var D = new MatrixValue(m.R, m.C, NumKind.Double, lam.Any(z => z.Imaginary != 0));
            for (int k = 0; k < m.R; k++) { D.Re[k + k * m.R] = lam[k].Real; if (D.Im != null) D.Im[k + k * m.R] = lam[k].Imaginary; }
            return Out(V, D);
        });
        Reg("svd", (it, a, n) =>
        {
            var m = M(a[0], "svd"); var A = Ops.ToMathNet(m);
            var s = A.Svd(true);
            if (n <= 1) return Out(MatrixValue.Col(s.S.ToArray()));
            var S = new MatrixValue(m.R, m.C);
            for (int k = 0; k < s.S.Count; k++) S.Re[k + k * m.R] = s.S[k];
            return Out(Ops.FromMathNet(s.U), S, Ops.FromMathNet(s.VT.Transpose()));
        });
        Reg("lu", (it, a, n) =>
        {
            var m = Square(a[0], "lu"); var lu = Ops.ToMathNet(m).LU();
            var L = lu.L; var U = lu.U; var P = Matrix<double>.Build.Dense(m.R, m.R);
            var piv = lu.P;
            for (int i = 0; i < m.R; i++) P[piv[i], i] = 1;
            if (n <= 2) return Out(Ops.FromMathNet(P.Transpose() * L), Ops.FromMathNet(U));
            return Out(Ops.FromMathNet(L), Ops.FromMathNet(U), Ops.FromMathNet(P));
        });
        Reg("qr", (it, a, n) =>
        {
            var qr = Ops.ToMathNet(M(a[0], "qr")).QR();
            if (n <= 1) return Out(Ops.FromMathNet(qr.R));
            return Out(Ops.FromMathNet(qr.Q), Ops.FromMathNet(qr.R));
        });
        Reg1("chol", a =>
        {
            var m = Square(a[0], "chol");
            try { return Ops.FromMathNet(Ops.ToMathNet(m).Cholesky().Factor.Transpose()); }
            catch (Exception) { throw new OctaveException("chol: input matrix must be positive definite"); }
        });
        Reg1("expm", a =>
        {
            var m = Square(a[0], "expm"); var A = Ops.ToMathNet(m);
            // scaling and squaring with Taylor series
            double nrm = A.InfinityNorm(); int sq = Math.Max(0, (int)Math.Ceiling(Math.Log2(Math.Max(nrm, 1e-300))) + 1);
            var X = A / Math.Pow(2, sq); var R = Matrix<double>.Build.DenseIdentity(m.R); var term = R.Clone();
            for (int k = 1; k < 30; k++) { term = term * X / k; R += term; }
            for (int k = 0; k < sq; k++) R = R * R;
            return Ops.FromMathNet(R);
        });
        Reg1("null", a =>
        {
            var A = Ops.ToMathNet(M(a[0], "null")); var s = A.Svd(true); int rank = A.Rank();
            var V = s.VT.Transpose();
            return Ops.FromMathNet(V.SubMatrix(0, V.RowCount, rank, V.ColumnCount - rank));
        });

        // polynomials
        Reg1("polyval", a =>
        {
            var p = M(a[0], "polyval"); var x = M(a[1], "polyval");
            return Ops.Map(x, xv => { double s = 0; foreach (var c in p.Re) s = s * xv + c; return s; },
                z => { Complex s = 0; for (int k = 0; k < p.Numel; k++) s = s * z + p.At(k); return s; });
        });
        Reg1("polyfit", a =>
        {
            var x = M(a[0], "polyfit"); var y = M(a[1], "polyfit"); int n = I(a[2], "polyfit");
            var V = Matrix<double>.Build.Dense(x.Numel, n + 1, (i, j) => Math.Pow(x.Re[i], n - j));
            var p = V.QR().Solve(Vector<double>.Build.Dense(y.Re));
            return MatrixValue.Row(p.ToArray());
        });
        Reg1("roots", a =>
        {
            var p = M(a[0], "roots").Re.SkipWhile(c => c == 0).ToList();
            int trailing = 0; while (p.Count > 0 && p[^1] == 0) { p.RemoveAt(p.Count - 1); trailing++; }
            int n = p.Count - 1;
            var res = new List<Complex>();
            if (n >= 1)
            {
                var C = Matrix<double>.Build.Dense(n, n);
                for (int j = 0; j < n; j++) C[0, j] = -p[j + 1] / p[0];
                for (int i = 1; i < n; i++) C[i, i - 1] = 1;
                res.AddRange(C.Evd().EigenValues);
            }
            for (int k = 0; k < trailing; k++) res.Add(0);
            if (res.Count == 0) return new MatrixValue(0, 0);
            return FromC(Vector<Complex>.Build.Dense(res.ToArray()));
        });
        Reg1("poly", a =>
        {
            var m = M(a[0], "poly");
            IEnumerable<Complex> r = (m.R == m.C && m.R > 1) ? Ops.ToMathNet(m).Evd().EigenValues : Enumerable.Range(0, m.Numel).Select(m.At);
            var c = new List<Complex> { 1 };
            foreach (var z in r) { var nc = new List<Complex>(c) { 0 }; for (int k = 1; k < nc.Count; k++) nc[k] -= z * c[k - 1]; c = nc; }
            var res = new MatrixValue(1, c.Count, NumKind.Double, true);
            for (int k = 0; k < c.Count; k++) { res.Re[k] = c[k].Real; res.Im![k] = Math.Abs(c[k].Imaginary) < 1e-12 ? 0 : c[k].Imaginary; }
            return res.Narrow();
        });
        Reg1("conv", a =>
        {
            var x = M(a[0], "conv"); var y = M(a[1], "conv");
            if (x.Numel == 0 || y.Numel == 0) return new MatrixValue(0, 0);
            var r = new double[x.Numel + y.Numel - 1];
            for (int i = 0; i < x.Numel; i++) for (int j = 0; j < y.Numel; j++) r[i + j] += x.Re[i] * y.Re[j];
            return (x.C == 1 && x.R > 1) ? MatrixValue.Col(r) : MatrixValue.Row(r);
        });
        Reg("deconv", (it, a, n) =>
        {
            var y = M(a[0], "deconv").Re.ToArray(); var den = M(a[1], "deconv").Re;
            int nb = y.Length - den.Length + 1;
            if (nb < 1) return Out(Num(0), MatrixValue.Row(y));
            var b = new double[nb]; var r = (double[])y.Clone();
            for (int k = 0; k < nb; k++) { b[k] = r[k] / den[0]; for (int j = 0; j < den.Length; j++) r[k + j] -= b[k] * den[j]; }
            return Out(MatrixValue.Row(b), MatrixValue.Row(r));
        });
        Reg1("interp1", a =>
        {
            var x = M(a[0], "interp1"); var y = M(a[1], "interp1"); var xi = M(a[2], "interp1");
            return Ops.Map(xi, v =>
            {
                for (int k = 0; k + 1 < x.Numel; k++)
                    if (v >= x.Re[k] && v <= x.Re[k + 1]) return y.Re[k] + (y.Re[k + 1] - y.Re[k]) * (v - x.Re[k]) / (x.Re[k + 1] - x.Re[k]);
                return double.NaN;
            });
        });
    }
}
