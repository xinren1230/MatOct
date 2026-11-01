// MatRewrite - GPL-3.0-or-later
// Phase 9: native linear-algebra functions (sqrtm/logm/funm via complex Schur, cross, normest,
// normest1, condest, rref, linsolve, lscov, isdefinite, subspace, housh, planerot, vech,
// commutation/duplication matrices).  Algorithms and messages follow the Octave sources.
using System.Numerics;
using MatRewrite.Core.Runtime;
using CM = MathNet.Numerics.LinearAlgebra.Matrix<System.Numerics.Complex>;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static CM ToCM(MatrixValue m)
    {
        int r = m.R, c = m.C;
        return CM.Build.Dense(r, c, (i, j) => new Complex(m.Re[i + j * r], m.Im?[i + j * r] ?? 0));
    }
    static MatrixValue FromCMAuto(CM A, bool forceReal = false)
    {
        double nrm = 0; foreach (var z in A.Enumerate()) nrm = Math.Max(nrm, Complex.Abs(z));
        bool real = forceReal || A.Enumerate().All(z => Math.Abs(z.Imaginary) <= 1e-13 * Math.Max(nrm, 1e-300) || z.Imaginary == 0);
        return FromCM(A, real);
    }
    static CM CEye(int n) => CM.Build.DenseIdentity(n);

    // square root of an upper triangular complex matrix
    static CM SqrtTri(CM T)
    {
        int n = T.RowCount; var R = CM.Build.Dense(n, n);
        for (int j = 0; j < n; j++)
        {
            R[j, j] = Complex.Sqrt(T[j, j]);
            for (int i = j - 1; i >= 0; i--)
            {
                Complex s = 0; for (int k = i + 1; k < j; k++) s += R[i, k] * R[k, j];
                var den = R[i, i] + R[j, j];
                R[i, j] = den == Complex.Zero ? (T[i, j] - s == Complex.Zero ? 0 : new Complex(double.PositiveInfinity, 0)) : (T[i, j] - s) / den;
            }
        }
        return R;
    }

    static bool IsUpperTri(CM T, double tol) { for (int j = 0; j < T.ColumnCount; j++) for (int i = j + 1; i < T.RowCount; i++) if (Complex.Abs(T[i, j]) > tol) return false; return true; }
    static bool IsDiagCM(CM T) { for (int j = 0; j < T.ColumnCount; j++) for (int i = 0; i < T.RowCount; i++) if (i != j && T[i, j] != Complex.Zero) return false; return true; }

    static (CM U, CM T) SchurOf(CM A)
    {
        if (IsUpperTri(A, 0)) return (CEye(A.RowCount), A.Clone());
        return ComplexSchur(A);
    }

    static MatrixValue SqrtmCore(Interpreter it, MatrixValue m)
    {
        var A = ToCM(m); int n = A.RowCount;
        var (U, T) = SchurOf(A);
        var R = SqrtTri(T);
        bool sing = Enumerable.Range(0, n).Any(i => T[i, i] == Complex.Zero);
        if (sing) it.Warning("sqrtm: matrix is singular, may not have a square root", "Octave:sqrtm:SingularMatrix");
        var X = U * R * U.ConjugateTranspose();
        bool realOk = m.Im == null && Enumerable.Range(0, n).All(i => !(T[i, i].Real < 0 && Math.Abs(T[i, i].Imaginary) < 1e-14 * (1 + Complex.Abs(T[i, i]))));
        var r = FromCMAuto(X, realOk);
        if (m.Kind == NumKind.Single) r.CastInPlace(NumKind.Single);
        return r;
    }

    static (CM S, int iters) LogmCore(Interpreter it, CM A, int optIters, bool isReal, out bool realEig)
    {
        int n = A.RowCount;
        var (u, s) = SchurOf(A);
        var eigv = Enumerable.Range(0, n).Select(i => s[i, i]).ToArray();
        double mx = eigv.Max(Complex.Abs);
        double tol = n * (mx == 0 ? double.Epsilon : Math.BitIncrement(mx) - mx);
        var neg = eigv.Select(e => e.Real < -tol && e.Imaginary <= tol).ToArray();
        if (neg.Any(b => b)) it.Warning("logm: principal matrix logarithm is not defined for matrices with negative eigenvalues; computing non-principal logarithm", "Octave:logm:non-principal");
        realEig = !neg.Any(b => b);
        double offmax = 0; for (int j = 0; j < n; j++) for (int i = 0; i < j; i++) offmax = Math.Max(offmax, Complex.Abs(s[i, j]));
        if (offmax < tol)
        {
            var d = CM.Build.Dense(n, n);
            for (int i = 0; i < n; i++) { var l = Complex.Log(eigv[i]); if (double.IsInfinity(l.Real)) l = -Math.Log(double.MaxValue); d[i, i] = l; }
            return (u * d * u.ConjugateTranspose(), 0);
        }
        double[] theta = { 0, 0, 1.61e-2, 5.38e-2, 1.13e-1, 1.86e-1, 2.6429608311114350e-1 };
        int k = 0, p = 0, m = 7;
        while (k < optIters)
        {
            double tau = (s - CEye(n)).L1Norm();
            if (tau <= theta[6])
            {
                p++;
                int j1 = Array.FindIndex(theta, t => tau <= t) + 1, j2 = Array.FindIndex(theta, t => tau / 2 <= t) + 1;
                if (j1 - j2 <= 1 || p == 2) { m = j1; break; }
            }
            k++; s = SqrtTri(s);
        }
        if (k >= optIters) it.Warning("logm: maximum number of square roots exceeded; results may still be accurate");
        s = s - CEye(n);
        if (m > 1)
        {
            // Gauss-Legendre nodes on [0,1]
            var J = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(m, m);
            for (int i = 1; i < m; i++) { double v = i / Math.Sqrt(4.0 * i * i - 1); J[i, i - 1] = v; J[i - 1, i] = v; }
            var evd = J.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);
            var acc = CM.Build.Dense(n, n);
            for (int j = 0; j < m; j++)
            {
                double node = (evd.EigenValues[j].Real + 1) / 2, w = evd.EigenVectors[0, j] * evd.EigenVectors[0, j];
                acc += w * (s * (CEye(n) + node * s).Inverse());
            }
            s = acc;
        }
        return (Math.Pow(2, k) * u * s * u.ConjugateTranspose(), k);
    }

    static void RegisterPhase9Linalg()
    {
        Reg("sqrtm", (it, a, n) =>
        {
            if (a.Length != 1) Usage("sqrtm");
            var A = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (A is not MatrixValue m || m.IsChar && false) throw new OctaveException("sqrtm: A must be a square matrix");
            if (m.Dims.Length > 2 || m.R != m.C) throw new OctaveException("sqrtm: A must be a square matrix");
            if (m.Numel == 0) return Out(MatrixValue.Empty());
            if (m.Kind is not (NumKind.Double or NumKind.Single)) m = (MatrixValue)Fc(it, "double", m);
            return Out(SqrtmCore(it, m));
        });
        Reg("logm", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("logm");
            var A0 = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            var m = M(A0, "logm");
            if (m.Dims.Length > 2 || m.R != m.C) throw new OctaveException("logm: A must be a square matrix");
            if (m.Numel == 1) return Out(Fc(it, "log", m));
            if (m.Numel == 0) return Out(MatrixValue.Empty(), Num(0));
            int optIters = a.Length > 1 ? I(a[1], "logm") : 100;
            var A = ToCM(m);
            if (IsDiagCM(A))
            {
                var d = CM.Build.Dense(m.R, m.R); for (int i = 0; i < m.R; i++) d[i, i] = Complex.Log(A[i, i]);
                return Out(FromCMAuto(d, m.Im == null && Enumerable.Range(0, m.R).All(i => A[i, i].Real >= 0)), Num(0));
            }
            var (S, k) = LogmCore(it, A, optIters, m.Im == null, out bool realEig);
            var r = FromCMAuto(S, realEig && m.Im == null);
            return Out(r, Num(k));
        });
        Reg("funm", (it, a, n) =>
        {
            if (a.Length < 2) Usage("funm");
            var A0 = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (A0 is not MatrixValue m || m.IsChar || m.Kind == NumKind.Logical || m.Dims.Length > 2 || m.R != m.C) throw new OctaveException("funm: A must be a numeric square matrix");
            if (!(a[1] is FunctionHandleValue || IsString(a[1]))) throw new OctaveException("funm: FUN must be a function handle or function name");
            if (a.Length > 2 && !(a[2] is StructValue sv && sv.Numel == 1)) throw new OctaveException("funm: OPTIONS must be a scalar struct");
            bool single = m.Kind == NumKind.Single;
            if (m.Kind != NumKind.Double) m = (MatrixValue)Fc(it, "double", m);
            string fname = a[1] is FunctionHandleValue fh ? (fh.Name ?? "") : ((MatrixValue)a[1]).AsString();
            int N = m.R;
            var A = ToCM(m);
            CM F;
            CM Expm(CM X)
            {
                double nrm = X.InfinityNorm(); int sq = Math.Max(0, (int)Math.Ceiling(Math.Log2(Math.Max(nrm, 1e-300))) + 1);
                var Y = X / Math.Pow(2, sq); var R = CEye(N); var term = CEye(N);
                for (int k = 1; k < 40; k++) { term = term * Y / k; R += term; }
                for (int k = 0; k < sq; k++) R = R * R;
                return R;
            }
            var I_ = Complex.ImaginaryOne;
            switch (fname)
            {
                case "exp": case "expm": F = Expm(A); break;
                case "sin": F = (Expm(I_ * A) - Expm(-I_ * A)) / (2 * I_); break;
                case "cos": F = (Expm(I_ * A) + Expm(-I_ * A)) / 2; break;
                case "sinh": F = (Expm(A) - Expm(-A)) / 2; break;
                case "cosh": F = (Expm(A) + Expm(-A)) / 2; break;
                case "log": case "logm":
                    if (N == 1) { F = CM.Build.Dense(1, 1, Complex.Log(A[0, 0])); break; }
                    F = LogmCore(it, A, 100, m.Im == null, out _).S; break;
                case "sqrt": case "sqrtm": { var (U, T) = SchurOf(A); F = U * SqrtTri(T) * U.ConjugateTranspose(); break; }
                default:
                {
                    // Schur-Parlett recurrence (distinct eigenvalues)
                    var (U, T) = SchurOf(A);
                    var diag = new MatrixValue(N, 1, NumKind.Double, true);
                    for (int i = 0; i < N; i++) { diag.Re[i] = T[i, i].Real; diag.Im![i] = T[i, i].Imaginary; }
                    var fd = M(it.CallCallable(a[1], new Value[] { diag }, 1)[0], "funm");
                    var Ft = CM.Build.Dense(N, N);
                    for (int i = 0; i < N; i++) Ft[i, i] = new Complex(fd.Re[i], fd.Im?[i] ?? 0);
                    for (int p = 1; p < N; p++)
                        for (int i = 0; i + p < N; i++)
                        {
                            int j = i + p; Complex s = T[i, j] * (Ft[j, j] - Ft[i, i]);
                            for (int k = i + 1; k < j; k++) s += T[i, k] * Ft[k, j] - Ft[i, k] * T[k, j];
                            var den = T[j, j] - T[i, i];
                            Ft[i, j] = den == Complex.Zero ? 0 : s / den;
                        }
                    F = U * Ft * U.ConjugateTranspose();
                    break;
                }
            }
            bool realIn = m.Im == null;
            bool forceReal = realIn && fname is "exp" or "expm" or "sin" or "cos" or "sinh" or "cosh";
            var r = FromCMAuto(F, forceReal);
            if (single) r.CastInPlace(NumKind.Single);
            var outp = new StructValue();
            outp.Set("terms", Num(1)); outp.Set("ind", new CellValue(1, 1, new Value[] { RangeV(1, N) })); outp.Set("ord", RowD(Enumerable.Repeat(1.0, N))); outp.Set("T", FromCMAuto(SchurOf(A).T));
            return Out(r, Num(0), outp);
        });

        Reg("cross", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 3) Usage("cross");
            Value x = a[0], y = a[1];
            int nd = SizeOf(x).Length;
            if (a.Length < 3 && nd < 3 && SizeOf(y).Length < 3)
            {
                var sx = SizeOf(x); var sy = SizeOf(y);
                if (sx[1] == 1 && sy[0] == 1) { it.Warning("cross: cross product of column by row produces row output"); x = Fc(it, "transpose", x); }
                else if (sx[0] == 1 && sy[1] == 1) { it.Warning("cross: cross product of row by column produces row output"); y = Fc(it, "transpose", y); }
            }
            var sz = SizeOf(x); int dim;
            if (a.Length == 2)
            {
                dim = Array.IndexOf(sz, 3) + 1;
                if (dim == 0) throw new OctaveException("cross: must have at least one dimension with 3 elements");
            }
            else
            {
                var dv = a[2];
                if (!(IsNum(dv) && !IsCplxV(dv) && IsScal(dv) && D(dv, "cross") > 0 && D(dv, "cross") == Math.Floor(D(dv, "cross"))))
                    throw new OctaveException("cross: DIM must be a positive scalar whole number");
                dim = (int)D(dv, "cross");
                var syd = SizeOf(y);
                if (dim > nd || sz[dim - 1] != 3 || dim > syd.Length || syd[dim - 1] != 3) throw new OctaveException("cross: X and Y must have three elements in dimension DIM");
            }
            if (!SizeOf(x).SequenceEqual(SizeOf(y))) throw new OctaveException("cross: X and Y must have the same dimensions");
            Value Pick(Value v, int k) { var idx = Enumerable.Range(0, nd).Select(q => q == dim - 1 ? Dn(k) : Colon).ToArray(); return IndexAny(it, v, idx); }
            Value x1 = Pick(x, 1), x2 = Pick(x, 2), x3 = Pick(x, 3), y1 = Pick(y, 1), y2 = Pick(y, 2), y3 = Pick(y, 3);
            Value Sub(Value p, Value q, Value r, Value s) => Bx("-", Bx(".*", p, q), Bx(".*", r, s));
            return Out(Fc(it, "cat", Dn(dim), Sub(x2, y3, x3, y2), Sub(x3, y1, x1, y3), Sub(x1, y2, x2, y1)));
        });

        Reg("normest", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("normest");
            var A = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (!IsNum(A) || SizeOf(A).Length != 2) throw new OctaveException("normest: A must be a numeric 2-D matrix");
            double tol = 1e-6;
            if (a.Length > 1) { if (!(IsScal(a[1]) && !IsCplxV(a[1]) && a[1] is MatrixValue)) throw new OctaveException("normest: TOL must be a real scalar"); tol = D(a[1], "normest"); }
            var m = (MatrixValue)A; bool single = m.Kind == NumKind.Single;
            tol = Math.Max(tol, single ? 1.1920929e-07 : 2.220446049250313e-16);
            var C = ToCM(m); int nc = m.C;
            var rnd = new Random(12345);
            var yv = MathNet.Numerics.LinearAlgebra.Vector<Complex>.Build.Dense(nc, _ => rnd.NextDouble());
            int iter = 0; double nest = 0, n0;
            do
            {
                n0 = nest;
                var xv = C * yv; double nx = xv.L2Norm();
                xv = nx == 0 ? MathNet.Numerics.LinearAlgebra.Vector<Complex>.Build.Dense(nc, _ => rnd.NextDouble()) : xv / nx;
                yv = C.ConjugateTranspose() * xv; nest = yv.L2Norm(); iter++;
            } while (Math.Abs(nest - n0) > tol * nest && iter < 10000);
            Value r = Num(nest); if (single) ((MatrixValue)r).CastInPlace(NumKind.Single);
            return Out(r, Num(iter));
        });
        Reg("normest1", (it, a, n) =>
        {
            if (a.Length < 1) Usage("normest1");
            var A = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (A is FunctionHandleValue) throw new OctaveException("normest1: function handle input is not supported natively");
            if (A is not MatrixValue m || m.Dims.Length > 2 || m.R != m.C) throw new OctaveException("normest1: A must be a square matrix or a function handle");
            if (m.Kind != NumKind.Double && m.Kind != NumKind.Single) m = (MatrixValue)Fc(it, "double", m);
            int N = m.R; double best = 0; int bj = 0;
            for (int j = 0; j < N; j++) { double s = 0; for (int i = 0; i < N; i++) s += Complex.Abs(new Complex(m.Re[i + j * N], m.Im?[i + j * N] ?? 0)); if (s > best) { best = s; bj = j; } }
            var v = new MatrixValue(N, 1); if (N > 0) v.Re[bj] = 1;
            var w = (MatrixValue)Fc(it, "mtimes", m, v);
            return Out(Num(best), v, w, Num(2));
        });
        Reg("condest", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 6) Usage("condest");
            var A = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (A is not MatrixValue m) throw new OctaveException("condest: first argument must be a square matrix or function handle");
            if (m.R != m.C) throw new OctaveException("condest: A must be square");
            double n1 = D(Fc(it, "norm", m, Dn(1)), "condest");
            Value inv;
            try { inv = Fc(it, "inv", m); } catch { return Out(Num(double.PositiveInfinity)); }
            var im = (MatrixValue)inv;
            if (im.Re.Any(double.IsInfinity) || im.Re.Any(double.IsNaN)) return Out(Num(double.PositiveInfinity));
            double n2 = D(Fc(it, "norm", inv, Dn(1)), "condest");
            int N = m.R; double best = 0; int bj = 0;
            for (int j = 0; j < N; j++) { double s = 0; for (int i = 0; i < N; i++) s += Math.Abs(im.Re[i + j * N]); if (s > best) { best = s; bj = j; } }
            var v = new MatrixValue(N, 1); if (N > 0) v.Re[bj] = 1;
            v = (MatrixValue)Bx("/", Fc(it, "mtimes", inv, v), Dn(Math.Max(n2, 1e-300)));
            return Out(Num(n1 * n2), v);
        });
        Reg("rref", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("rref");
            var m = Copy(M(a[0], "rref"));
            if (m.Dims.Length > 2) throw new OctaveException("rref: A must be a 2-dimensional matrix");
            if (m.Kind != NumKind.Double && m.Kind != NumKind.Single) m = Copy((MatrixValue)Fc(it, "double", m));
            int R = m.R, C = m.C;
            var A = ToCM(m);
            double tol;
            if (a.Length > 1) tol = D(a[1], "rref");
            else { double ninf = A.InfinityNorm(); tol = (m.Kind == NumKind.Single ? 1.1920929e-07 : 2.220446049250313e-16) * Math.Max(R, C) * ninf; }
            var used = new List<double>(); int r = 0;
            for (int c = 0; c < C && R > 0; c++)
            {
                int piv = r; double mx = -1;
                for (int i = r; i < R; i++) { double v = Complex.Abs(A[i, c]); if (v > mx) { mx = v; piv = i; } }
                if (mx <= tol) { for (int i = r; i < R; i++) A[i, c] = 0; }
                else
                {
                    used.Add(c + 1);
                    for (int j = c; j < C; j++) (A[piv, j], A[r, j]) = (A[r, j], A[piv, j]);
                    var p = A[r, c]; for (int j = c; j < C; j++) A[r, j] /= p;
                    for (int i = 0; i < R; i++) { if (i == r) continue; var f = A[i, c]; if (f == Complex.Zero) continue; for (int j = c; j < C; j++) A[i, j] -= f * A[r, j]; }
                    if (r++ == R - 1) break;
                }
            }
            var res = FromCM(A, m.Im == null);
            if (m.Kind == NumKind.Single) res.CastInPlace(NumKind.Single);
            return Out(res, RowD(used));
        });
        Reg("linsolve", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 3) Usage("linsolve");
            if (!(IsNumL(a[0]) && IsNumL(a[1])) || a[0] is MatrixValue c0 && c0.IsChar || a[1] is MatrixValue c1 && c1.IsChar) throw new OctaveException("linsolve: A and B must be numeric");
            Value A = a[0]; bool trans = false;
            if (a.Length > 2)
            {
                if (a[2] is not StructValue o) throw new OctaveException("linsolve: OPTS must be a structure");
                if (o.Fields.Contains("TRANSA") && Tr(o.Elems[0]["TRANSA"])) trans = true;
            }
            var x = trans ? Bx("\\", Fc(it, "ctranspose", A), a[1]) : Bx("\\", A, a[1]);
            if (n > 1)
            {
                var sz = SizeOf(A);
                return Out(x, sz[0] == sz[1] ? Fc(it, "rcond", A is Sparse.SparseValue sp ? sp.ToDense() : A) : Fc(it, "rank", A is Sparse.SparseValue sp2 ? sp2.ToDense() : A));
            }
            return Out(x);
        });
        Reg("isdefinite", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("isdefinite");
            var A = a[0] is Sparse.SparseValue sp ? sp.ToDense() : a[0];
            if (!IsNum(A)) return Out(Bool(false));
            var m = (MatrixValue)A; if (m.Kind != NumKind.Double && m.Kind != NumKind.Single) m = (MatrixValue)Fc(it, "double", m);
            double tol;
            if (a.Length == 1) { double fro = D(Fc(it, "norm", m, Str("fro")), "isdefinite"); double ep = m.Kind == NumKind.Single ? 1.1920929e-07 : 2.220446049250313e-16; tol = 100 * ep * fro; }
            else { if (!(IsNum(a[1]) && IsScal(a[1]) && D(a[1], "isdefinite") >= 0)) throw new OctaveException("isdefinite: TOL must be a scalar >= 0"); tol = D(a[1], "isdefinite"); }
            if (m.R != m.C) return Out(Bool(false));
            if (!Tr(Fc(it, "ishermitian", m, Dn(tol)))) return Out(Bool(false));
            var e = Bx("-", m, Bx("*", Dn(tol), Fc(it, "eye", Dn(m.R))));
            var r = Fn(it, "chol", 2, e);
            return Out(Bool(r.Length > 1 && D(r[1], "isdefinite") == 0));
        });
        Reg("subspace", (it, a, n) =>
        {
            if (a.Length != 2) Usage("subspace");
            if (SizeOf(a[0]).Length != 2 || SizeOf(a[1]).Length != 2) throw new OctaveException("subspace: A and B must be 2-dimensional arrays");
            if (SizeOf(a[0])[0] != SizeOf(a[1])[0]) throw new OctaveException("subspace: column dimensions of A and B must match");
            var A = Fc(it, "orth", a[0]); var B = Fc(it, "orth", a[1]);
            var c = Bx("*", Fc(it, "ctranspose", A), B);
            double scos = D(Fc(it, "min", Fc(it, "svd", c)), "subspace");
            if (scos * scos > 0.5)
            {
                c = SizeOf(A)[1] >= SizeOf(B)[1] ? Bx("-", B, Bx("*", A, c)) : Bx("-", A, Bx("*", B, Fc(it, "ctranspose", c)));
                double ssin = D(Fc(it, "max", Fc(it, "svd", c)), "subspace");
                return Out(Num(Math.Asin(Math.Min(ssin, 1))));
            }
            return Out(Num(Math.Acos(scos)));
        });
        Reg("housh", (it, a, n) =>
        {
            if (a.Length != 3) Usage("housh");
            if (!IsVec(a[0]) && !IsScal(a[0])) throw new OctaveException("housh: first input must be a vector");
            if (!IsScal(a[1])) throw new OctaveException("housh: second argument must be an integer scalar");
            var x = Copy(M(a[0], "housh")); int j = I(a[1], "housh") - 1; double z = D(a[2], "housh");
            double mx = 0; for (int k = 0; k < x.Numel; k++) mx = Math.Max(mx, Complex.Abs(x.At(k)));
            double beta = 0;
            if (mx != 0)
            {
                for (int k = 0; k < x.Numel; k++) { x.Re[k] /= mx; if (x.Im != null) x.Im[k] /= mx; }
                double alpha = Math.Sqrt(Enumerable.Range(0, x.Numel).Sum(k => Math.Pow(Complex.Abs(x.At(k)), 2)));
                if (alpha > z)
                {
                    var hj = x.At(j);
                    beta = 1.0 / (alpha * (alpha + Complex.Abs(hj)));
                    Complex sg = hj == Complex.Zero ? 1 : hj / Complex.Abs(hj);
                    var nv = hj + alpha * sg; x.Re[j] = nv.Real; if (x.Im != null) x.Im[j] = nv.Imaginary;
                }
            }
            return Out(x, Num(beta), Num(beta == 0 ? 1 : 0));
        });
        Reg("planerot", (it, a, n) =>
        {
            if (a.Length < 1) Usage("planerot");
            if (!(IsVec(a[0]) && NumelOf(a[0]) == 2)) throw new OctaveException("planerot: X must be a 2-element vector");
            var x = M(a[0], "planerot");
            var G = Fc(it, "givens", Dn(x.Re[0]), Dn(x.Re[1]));
            return Out(G, Bx("*", G, Fc(it, "reshape", x, Dn(2), Dn(1))));
        });
        Reg("vech", (it, a, n) =>
        {
            if (a.Length != 1) Usage("vech");
            var m = M(a[0], "vech");
            if (m.R != m.C) throw new OctaveException("vech: X must be square");
            var idx = new List<double>(); for (int j = 0; j < m.C; j++) for (int i = j; i < m.R; i++) idx.Add(i + j * m.R + 1);
            return Out(IndexAny(it, m, new Value[] { new MatrixValue(idx.Count, 1, idx.ToArray(), null) }));
        });
        Reg("commutation_matrix", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 2) Usage("commutation_matrix");
            bool PosInt(Value v) => IsScal(v) && IsNum(v) && D(v, "x") > 0 && D(v, "x") == Math.Floor(D(v, "x"));
            if (!PosInt(a[0])) throw new OctaveException("commutation_matrix: M must be a positive integer");
            int M_ = I(a[0], "c"), N_ = M_;
            if (a.Length > 1) { if (!PosInt(a[1])) throw new OctaveException("commutation_matrix: N must be a positive integer"); N_ = I(a[1], "c"); }
            int mn = M_ * N_; var r = new MatrixValue(mn, mn);
            for (int i = 0; i < M_; i++) for (int j = 0; j < N_; j++) r.Re[(i * N_ + j) + mn * (j * M_ + i)] = 1;
            return Out(r);
        });
        Reg("duplication_matrix", (it, a, n) =>
        {
            if (a.Length != 1) Usage("duplication_matrix");
            var v = a[0];
            if (!(IsScal(v) && IsNum(v) && D(v, "d") > 0 && D(v, "d") == Math.Floor(D(v, "d")))) throw new OctaveException("duplication_matrix: N must be a positive integer");
            int N = I(v, "d"); int rows = N * N, cols = N * (N + 1) / 2; var r = new MatrixValue(rows, cols);
            int count = 0;
            for (int j = 0; j < N; j++)
            {
                r.Re[(j * N + j) + rows * count] = 1;
                for (int i = j + 1; i < N; i++) { r.Re[(j * N + i) + rows * (count + i - j)] = 1; r.Re[(i * N + j) + rows * (count + i - j)] = 1; }
                count += N - j;
            }
            return Out(r);
        });
        Reg("lscov", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 4) Usage("lscov");
            Value A = a[0], b = a[1];
            int nr = SizeOf(A)[0];
            if (SizeOf(b)[0] != nr) throw new OctaveException("lscov: A and B must have the same number of rows");
            if (a.Length > 2 && NumelOf(a[2]) > 0)
            {
                var V = a[2];
                if (IsVec(V) || IsScal(V))
                {
                    if (NumelOf(V) != nr) throw new OctaveException("lscov: vector V must have n (number of row in A) elements ");
                    var w = Fc(it, "sqrt", Fc(it, "reshape", V, Dn(nr), Dn(1)));
                    A = Bx(".*", A, w); b = Bx(".*", b, w);
                }
                else
                {
                    var sv = SizeOf(V);
                    if (sv[0] != sv[1] || sv[0] != nr) throw new OctaveException("lscov: matrix V must be square with the same number of rows as A");
                    var L = Fc(it, "transpose", Fc(it, "chol", V));
                    A = Bx("\\", L, A); b = Bx("\\", L, b);
                }
            }
            var x = Bx("\\", A, b);
            if (n <= 1) return Out(x);
            int p = SizeOf(A)[1];
            var res = Bx("-", b, Bx("*", A, x));
            var mse = Bx("/", Fc(it, "sumsq", res), Dn(Math.Max(nr - p, 1)));
            var Ainv = Fc(it, "inv", Bx("*", Fc(it, "ctranspose", A), A));
            var stdx = Bx("*", Fc(it, "sqrt", Fc(it, "diag", Ainv)), Fc(it, "sqrt", mse));
            if (n <= 3) return Out(x, stdx, mse);
            return Out(x, stdx, mse, Bx("*", Ainv, Dn(D(mse, "lscov"))));
        });
    }
}
