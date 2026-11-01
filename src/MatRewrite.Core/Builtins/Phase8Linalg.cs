// MatRewrite - GPL-3.0-or-later
// QR (LAPACK-style Householder, optional column pivoting), qr/chol update family, cholinv.
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    sealed class CMat
    {
        public int R, C; public Complex[] A; public bool Cx;
        public CMat(int r, int c, bool cx) { R = r; C = c; A = new Complex[r * c]; Cx = cx; }
        public Complex this[int i, int j] { get => A[i + R * j]; set => A[i + R * j] = value; }
        public static CMat From(MatrixValue m)
        {
            if (m.Dims.Length > 2) throw new OctaveException("operation not defined for N-D arrays");
            var c = new CMat(m.R, m.C, m.Im != null);
            for (int k = 0; k < m.Numel; k++) c.A[k] = new Complex(m.Re[k], m.Im?[k] ?? 0);
            return c;
        }
        public MatrixValue ToMV(bool single = false)
        {
            var re = A.Select(z => z.Real).ToArray(); double[]? im = Cx ? A.Select(z => z.Imaginary).ToArray() : null;
            var m = new MatrixValue(R, C, re, im);
            if (single) m.CastInPlace(NumKind.Single);
            return m;
        }
        public CMat Mul(CMat b) { var r = new CMat(R, b.C, Cx || b.Cx); for (int j = 0; j < b.C; j++) for (int k = 0; k < C; k++) { var v = b[k, j]; if (v == Complex.Zero) continue; for (int i = 0; i < R; i++) r.A[i + R * j] += A[i + R * k] * v; } return r; }
        public CMat CT() { var r = new CMat(C, R, Cx); for (int i = 0; i < R; i++) for (int j = 0; j < C; j++) r[j, i] = Complex.Conjugate(this[i, j]); return r; }
        public CMat Cols(int[] idx) { var r = new CMat(R, idx.Length, Cx); for (int j = 0; j < idx.Length; j++) for (int i = 0; i < R; i++) r[i, j] = this[i, idx[j]]; return r; }
        public CMat Rows(int[] idx) { var r = new CMat(idx.Length, C, Cx); for (int j = 0; j < C; j++) for (int i = 0; i < idx.Length; i++) r[i, j] = this[idx[i], j]; return r; }
    }

    // Householder QR. Returns Q (m x qc), R (rr x n), perm (0-based) or null
    static (CMat Q, CMat R, int[] perm) HouseQR(CMat A0, bool econ, bool pivot, bool wantQ)
    {
        int m = A0.R, n = A0.C, k = Math.Min(m, n);
        var A = new CMat(m, n, A0.Cx); Array.Copy(A0.A, A.A, A.A.Length);
        var perm = Enumerable.Range(0, n).ToArray();
        var taus = new Complex[k]; var vs = new Complex[k][];
        for (int j = 0; j < k; j++)
        {
            if (pivot)
            {
                int best = j; double bn = -1;
                for (int c = j; c < n; c++) { double s = 0; for (int i = j; i < m; i++) s += (A[i, c] * Complex.Conjugate(A[i, c])).Real; if (s > bn) { bn = s; best = c; } }
                if (best != j) { for (int i = 0; i < m; i++) (A[i, j], A[i, best]) = (A[i, best], A[i, j]); (perm[j], perm[best]) = (perm[best], perm[j]); }
            }
            Complex alpha = A[j, j]; double xn = 0;
            for (int i = j + 1; i < m; i++) xn += (A[i, j] * Complex.Conjugate(A[i, j])).Real;
            xn = Math.Sqrt(xn);
            var v = new Complex[m - j]; v[0] = 1;
            Complex tau = 0;
            if (xn == 0 && alpha.Imaginary == 0) { tau = 0; }
            else
            {
                double nrm = Math.Sqrt(alpha.Real * alpha.Real + alpha.Imaginary * alpha.Imaginary + xn * xn);
                double beta = -(alpha.Real >= 0 ? 1 : -1) * nrm;
                tau = new Complex((beta - alpha.Real) / beta, -alpha.Imaginary / beta);
                var sc = 1.0 / (alpha - beta);
                for (int i = j + 1; i < m; i++) v[i - j] = A[i, j] * sc;
                A[j, j] = beta; for (int i = j + 1; i < m; i++) A[i, j] = 0;
                // apply H^H = I - conj(tau) v v^H to remaining columns
                var ct = Complex.Conjugate(tau);
                for (int c = j + 1; c < n; c++)
                {
                    Complex s = 0; for (int i = j; i < m; i++) s += Complex.Conjugate(v[i - j]) * A[i, c];
                    s *= ct; for (int i = j; i < m; i++) A[i, c] -= v[i - j] * s;
                }
            }
            taus[j] = tau; vs[j] = v;
        }
        int rr = econ ? k : m;
        var R = new CMat(rr, n, A0.Cx);
        for (int j = 0; j < n; j++) for (int i = 0; i < Math.Min(rr, Math.Min(j + 1, m)); i++) R[i, j] = A[i, j];
        CMat Q = null!;
        if (wantQ)
        {
            int qc = econ ? k : m;
            Q = new CMat(m, qc, A0.Cx); for (int i = 0; i < Math.Min(m, qc); i++) Q[i, i] = 1;
            for (int j = k - 1; j >= 0; j--)
            {
                var v = vs[j]; var tau = taus[j]; if (tau == Complex.Zero) continue;
                for (int c = 0; c < qc; c++)
                {
                    Complex s = 0; for (int i = j; i < m; i++) s += Complex.Conjugate(v[i - j]) * Q[i, c];
                    s *= tau; for (int i = j; i < m; i++) Q[i, c] -= v[i - j] * s;
                }
            }
        }
        return (Q, R, pivot ? perm : null!);
    }

    static CMat CholC(CMat A, string fn, out int info)
    {
        int n = A.R; var L = new CMat(n, n, A.Cx); info = 0;
        // upper R with R^H R = A
        for (int j = 0; j < n; j++)
        {
            Complex s = A[j, j]; for (int k = 0; k < j; k++) s -= Complex.Conjugate(L[k, j]) * L[k, j];
            if (s.Real <= 0 || double.IsNaN(s.Real)) { info = j + 1; return L; }
            double d = Math.Sqrt(s.Real); L[j, j] = d;
            for (int i = j + 1; i < n; i++)
            {
                Complex t = A[j, i]; for (int k = 0; k < j; k++) t -= Complex.Conjugate(L[k, j]) * L[k, i];
                L[j, i] = t / d;
            }
        }
        return L;
    }

    static MatrixValue Fl(Interpreter it, Value v, string fn)
    {
        if (v is MatrixValue m && !m.IsChar && (m.Kind == NumKind.Double || m.Kind == NumKind.Single)) return m;
        if (v is MatrixValue m2 && (m2.Kind == NumKind.Logical || NumTypes.IsInt(m2.Kind))) return M(Fc(it, "double", v), fn);
        if (v is Sparse.SparseValue) return M(Fc(it, "full", v), fn);
        throw new OctaveException($"{fn}: wrong type argument '{v.ClassName}'");
    }

    static Value[] QrOut(CMat Q, CMat R, int[] perm, bool vecPerm, int nout, bool single)
    {
        var res = new List<Value>();
        if (nout <= 1) return new Value[] { R.ToMV(single) };
        res.Add(Q.ToMV(single)); res.Add(R.ToMV(single));
        if (nout >= 3)
        {
            int n = perm.Length;
            if (vecPerm) res.Add(RowOf(perm.Select(p => (double)p + 1).ToArray()));
            else { var P = new double[n * n]; for (int j = 0; j < n; j++) P[perm[j] + n * j] = 1; res.Add(new MatrixValue(n, n, P, null)); }
        }
        return res.ToArray();
    }

    static CMat AFromQR(Interpreter it, Value q, Value r, string fn)
    {
        var Q = CMat.From(Fl(it, q, fn)); var R = CMat.From(Fl(it, r, fn));
        if (Q.C != R.R) throw new OctaveException($"{fn}: dimension mismatch");
        return Q.Mul(R);
    }

    static Value[] ReQR(CMat A, Value q0, bool single, int nout)
    {
        bool econ = SizeOf(q0)[1] < SizeOf(q0)[0] && A.C < A.R;
        var (Q, R, _) = HouseQR(A, econ, false, true);
        return new Value[] { Q.ToMV(single), R.ToMV(single) };
    }

    static int[] IdxList(Interpreter it, Value v, string fn) => Fl(it, v, fn).Re.Select(z => (int)z).ToArray();

    static void RegisterPhase8Linalg()
    {
        Reg("qr", (it, a, n) =>
        {
            if (a.Length < 1) Usage("qr");
            if (a.Length > 3) TooMany("qr");
            var am = Fl(it, a[0], "qr");
            bool single = am.IsSingle;
            bool econ = false, vecPerm = false;
            for (int k = 1; k < a.Length; k++)
            {
                if (a[k] is MatrixValue o && o.IsChar)
                {
                    var s = o.AsString();
                    if (s == "econ") { econ = true; vecPerm = true; }
                    else if (s == "vector") vecPerm = true;
                    else if (s == "matrix") vecPerm = false;
                    else if (s == "raw") { }
                    else throw new OctaveException($"qr: option string must be 'econ' or 'matrix', or 'vector', not \"{s}\"");
                }
                else if (IsScal(a[k]) && D(a[k], "qr") == 0) { econ = true; vecPerm = true; }
                else throw new OctaveException("qr: if second argument is numeric, it must be 0");
            }
            var A = CMat.From(am);
            var (Q, R, perm) = HouseQR(A, econ, n >= 3, n >= 2);
            return QrOut(Q, R, perm, vecPerm, n, single);
        });
        Reg("cholinv", (it, a, n) =>
        {
            if (a.Length != 1) Usage("cholinv");
            var A = CMat.From(Fl(it, a[0], "cholinv"));
            if (A.R != A.C) throw new OctaveException("cholinv: A must be a square matrix");
            var R = CholC(A, "cholinv", out int info);
            if (info != 0) throw new OctaveException("cholinv: A must be positive definite");
            return new[] { Fc(it, "inv", A.ToMV()) };
        });
        Reg("chol2inv", (it, a, n) =>
        {
            if (a.Length != 1) Usage("chol2inv");
            var R = CMat.From(Fl(it, a[0], "chol2inv"));
            if (R.R != R.C) throw new OctaveException("chol2inv: R must be a square matrix");
            var U = new CMat(R.R, R.C, R.Cx); for (int j = 0; j < R.C; j++) for (int i = 0; i <= j; i++) U[i, j] = R[i, j];
            return new[] { Fc(it, "inv", U.CT().Mul(U).ToMV()) };
        });
        Reg("cholupdate", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 3) Usage("cholupdate");
            var rm = Fl(it, a[0], "cholupdate"); var R = CMat.From(rm); var x = CMat.From(Fl(it, a[1], "cholupdate"));
            if (R.R != R.C || x.A.Length != R.R) throw new OctaveException("cholupdate: dimension mismatch between R and X");
            string op = a.Length == 3 ? Sx(a[2]) : "+";
            if (op != "+" && op != "-") throw new OctaveException("cholupdate: OP must be \"+\" or \"-\"");
            var X = new CMat(R.R, 1, x.Cx); Array.Copy(x.A, X.A, x.A.Length);
            var Am = R.CT().Mul(R); var xx = X.Mul(X.CT());
            for (int k = 0; k < Am.A.Length; k++) Am.A[k] += op == "+" ? xx.A[k] : -xx.A[k];
            var R1 = CholC(Am, "cholupdate", out int info);
            int err = info != 0 ? 1 : 0;
            if (err != 0 && n < 2) throw new OctaveException("cholupdate: downdate violates positiveness");
            if (err != 0) R1 = R;
            return new Value[] { R1.ToMV(rm.IsSingle), Dn(err) };
        });
        Reg("cholinsert", (it, a, n) =>
        {
            if (a.Length != 3) Usage("cholinsert");
            var rm = Fl(it, a[0], "cholinsert"); var R = CMat.From(rm); int j = I(a[1], "cholinsert") - 1;
            var x = CMat.From(Fl(it, a[2], "cholinsert"));
            int N = R.R;
            if (R.R != R.C || x.A.Length != N + 1) throw new OctaveException("cholinsert: dimension mismatch between R and X");
            if (j < 0 || j > N) throw new OctaveException("cholinsert: index J out of range");
            var A = R.CT().Mul(R); var B = new CMat(N + 1, N + 1, A.Cx || x.Cx);
            int Map(int i) => i < j ? i : i - 1;
            for (int c = 0; c <= N; c++) for (int r = 0; r <= N; r++)
            {
                if (r == j) B[r, c] = x.A[c] ; else if (c == j) B[r, c] = Complex.Conjugate(x.A[r]); else B[r, c] = A[Map(r), Map(c)];
            }
            var R1 = CholC(B, "cholinsert", out int info);
            if (info != 0 && n < 2) throw new OctaveException("cholinsert: singular matrix");
            return new Value[] { R1.ToMV(rm.IsSingle), Dn(info != 0 ? 1 : 0) };
        });
        Reg("choldelete", (it, a, n) =>
        {
            if (a.Length != 2) Usage("choldelete");
            var rm = Fl(it, a[0], "choldelete"); var R = CMat.From(rm); int j = I(a[1], "choldelete") - 1;
            if (R.R != R.C) throw new OctaveException("choldelete: R must be a square matrix");
            if (j < 0 || j >= R.R) throw new OctaveException("choldelete: index J out of range");
            var keep = Enumerable.Range(0, R.R).Where(i => i != j).ToArray();
            var A = R.CT().Mul(R).Rows(keep).Cols(keep);
            return new Value[] { CholC(A, "choldelete", out _).ToMV(rm.IsSingle) };
        });
        Reg("cholshift", (it, a, n) =>
        {
            if (a.Length != 3) Usage("cholshift");
            var rm = Fl(it, a[0], "cholshift"); var R = CMat.From(rm);
            int i = I(a[1], "cholshift") - 1, j = I(a[2], "cholshift") - 1;
            if (R.R != R.C) throw new OctaveException("cholshift: R must be a square matrix");
            if (i < 0 || j < 0 || i >= R.R || j >= R.R) throw new OctaveException("cholshift: index I or J is out of range");
            var p = ShiftPerm(R.R, i, j);
            var A = R.CT().Mul(R).Rows(p).Cols(p);
            return new Value[] { CholC(A, "cholshift", out _).ToMV(rm.IsSingle) };
        });
        Reg("qrupdate", (it, a, n) =>
        {
            if (a.Length != 4) Usage("qrupdate");
            var A = AFromQR(it, a[0], a[1], "qrupdate");
            var u = CMat.From(Fl(it, a[2], "qrupdate")); var v = CMat.From(Fl(it, a[3], "qrupdate"));
            if (u.A.Length != A.R || v.A.Length != A.C) throw new OctaveException("qrupdate: dimension mismatch");
            var B = new CMat(A.R, A.C, A.Cx || u.Cx || v.Cx);
            for (int c = 0; c < A.C; c++) for (int r = 0; r < A.R; r++) B[r, c] = A[r, c] + u.A[r] * Complex.Conjugate(v.A[c]);
            return ReQR(B, a[0], ((MatrixValue)Fl(it, a[0], "qrupdate")).IsSingle, n);
        });
        Reg("qrinsert", (it, a, n) =>
        {
            if (a.Length < 4 || a.Length > 5) Usage("qrinsert");
            var A = AFromQR(it, a[0], a[1], "qrinsert");
            var js = IdxList(it, a[2], "qrinsert"); var X = CMat.From(Fl(it, a[3], "qrinsert"));
            string orient = a.Length == 5 ? Sx(a[4]) : "col";
            if (orient != "col" && orient != "row") throw new OctaveException("qrinsert: ORIENT must be \"col\" or \"row\"");
            bool col = orient == "col";
            CMat B;
            if (col)
            {
                if (X.R != A.R && js.Length == 1 && X.A.Length == A.R) { var t = new CMat(A.R, 1, X.Cx); Array.Copy(X.A, t.A, X.A.Length); X = t; }
                int nc = A.C + js.Length; B = new CMat(A.R, nc, A.Cx || X.Cx);
                var isNew = new int[nc]; for (int q = 0; q < nc; q++) isNew[q] = -1;
                for (int q = 0; q < js.Length; q++) { if (js[q] < 1 || js[q] > nc) throw new OctaveException("qrinsert: index J out of range"); isNew[js[q] - 1] = q; }
                int src = 0;
                for (int c = 0; c < nc; c++) for (int r = 0; r < A.R; r++) B[r, c] = isNew[c] >= 0 ? X[r, isNew[c]] : A[r, src + (r == A.R - 1 ? 0 : 0)];
                src = 0; for (int c = 0; c < nc; c++) { if (isNew[c] >= 0) continue; for (int r = 0; r < A.R; r++) B[r, c] = A[r, src]; src++; }
            }
            else
            {
                if (js.Length != 1) throw new OctaveException("qrinsert: J must be a scalar for row insertion");
                int j = js[0] - 1; if (j < 0 || j > A.R) throw new OctaveException("qrinsert: index J out of range");
                B = new CMat(A.R + 1, A.C, A.Cx || X.Cx);
                for (int c = 0; c < A.C; c++) for (int r = 0; r <= A.R; r++) B[r, c] = r == j ? X.A[c] : A[r < j ? r : r - 1, c];
                var (Q2, R2, _) = HouseQR(B, false, false, true);
                return new Value[] { Q2.ToMV(), R2.ToMV() };
            }
            return ReQR(B, a[0], false, n);
        });
        Reg("qrdelete", (it, a, n) =>
        {
            if (a.Length < 3 || a.Length > 4) Usage("qrdelete");
            var A = AFromQR(it, a[0], a[1], "qrdelete");
            var js = IdxList(it, a[2], "qrdelete").Select(z => z - 1).ToHashSet();
            string orient = a.Length == 4 ? Sx(a[3]) : "col";
            if (orient != "col" && orient != "row") throw new OctaveException("qrdelete: ORIENT must be \"col\" or \"row\"");
            if (orient == "col")
            {
                if (js.Any(j => j < 0 || j >= A.C)) throw new OctaveException("qrdelete: index J out of range");
                var B = A.Cols(Enumerable.Range(0, A.C).Where(c => !js.Contains(c)).ToArray());
                return ReQR(B, a[0], false, n);
            }
            if (js.Any(j => j < 0 || j >= A.R)) throw new OctaveException("qrdelete: index J out of range");
            var Br = A.Rows(Enumerable.Range(0, A.R).Where(r => !js.Contains(r)).ToArray());
            var (Q2, R2, _) = HouseQR(Br, false, false, true);
            return new Value[] { Q2.ToMV(), R2.ToMV() };
        });
        Reg("qrshift", (it, a, n) =>
        {
            if (a.Length != 4) Usage("qrshift");
            var A = AFromQR(it, a[0], a[1], "qrshift");
            int i = I(a[2], "qrshift") - 1, j = I(a[3], "qrshift") - 1;
            if (i < 0 || j < 0 || i >= A.C || j >= A.C) throw new OctaveException("qrshift: index out of range");
            return ReQR(A.Cols(ShiftPerm(A.C, i, j)), a[0], false, n);
        });
    }

    static int[] ShiftPerm(int n, int i, int j)
    {
        var p = Enumerable.Range(0, n).ToArray();
        if (i < j) { for (int k = i; k < j; k++) p[k] = k + 1; p[j] = i; }
        else if (j < i) { p[j] = i; for (int k = j + 1; k <= i; k++) p[k] = k - 1; }
        return p;
    }
}
