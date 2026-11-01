// Phase 10 plot round 2: native kernels used by CoreLib isosurface/isocaps/stream*.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static readonly int[,] McOff = { { 0, 0, 0 }, { 1, 0, 0 }, { 1, 1, 0 }, { 0, 1, 0 }, { 0, 0, 1 }, { 1, 0, 1 }, { 1, 1, 1 }, { 0, 1, 1 } };
    static readonly int[,] McEdges = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };

    static void RegisterPhase10Plot()
    {
        // quiver is now provided by CoreLib quiver.m/__quiv__.m (hggroup with stem/head/base children).
        cur!.Builtins.Remove("quiver");
        foreach (var nm in new[] { "expm", "null", "condeig", "ordeig" }) cur.Builtins.Remove(nm);
        // C = __streameuler__ (D, U, V, [W], TX, TY, [TZ], P0, H, MAXV): Euler streamline in 0-based grid coordinates.
        Reg("__streameuler__", (it, a, nout) =>
        {
            int d = I(a[0], "__streameuler__");
            var F = new MatrixValue[d]; var T = new double[d][];
            for (int q = 0; q < d; q++) F[q] = M(a[1 + q], "__streameuler__");
            for (int q = 0; q < d; q++) T[q] = M(a[1 + d + q], "__streameuler__").Re;
            var p0 = M(a[1 + 2 * d], "__streameuler__").Re; double h = D(a[2 + 2 * d], "__streameuler__"); int maxv = I(a[3 + 2 * d], "__streameuler__");
            var dims = F[0].Dims; int[] n = new int[d]; for (int q = 0; q < d; q++) n[q] = q < dims.Length ? dims[q] : 1;
            // grid order: dims[0] = rows = y, dims[1] = cols = x, dims[2] = z ; coordinates p = (x, y, z)
            int[] ax = d == 2 ? new[] { 1, 0 } : new[] { 1, 0, 2 };
            double[]? Vel(double[] p)
            {
                var i0 = new int[d]; var fr = new double[d];
                for (int q = 0; q < d; q++)
                {
                    int nn = n[ax[q]];
                    if (double.IsNaN(p[q]) || p[q] < 0 || p[q] > nn - 1) return null;
                    i0[q] = Math.Min((int)Math.Floor(p[q]), Math.Max(nn - 2, 0)); fr[q] = p[q] - i0[q];
                }
                var v = new double[d];
                for (int corner = 0; corner < (1 << d); corner++)
                {
                    double w = 1; var idx = new int[3];
                    for (int q = 0; q < d; q++) { int b = (corner >> q) & 1; w *= b == 1 ? fr[q] : 1 - fr[q]; idx[ax[q]] = Math.Min(i0[q] + b, n[ax[q]] - 1); }
                    if (w == 0) continue;
                    int lin = idx[0] + n[0] * (idx[1] + (d == 3 ? n[1] * idx[2] : 0));
                    for (int q = 0; q < d; q++) v[q] += w * F[q].Re[lin];
                }
                for (int q = 0; q < d; q++) { int ii = Math.Min((int)Math.Floor(p[q]), T[q].Length - 1); v[q] *= T[q][Math.Max(ii, 0)]; }
                return v;
            }
            List<double[]> Trace(double dir)
            {
                var pts = new List<double[]>(); var p = (double[])p0.Clone();
                if (Vel(p) == null) return pts;
                pts.Add((double[])p.Clone());
                while (pts.Count < maxv)
                {
                    var v = Vel(p); if (v == null) break;
                    double nv = Math.Sqrt(v.Sum(x => x * x)); if (nv == 0 || double.IsNaN(nv)) break;
                    var np = new double[d]; for (int q = 0; q < d; q++) np[q] = p[q] + dir * h * v[q] / nv;
                    if (Vel(np) == null) break;
                    p = np; pts.Add((double[])p.Clone());
                }
                return pts;
            }
            int sgn = h > 0 ? 1 : -1; h = Math.Abs(h); var all = Trace(sgn);
            var R = new MatrixValue(all.Count, d);
            for (int r = 0; r < all.Count; r++) for (int q = 0; q < d; q++) R.Re[r + q * all.Count] = all[r][q];
            return new Value[] { R };
        });
        // [T, P, COL] = __marching_cube__ (XX, YY, ZZ, V, ISO [, COL])
        Reg("__marching_cube__", (it, a, nout) =>
        {
            if (a.Length is < 5 or > 6) Usage("__marching_cube__");
            var X = M(a[0], "__marching_cube__"); var Y = M(a[1], "__marching_cube__"); var Z = M(a[2], "__marching_cube__");
            var V = M(a[3], "__marching_cube__"); double iso = D(a[4], "__marching_cube__");
            MatrixValue? C = a.Length == 6 ? M(a[5], "__marching_cube__") : null;
            var dims = V.Dims;
            if (dims.Length != 3) throw new OctaveException("__marching_cube__: XX, YY, ZZ, v must be 3-D matrices");
            int n0 = dims[0], n1 = dims[1], n2 = dims[2];
            if (n0 < 2 || n1 < 2 || n2 < 2) throw new OctaveException("__marching_cube__: grid size must be at least 2x2x2");
            if (X.Numel != V.Numel || Y.Numel != V.Numel || Z.Numel != V.Numel || (C != null && C.Numel != V.Numel))
                throw new OctaveException("__marching_cube__: XX, YY, ZZ, v must be of equal size");
            int c0 = n0 - 1, c1 = n1 - 1, c2 = n2 - 1, nc = c0 * c1 * c2;
            int L(int i, int j, int k) => i + n0 * (j + n1 * k);
            var cube = new int[nc];
            var vals = V.Re;
            for (int k = 0; k < c2; k++) for (int j = 0; j < c1; j++) for (int i = 0; i < c0; i++)
            {
                int cc = 0;
                for (int q = 0; q < 8; q++) if (vals[L(i + McOff[q, 0], j + McOff[q, 1], k + McOff[q, 2])] > iso) cc |= 1 << q;
                cube[i + c0 * (j + c1 * k)] = cc;
            }
            var vid = new int[nc, 12];
            var P = new List<double[]>();
            for (int e = 0; e < 12; e++)
                for (int id = 0; id < nc; id++)
                {
                    if ((MarchingTables.Edge[cube[id]] & (1 << e)) == 0) continue;
                    int i = id % c0, j = id / c0 % c1, k = id / (c0 * c1);
                    int a1 = McEdges[e, 0], a2 = McEdges[e, 1];
                    int p1 = L(i + McOff[a1, 0], j + McOff[a1, 1], k + McOff[a1, 2]);
                    int p2 = L(i + McOff[a2, 0], j + McOff[a2, 1], k + McOff[a2, 2]);
                    double v1 = vals[p1], v2 = vals[p2];
                    double mu = Math.Abs(v1 - v2) < 10 * double.Epsilon * 0 + 10 * 2.220446049250313e-16 * (Math.Abs(v1) + Math.Abs(v2)) ? 0 : (iso - v1) / (v2 - v1);
                    var pt = new double[4];
                    pt[0] = X.Re[p1] + mu * (X.Re[p2] - X.Re[p1]);
                    pt[1] = Y.Re[p1] + mu * (Y.Re[p2] - Y.Re[p1]);
                    pt[2] = Z.Re[p1] + mu * (Z.Re[p2] - Z.Re[p1]);
                    if (C != null) pt[3] = C.Re[p1] + mu * (C.Re[p2] - C.Re[p1]);
                    P.Add(pt); vid[id, e] = P.Count;
                }
            var T = new List<int[]>();
            for (int s = 0; s < 15; s += 3)
                for (int id = 0; id < nc; id++)
                {
                    int b = cube[id] * 16 + s;
                    if (MarchingTables.Tri[b] < 0) continue;
                    T.Add(new[] { vid[id, MarchingTables.Tri[b]], vid[id, MarchingTables.Tri[b + 1]], vid[id, MarchingTables.Tri[b + 2]] });
                }
            var Tm = new MatrixValue(T.Count, 3); var Pm = new MatrixValue(P.Count, 3); var Cm = new MatrixValue(C != null ? P.Count : 0, C != null ? 1 : 0);
            for (int r = 0; r < T.Count; r++) for (int q = 0; q < 3; q++) Tm.Re[r + q * T.Count] = T[r][q];
            for (int r = 0; r < P.Count; r++) { for (int q = 0; q < 3; q++) Pm.Re[r + q * P.Count] = P[r][q]; if (C != null) Cm.Re[r] = P[r][3]; }
            if (T.Count == 0) { Tm = new MatrixValue(0, 0); Pm = new MatrixValue(0, 0); Cm = new MatrixValue(0, 0); }
            return new Value[] { Tm, Pm, Cm };
        });
    }
}
