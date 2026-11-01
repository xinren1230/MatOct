// MatRewrite - GPL-3.0-or-later
// decic: consistent initial conditions for ode15i (port of scripts/ode/decic.m).
// The objective ||F(t0,y,yp)|| is minimised with a Levenberg-Marquardt least-squares solver.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase8Decic()
    {
        Reg("decic", (it, a, nout) =>
        {
            if (a.Length < 6) throw new OctaveException("Invalid call to decic");
            if (a[0] is not FunctionHandleValue) throw new OctaveException("decic: FCN must be a valid function handle");
            if (!(IsNum(a[1]) && IsScal(a[1]))) throw new OctaveException("decic: T0 must be a numeric scalar");
            for (int q = 2; q < 6; q++)
                if (!(IsNum(a[q]) && IsVec(a[q]))) throw new OctaveException("decic: Y0, FIXED_Y0, YP0, and FIXED_YP0 must be numeric vectors");
            var y0 = GVec(a[2]).ToArray(); var fy = GVec(a[3]); var yp0 = GVec(a[4]).ToArray(); var fyp = GVec(a[5]);
            int n = y0.Length;
            if (fy.Length != n || yp0.Length != n || fyp.Length != n) throw new OctaveException("decic: length of Y0, FIXED_Y0, YP0, and FIXED_YP0 must be equal");
            for (int i = 0; i < n; i++)
                if (!(fy[i] == 0 || fy[i] == 1) || !(fyp[i] == 0 || fyp[i] == 1)) throw new OctaveException("decic: FIXED_Y0 and FIXED_YP0 must be boolean vectors");
            var fl = Enumerable.Range(0, n).Where(i => fy[i] == 0).ToArray();
            var fu = Enumerable.Range(0, n).Where(i => fyp[i] == 0).ToArray();
            if (n - fl.Length - fu.Length > 0) throw new OctaveException("decic: cannot fix more than length (Y0) components");
            double tolf = 0, tolx = Math.Pow(2, -52);
            if (a.Length >= 7 && a[6] is StructValue opt)
            {
                Value G(string f) => opt.HasField(f) ? opt.Get(f) : MatrixValue.Empty();
                var at = G("AbsTol"); if (NumelOf(at) > 0) { if (!IsScal(at)) throw new OctaveException("decic: AbsTol must be a scalar value"); tolf = D(at, "decic"); }
                var rt = G("RelTol"); if (NumelOf(rt) > 0) { if (!IsScal(rt)) throw new OctaveException("decic: RelTol must be a scalar value"); tolx = D(rt, "decic"); }
            }
            Value col(double[] v) => new MatrixValue(v.Length, 1, (double[])v.Clone(), null);
            (double[] y, double[] yp) Unpack(double[] x)
            {
                var y = (double[])y0.Clone(); var yp = (double[])yp0.Clone();
                for (int q = 0; q < fl.Length; q++) y[fl[q]] = x[q];
                for (int q = 0; q < fu.Length; q++) yp[fu[q]] = x[fl.Length + q];
                return (y, yp);
            }
            double[] Res(double[] x) { var (y, yp) = Unpack(x); return GVec(it.CallCallable(a[0], new[] { a[1], col(y), col(yp) }, 1)[0]).ToArray(); }
            int m = fl.Length + fu.Length;
            var xk = fl.Select(i => y0[i]).Concat(fu.Select(i => yp0[i])).ToArray();
            var r = Res(xk); double nr = r.Sum(q => q * q); double lam = 1e-3;
            for (int iter = 0; iter < 400 && m > 0; iter++)
            {
                if (Math.Sqrt(nr) <= tolf) break;
                int nr_ = r.Length; var J = new double[nr_, m];
                for (int j = 0; j < m; j++)
                {
                    double h = Math.Pow(2, -52.0 / 3) * Math.Max(1, Math.Abs(xk[j]));
                    var xp = (double[])xk.Clone(); xp[j] += h; var xm = (double[])xk.Clone(); xm[j] -= h;
                    var rp = Res(xp); var rm = Res(xm);
                    for (int i = 0; i < nr_; i++) J[i, j] = (rp[i] - rm[i]) / (2 * h);
                }
                var JtJ = new double[m, m]; var Jtr = new double[m];
                for (int p = 0; p < m; p++) { for (int q = 0; q < m; q++) { double s = 0; for (int i = 0; i < nr_; i++) s += J[i, p] * J[i, q]; JtJ[p, q] = s; } double t = 0; for (int i = 0; i < nr_; i++) t += J[i, p] * r[i]; Jtr[p] = t; }
                bool improved = false;
                for (int tries = 0; tries < 30; tries++)
                {
                    var Ab = new double[m, m + 1];
                    for (int p = 0; p < m; p++) { for (int q = 0; q < m; q++) Ab[p, q] = JtJ[p, q] + (p == q ? lam * (JtJ[p, p] + 1e-12) : 0); Ab[p, m] = -Jtr[p]; }
                    var dx = SolveGauss(Ab, m);
                    var xn = xk.Select((v, i) => v + dx[i]).ToArray();
                    var rn = Res(xn); double nn = rn.Sum(q => q * q);
                    if (nn < nr) { double step = Math.Sqrt(dx.Sum(q => q * q)); xk = xn; r = rn; nr = nn; lam = Math.Max(lam / 10, 1e-15); improved = true; if (step <= tolx * (1 + Math.Sqrt(xk.Sum(q => q * q)))) iter = int.MaxValue - 1; break; }
                    lam *= 10;
                }
                if (!improved) break;
            }
            var (yN, ypN) = Unpack(xk);
            var outs = new List<Value> { col(yN), col(ypN) };
            if (nout > 2) outs.Add(it.CallCallable(a[0], new[] { a[1], col(yN), col(ypN) }, 1)[0]);
            return outs.ToArray();
        });
    }

    static double[] SolveGauss(double[,] A, int m)
    {
        for (int c = 0; c < m; c++)
        {
            int p = c; for (int i = c + 1; i < m; i++) if (Math.Abs(A[i, c]) > Math.Abs(A[p, c])) p = i;
            if (p != c) for (int j = 0; j <= m; j++) (A[c, j], A[p, j]) = (A[p, j], A[c, j]);
            double d = A[c, c]; if (d == 0) continue;
            for (int i = c + 1; i < m; i++) { double f = A[i, c] / d; if (f == 0) continue; for (int j = c; j <= m; j++) A[i, j] -= f * A[c, j]; }
        }
        var x = new double[m];
        for (int i = m - 1; i >= 0; i--) { double s = A[i, m]; for (int j = i + 1; j < m; j++) s -= A[i, j] * x[j]; x[i] = A[i, i] == 0 ? 0 : s / A[i, i]; }
        return x;
    }
}
