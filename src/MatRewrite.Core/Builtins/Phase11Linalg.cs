// MatRewrite - GPL-3.0-or-later
// Phase 11: real Schur decomposition (Householder Hessenberg + Francis double-shift QR, JAMA/EISPACK
// style) with LAPACK dlanv2-style standardisation of 2x2 blocks, and complex QZ.
using System.Numerics;
using MatRewrite.Core.Runtime;
using CM = MathNet.Numerics.LinearAlgebra.Matrix<System.Numerics.Complex>;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    internal static (double[,] U, double[,] T) RealSchur(double[,] A)
    {
        int nn = A.GetLength(0);
        var H = (double[,])A.Clone(); var V = new double[nn, nn]; var ort = new double[nn];
        int low = 0, high = nn - 1;
        for (int m = low + 1; m <= high - 1; m++)
        {
            double scale = 0; for (int i = m; i <= high; i++) scale += Math.Abs(H[i, m - 1]);
            if (scale == 0) continue;
            double h = 0;
            for (int i = high; i >= m; i--) { ort[i] = H[i, m - 1] / scale; h += ort[i] * ort[i]; }
            double g = Math.Sqrt(h); if (ort[m] > 0) g = -g;
            h -= ort[m] * g; ort[m] -= g;
            for (int j = m; j < nn; j++) { double f = 0; for (int i = high; i >= m; i--) f += ort[i] * H[i, j]; f /= h; for (int i = m; i <= high; i++) H[i, j] -= f * ort[i]; }
            for (int i = 0; i <= high; i++) { double f = 0; for (int j = high; j >= m; j--) f += ort[j] * H[i, j]; f /= h; for (int j = m; j <= high; j++) H[i, j] -= f * ort[j]; }
            ort[m] = scale * ort[m]; H[m, m - 1] = scale * g;
        }
        for (int i = 0; i < nn; i++) V[i, i] = 1;
        for (int m = high - 1; m >= low + 1; m--)
        {
            if (H[m, m - 1] == 0) continue;
            for (int i = m + 1; i <= high; i++) ort[i] = H[i, m - 1];
            for (int j = m; j <= high; j++)
            {
                double g = 0; for (int i = m; i <= high; i++) g += ort[i] * V[i, j];
                g = (g / ort[m]) / H[m, m - 1];
                for (int i = m; i <= high; i++) V[i, j] += g * ort[i];
            }
        }
        for (int i = 0; i < nn; i++) for (int j = 0; j < i - 1; j++) H[i, j] = 0;

        // Francis double-shift QR
        int n = nn - 1; double eps = Math.Pow(2, -52), exshift = 0, p = 0, q = 0, r = 0, s = 0, z = 0, w, x, y;
        double norm = 0; for (int i = 0; i < nn; i++) for (int j = Math.Max(i - 1, 0); j < nn; j++) norm += Math.Abs(H[i, j]);
        int iter = 0, total = 0;
        while (n >= low)
        {
            int l = n;
            while (l > low) { s = Math.Abs(H[l - 1, l - 1]) + Math.Abs(H[l, l]); if (s == 0) s = norm; if (Math.Abs(H[l, l - 1]) < eps * s) break; l--; }
            if (l == n) { H[n, n] += exshift; if (n > 0) H[n, n - 1] = 0; n--; iter = 0; }
            else if (l == n - 1)
            {
                w = H[n, n - 1] * H[n - 1, n]; p = (H[n - 1, n - 1] - H[n, n]) / 2; q = p * p + w; z = Math.Sqrt(Math.Abs(q));
                H[n, n] += exshift; H[n - 1, n - 1] += exshift; x = H[n, n];
                if (q >= 0)
                {
                    z = p >= 0 ? p + z : p - z;
                    x = H[n, n - 1]; s = Math.Abs(x) + Math.Abs(z); p = x / s; q = z / s; r = Math.Sqrt(p * p + q * q); p /= r; q /= r;
                    for (int j = n - 1; j < nn; j++) { z = H[n - 1, j]; H[n - 1, j] = q * z + p * H[n, j]; H[n, j] = q * H[n, j] - p * z; }
                    for (int i = 0; i <= n; i++) { z = H[i, n - 1]; H[i, n - 1] = q * z + p * H[i, n]; H[i, n] = q * H[i, n] - p * z; }
                    for (int i = low; i <= high; i++) { z = V[i, n - 1]; V[i, n - 1] = q * z + p * V[i, n]; V[i, n] = q * V[i, n] - p * z; }
                    H[n, n - 1] = 0;
                }
                if (n - 1 > 0) H[n - 1, n - 2] = 0;
                n -= 2; iter = 0;
            }
            else
            {
                x = H[n, n]; y = 0; w = 0;
                if (l < n) { y = H[n - 1, n - 1]; w = H[n, n - 1] * H[n - 1, n]; }
                if (iter == 10)
                {
                    exshift += x; for (int i = low; i <= n; i++) H[i, i] -= x;
                    s = Math.Abs(H[n, n - 1]) + Math.Abs(H[n - 1, n - 2]); x = y = 0.75 * s; w = -0.4375 * s * s;
                }
                if (iter == 30)
                {
                    s = (y - x) / 2; s = s * s + w;
                    if (s > 0) { s = Math.Sqrt(s); if (y < x) s = -s; s = x - w / ((y - x) / 2 + s); for (int i = low; i <= n; i++) H[i, i] -= s; exshift += s; x = y = w = 0.964; }
                }
                iter++; if (++total > 100 * nn + 1000) throw new OctaveException("schur: QR iteration failed to converge");
                int m = n - 2;
                while (m >= l)
                {
                    z = H[m, m]; r = x - z; s = y - z;
                    p = (r * s - w) / H[m + 1, m] + H[m, m + 1]; q = H[m + 1, m + 1] - z - r - s; r = H[m + 2, m + 1];
                    s = Math.Abs(p) + Math.Abs(q) + Math.Abs(r); p /= s; q /= s; r /= s;
                    if (m == l) break;
                    if (Math.Abs(H[m, m - 1]) * (Math.Abs(q) + Math.Abs(r)) < eps * (Math.Abs(p) * (Math.Abs(H[m - 1, m - 1]) + Math.Abs(z) + Math.Abs(H[m + 1, m + 1])))) break;
                    m--;
                }
                for (int i = m + 2; i <= n; i++) { H[i, i - 2] = 0; if (i > m + 2) H[i, i - 3] = 0; }
                for (int k = m; k <= n - 1; k++)
                {
                    bool notlast = k != n - 1;
                    if (k != m)
                    {
                        p = H[k, k - 1]; q = H[k + 1, k - 1]; r = notlast ? H[k + 2, k - 1] : 0;
                        x = Math.Abs(p) + Math.Abs(q) + Math.Abs(r);
                        if (x == 0) continue;
                        p /= x; q /= x; r /= x;
                    }
                    s = Math.Sqrt(p * p + q * q + r * r); if (p < 0) s = -s;
                    if (s != 0)
                    {
                        if (k != m) { H[k, k - 1] = -s * x; H[k + 1, k - 1] = 0; if (notlast) H[k + 2, k - 1] = 0; } else if (l != m) H[k, k - 1] = -H[k, k - 1];
                        p += s; x = p / s; y = q / s; z = r / s; q /= p; r /= p;
                        for (int j = k; j < nn; j++)
                        {
                            p = H[k, j] + q * H[k + 1, j];
                            if (notlast) { p += r * H[k + 2, j]; H[k + 2, j] -= p * z; }
                            H[k, j] -= p * x; H[k + 1, j] -= p * y;
                        }
                        for (int i = 0; i <= Math.Min(n, k + 3); i++)
                        {
                            p = x * H[i, k] + y * H[i, k + 1];
                            if (notlast) { p += z * H[i, k + 2]; H[i, k + 2] -= p * r; }
                            H[i, k] -= p; H[i, k + 1] -= p * q;
                        }
                        for (int i = low; i <= high; i++)
                        {
                            p = x * V[i, k] + y * V[i, k + 1];
                            if (notlast) { p += z * V[i, k + 2]; V[i, k + 2] -= p * r; }
                            V[i, k] -= p; V[i, k + 1] -= p * q;
                        }
                    }
                }
            }
        }
        // standardise 2x2 blocks: equal diagonal (dlanv2)
        for (int j = 0; j < nn - 1; j++)
        {
            if (H[j + 1, j] == 0) continue;
            double a = H[j, j], b = H[j, j + 1], c = H[j + 1, j], d = H[j + 1, j + 1];
            double temp = a - d, pp = 0.5 * temp, sigma = b + c, tau = Hypot(sigma, temp);
            double cs, sn;
            if (tau == 0) { cs = 1; sn = 0; }
            else { cs = Math.Sqrt(0.5 * (1 + Math.Abs(sigma) / tau)); sn = -(pp / (tau * cs)) * (sigma >= 0 ? 1 : -1); }
            for (int i = 0; i < nn; i++) { double h1 = H[i, j], h2 = H[i, j + 1]; H[i, j] = h1 * cs + h2 * sn; H[i, j + 1] = -h1 * sn + h2 * cs; }
            for (int k = 0; k < nn; k++) { double h1 = H[j, k], h2 = H[j + 1, k]; H[j, k] = cs * h1 + sn * h2; H[j + 1, k] = -sn * h1 + cs * h2; }
            for (int i = 0; i < nn; i++) { double v1 = V[i, j], v2 = V[i, j + 1]; V[i, j] = v1 * cs + v2 * sn; V[i, j + 1] = -v1 * sn + v2 * cs; }
            double avg = 0.5 * (H[j, j] + H[j + 1, j + 1]); H[j, j] = avg; H[j + 1, j + 1] = avg;
            if (j + 2 < nn) H[j + 2, j] = 0;
            j++;
        }
        for (int i = 0; i < nn; i++) for (int j = 0; j < i - 1; j++) H[i, j] = 0;
        return (V, H);
    }
    static double Hypot(double a, double b) { a = Math.Abs(a); b = Math.Abs(b); if (a < b) (a, b) = (b, a); return a == 0 ? 0 : a * Math.Sqrt(1 + (b / a) * (b / a)); }

    static double[,] To2D(MatrixValue m) { var r = new double[m.R, m.C]; for (int j = 0; j < m.C; j++) for (int i = 0; i < m.R; i++) r[i, j] = m.Re[i + j * m.R]; return r; }
    static MatrixValue From2D(double[,] a) { int R = a.GetLength(0), C = a.GetLength(1); var m = new MatrixValue(R, C); for (int j = 0; j < C; j++) for (int i = 0; i < R; i++) m.Re[i + j * R] = a[i, j]; return m; }

    static void RegisterPhase11Linalg()
    {
        var oldSchur = cur!.Builtins["schur"];
        Reg("schur", (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to schur");
            var m = M(a[0], "schur");
            if (m.R != m.C) throw new OctaveException("schur: argument must be a square matrix");
            string opt = a.Length > 1 && a[1] is MatrixValue om && om.IsChar ? om.AsString().ToLowerInvariant() : "";
            if (!m.IsComplex && (opt == "complex" || opt == "c") && m.Numel > 0 && m.Kind == NumKind.Double && !m.Re.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
            {
                var (U0, T0) = RealSchur(To2D(m)); bool tri = true;
                for (int i = 1; i < m.R && tri; i++) if (T0[i, i - 1] != 0) tri = false;
                if (tri) { var Tm0 = From2D(T0); var Um0 = From2D(U0); return n <= 1 ? Out(Tm0) : Out(Um0, Tm0); }
            }
            if (m.IsComplex || opt == "complex" || opt == "c" || m.Numel == 0) return oldSchur(it, new[] { a[0] }, n);
            if (m.Re.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
            {
                var nanm = new MatrixValue(m.R, m.C); Array.Fill(nanm.Re, double.NaN);
                return n <= 1 ? Out(nanm) : Out(nanm, nanm);
            }
            var (U, T) = RealSchur(To2D(m));
            var Tm = From2D(T); var Um = From2D(U);
            if (m.Kind == NumKind.Single) { Tm = (MatrixValue)it.CallFunction("single", new Value[] { Tm }, 1)[0]; Um = (MatrixValue)it.CallFunction("single", new Value[] { Um }, 1)[0]; }
            return n <= 1 ? Out(Tm) : Out(Um, Tm);
        });
        Reg("qz", (it, a, n) =>
        {
            if (a.Length < 2) throw new OctaveException("Invalid call to qz");
            var A = M(a[0], "qz"); var B = M(a[1], "qz");
            if (A.R != A.C || B.R != B.C || A.R != B.R) throw new OctaveException("qz: A and B must be square matrices of the same size");
            string opt = a.Length > 2 && a[2] is MatrixValue om && om.IsChar ? om.AsString().ToLowerInvariant() : "complex";
            if (opt != "real" && opt != "complex") throw new OctaveException("qz: OPT must be 'real' or 'complex'");
            int N = A.R;
            if (N == 0) { var e = new MatrixValue(0, 0); return Out(e, e, e, e, e, e); }
            bool real = opt == "real" && !A.IsComplex && !B.IsComplex;
            MatrixValue AA, BB, Qm, Zm;
            if (real)
            {
                var Am = Ops.ToMathNet(A); var Bm = Ops.ToMathNet(B);
                if (Bm.Rank() < N) throw new OctaveException("qz: singular B is not supported");
                var (Z, T) = RealSchur(To2D(Ops.FromMathNet(Bm.Solve(Am))));
                var Zd = Ops.ToMathNet(From2D(Z));
                var qr = (Bm * Zd).QR(MathNet.Numerics.LinearAlgebra.Factorization.QRMethod.Full);
                var Q = qr.Q; var R = qr.R;
                var Qt = Q.Transpose();
                var AAm = Qt * Am * Zd; var BBm = Qt * Bm * Zd;
                for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) { if (i > j) BBm[i, j] = 0; if (i > j + 1 || (i == j + 1 && T[i, j] == 0)) AAm[i, j] = 0; }
                AA = Ops.FromMathNet(AAm); BB = Ops.FromMathNet(BBm); Qm = Ops.FromMathNet(Qt); Zm = Ops.FromMathNet(Zd);
            }
            else
            {
                var Ac = ToCM(A); var Bc = ToCM(B);
                if (Bc.Rank() < N) throw new OctaveException("qz: singular B is not supported");
                var (U, T) = ComplexSchur(Bc.Solve(Ac));
                var qr = (Bc * U).QR(MathNet.Numerics.LinearAlgebra.Factorization.QRMethod.Full);
                var Qh = qr.Q.ConjugateTranspose();
                var AAc = Qh * Ac * U; var BBc = Qh * Bc * U;
                for (int i = 0; i < N; i++) for (int j = 0; j < i; j++) { AAc[i, j] = 0; BBc[i, j] = 0; }
                AA = FromCM(AAc, false); BB = FromCM(BBc, false); Qm = FromCM(Qh, false); Zm = FromCM(U, false);
            }
            if (n <= 4) return Out(AA, BB, Qm, Zm);
            var ev = it.CallFunction("eig", new Value[] { A, B }, 2);
            var evl = it.CallFunction("eig", new Value[] { it.CallFunction("ctranspose", new Value[] { A }, 1)[0], it.CallFunction("ctranspose", new Value[] { B }, 1)[0] }, 2);
            return Out(AA, BB, Qm, Zm, ev[0], evl[0]);
        });
    }
}
