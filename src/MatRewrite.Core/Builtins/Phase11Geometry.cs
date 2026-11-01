// MatRewrite - GPL-3.0-or-later
// Native computational geometry: Bowyer-Watson Delaunay triangulation (N-D), convex hulls,
// point location (tsearch) and Voronoi cells derived from the Delaunay triangulation.
using MatRewrite.Core.Runtime;
namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    sealed class Simplex { public int[] V = null!; public double[] C = null!; public double R2; public bool Alive = true; }

    static double[]? SolveSmall(double[,] A, double[] b)
    {
        int n = b.Length; var a = (double[,])A.Clone(); var x = (double[])b.Clone();
        double scale = 0; foreach (var v in a) scale = Math.Max(scale, Math.Abs(v));
        for (int k = 0; k < n; k++)
        {
            int p = k; for (int i = k + 1; i < n; i++) if (Math.Abs(a[i, k]) > Math.Abs(a[p, k])) p = i;
            if (Math.Abs(a[p, k]) <= 1e-13 * scale || a[p, k] == 0) return null;
            if (p != k) { for (int j = 0; j < n; j++) (a[k, j], a[p, j]) = (a[p, j], a[k, j]); (x[k], x[p]) = (x[p], x[k]); }
            for (int i = k + 1; i < n; i++) { double f = a[i, k] / a[k, k]; if (f == 0) continue; for (int j = k; j < n; j++) a[i, j] -= f * a[k, j]; x[i] -= f * x[k]; }
        }
        for (int k = n - 1; k >= 0; k--) { double s = x[k]; for (int j = k + 1; j < n; j++) s -= a[k, j] * x[j]; x[k] = s / a[k, k]; }
        return x;
    }

    static double Det(double[,] A)
    {
        int n = A.GetLength(0); var a = (double[,])A.Clone(); double d = 1;
        for (int k = 0; k < n; k++)
        {
            int p = k; for (int i = k + 1; i < n; i++) if (Math.Abs(a[i, k]) > Math.Abs(a[p, k])) p = i;
            if (a[p, k] == 0) return 0;
            if (p != k) { for (int j = 0; j < n; j++) (a[k, j], a[p, j]) = (a[p, j], a[k, j]); d = -d; }
            d *= a[k, k];
            for (int i = k + 1; i < n; i++) { double f = a[i, k] / a[k, k]; for (int j = k; j < n; j++) a[i, j] -= f * a[k, j]; }
        }
        return d;
    }

    /// <summary>Signed volume*d! of simplex given vertex coordinates.</summary>
    static double SimplexDet(double[][] P, int[] v)
    {
        int d = P[v[0]].Length; var A = new double[d, d];
        for (int i = 0; i < d; i++) for (int j = 0; j < d; j++) A[i, j] = P[v[i + 1]][j] - P[v[0]][j];
        return Det(A);
    }

    static void Circum(double[][] P, Simplex s)
    {
        int d = P[0].Length; var A = new double[d, d]; var b = new double[d]; var p0 = P[s.V[0]];
        for (int i = 0; i < d; i++)
        {
            var pi = P[s.V[i + 1]]; double r = 0;
            for (int j = 0; j < d; j++) { double t = pi[j] - p0[j]; A[i, j] = 2 * t; r += t * t; }
            b[i] = r;
        }
        var c = SolveSmall(A, b);
        if (c == null) { s.C = new double[d]; s.R2 = double.PositiveInfinity; return; }
        double r2 = 0; for (int j = 0; j < d; j++) { r2 += c[j] * c[j]; c[j] += p0[j]; }
        s.C = c; s.R2 = r2;
    }

    /// <summary>Delaunay triangulation of points (rows). Returns 0-based simplices, positively oriented, degenerate ones removed.</summary>
    internal static List<int[]> Delaunay(double[][] pts)
    {
        int n = pts.Length; var res = new List<int[]>();
        if (n == 0) return res;
        int d = pts[0].Length;
        if (d == 1)
        {
            var ord = Enumerable.Range(0, n).OrderBy(i => pts[i][0]).ToList();
            for (int k = 0; k + 1 < ord.Count; k++) if (pts[ord[k + 1]][0] > pts[ord[k]][0]) res.Add(new[] { ord[k], ord[k + 1] });
            return res;
        }
        // normalize
        var lo = new double[d]; var hi = new double[d];
        for (int j = 0; j < d; j++) { lo[j] = pts.Min(p => p[j]); hi[j] = pts.Max(p => p[j]); }
        double sc = 0; for (int j = 0; j < d; j++) sc = Math.Max(sc, hi[j] - lo[j]); if (sc == 0) return res;
        var P = new double[n + d + 1][];
        for (int i = 0; i < n; i++) { P[i] = new double[d]; for (int j = 0; j < d; j++) P[i][j] = 2 * (pts[i][j] - lo[j]) / sc - 1; }
        const double M = 1e4; double K = M * (d + 1) * 4;
        for (int k = 0; k <= d; k++) { P[n + k] = Enumerable.Repeat(-M, d).ToArray(); if (k > 0) P[n + k][k - 1] += K; }
        // remove exact duplicates
        var seen = new Dictionary<string, int>(); var use = new List<int>();
        for (int i = 0; i < n; i++) { var key = string.Join(",", pts[i].Select(v => v.ToString("R"))); if (seen.TryAdd(key, i)) use.Add(i); }
        var simp = new List<Simplex>();
        var s0 = new Simplex { V = Enumerable.Range(n, d + 1).ToArray() }; Circum(P, s0); simp.Add(s0);
        use.Reverse();
        foreach (int p in use)
        {
            var x = P[p]; var bad = new List<Simplex>();
            foreach (var s in simp)
            {
                if (!s.Alive) continue;
                if (double.IsPositiveInfinity(s.R2)) { bad.Add(s); continue; }
                double dd = 0; for (int j = 0; j < d; j++) { double t = x[j] - s.C[j]; dd += t * t; }
                if (dd < s.R2 * (1 - 1e-12)) bad.Add(s);
            }
            if (bad.Count == 0) continue; // should not happen
            var faces = new Dictionary<string, (int[] f, int cnt)>();
            foreach (var s in bad)
            {
                s.Alive = false;
                for (int k = 0; k <= d; k++)
                {
                    var f = s.V.Where((_, i) => i != k).OrderBy(v => v).ToArray();
                    var key = string.Join(",", f);
                    faces[key] = faces.TryGetValue(key, out var e) ? (e.f, e.cnt + 1) : (f, 1);
                }
            }
            foreach (var (f, cnt) in faces.Values)
            {
                if (cnt != 1) continue;
                var ns = new Simplex { V = f.Append(p).ToArray() }; Circum(P, ns); simp.Add(ns);
            }
            if (simp.Count > 4 * simp.Count(s => s.Alive) + 1000) simp = simp.Where(s => s.Alive).ToList();
        }
        // volume tolerance relative to normalized coordinates
        foreach (var s in simp)
        {
            if (!s.Alive || s.V.Any(v => v >= n)) continue;
            var v = (int[])s.V.Clone();
            double det = SimplexDet(P, v);
            if (Math.Abs(det) < 1e-12) continue;
            if (det < 0) (v[d - 1], v[d]) = (v[d], v[d - 1]);
            res.Add(v);
        }
        return res;
    }

    static double[][] Rows(MatrixValue m) { var r = new double[m.R][]; for (int i = 0; i < m.R; i++) { r[i] = new double[m.C]; for (int j = 0; j < m.C; j++) r[i][j] = m.Re[i + j * m.R]; } return r; }
    static MatrixValue FromRows(List<int[]> rows, int cols, int offset = 1)
    {
        var m = new MatrixValue(rows.Count, cols);
        for (int i = 0; i < rows.Count; i++) for (int j = 0; j < cols; j++) m.Re[i + j * rows.Count] = rows[i][j] + offset;
        return m;
    }

    /// <summary>2-D convex hull (Andrew monotone chain): closed counter-clockwise loop of 0-based indices, starting at the lowest-x point.</summary>
    internal static List<int> Hull2(double[] x, double[] y)
    {
        int n = x.Length;
        var idx = Enumerable.Range(0, n).OrderBy(i => x[i]).ThenBy(i => y[i]).ToList();
        // drop duplicates
        var u = new List<int>(); foreach (var i in idx) if (u.Count == 0 || x[u[^1]] != x[i] || y[u[^1]] != y[i]) u.Add(i);
        if (u.Count < 3) { var r = new List<int>(u); if (r.Count > 0) r.Add(r[0]); return r; }
        double Cross(int o, int a, int b) => (x[a] - x[o]) * (y[b] - y[o]) - (y[a] - y[o]) * (x[b] - x[o]);
        var H = new List<int>();
        foreach (var i in u) { while (H.Count >= 2 && Cross(H[^2], H[^1], i) <= 0) H.RemoveAt(H.Count - 1); H.Add(i); }
        int lower = H.Count + 1;
        for (int k = u.Count - 2; k >= 0; k--) { int i = u[k]; while (H.Count >= lower && Cross(H[^2], H[^1], i) <= 0) H.RemoveAt(H.Count - 1); H.Add(i); }
        return H; // last == first
    }

    static void RegisterPhase11Geometry()
    {
        Reg("__delaunayn__", (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to __delaunayn__");
            var m = M(a[0], "__delaunayn__");
            var T = Delaunay(Rows(m));
            return Out(FromRows(T, m.C + 1));
        });
        Reg("convhulln", (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to convhulln");
            var m = M(a[0], "convhulln"); int d = m.C; var P = Rows(m);
            if (m.R <= d) throw new OctaveException("convhulln: dimension of points must be less than the number of points");
            if (d == 2)
            {
                var h = Hull2(P.Select(p => p[0]).ToArray(), P.Select(p => p[1]).ToArray());
                var e = new List<int[]>(); for (int k = 0; k + 1 < h.Count; k++) e.Add(new[] { h[k], h[k + 1] });
                double A = 0; for (int k = 0; k + 1 < h.Count; k++) A += P[h[k]][0] * P[h[k + 1]][1] - P[h[k + 1]][0] * P[h[k]][1];
                return Out(FromRows(e, 2), Num(Math.Abs(A) / 2));
            }
            var T = Delaunay(P);
            var faces = new Dictionary<string, (int[] f, int cnt)>(); double vol = 0, fact = 1;
            for (int k = 2; k <= d; k++) fact *= k;
            foreach (var s in T)
            {
                vol += Math.Abs(SimplexDet(P, s)) / fact;
                for (int k = 0; k <= d; k++)
                {
                    // face with orientation such that the normal points away from the omitted vertex
                    var f = s.Where((_, i) => i != k).ToArray();
                    if ((d - k) % 2 == 1) (f[0], f[1]) = (f[1], f[0]);
                    var key = string.Join(",", f.OrderBy(v => v));
                    faces[key] = faces.TryGetValue(key, out var ex) ? (ex.f, ex.cnt + 1) : (f, 1);
                }
            }
            var H = faces.Values.Where(v => v.cnt == 1).Select(v => v.f).ToList();
            // make outward oriented: compare with centroid
            var cen = new double[d]; for (int j = 0; j < d; j++) cen[j] = P.Average(p => p[j]);
            var P2 = P.Append(cen).ToArray(); int ci = P.Length;
            for (int i = 0; i < H.Count; i++)
            {
                var f = H[i]; var v = new[] { ci }.Concat(f).ToArray();
                if (SimplexDet(P2, v) < 0) { f = (int[])f.Clone(); (f[0], f[1]) = (f[1], f[0]); H[i] = f; }
            }
            return Out(FromRows(H, d), Num(vol));
        });
        Reg("__convhull2__", (it, a, n) =>
        {
            var x = M(a[0], "convhull"); var y = M(a[1], "convhull");
            var h = Hull2(x.Re, y.Re);
            double A = 0; for (int k = 0; k + 1 < h.Count; k++) A += x.Re[h[k]] * y.Re[h[k + 1]] - x.Re[h[k + 1]] * y.Re[h[k]];
            var H = new MatrixValue(h.Count, 1, h.Select(v => (double)v + 1).ToArray());
            return Out(H, Num(Math.Abs(A) / 2));
        });
        Reg("tsearch", (it, a, n) =>
        {
            if (a.Length != 5) throw new OctaveException("Invalid call to tsearch");
            var x = M(a[0], "tsearch"); var y = M(a[1], "tsearch"); var t = M(a[2], "tsearch"); var xi = M(a[3], "tsearch"); var yi = M(a[4], "tsearch");
            if (t.C != 3) throw new OctaveException("tsearch: T must be a matrix with 3 columns");
            if (xi.Numel != yi.Numel) throw new OctaveException("tsearch: XI and YI must be the same size");
            var r = new MatrixValue(xi.R, xi.C); int nt = t.R; const double eps = 1e-12;
            var tri = new (double x0, double y0, double a11, double a12, double a21, double a22, double minx, double maxx, double miny, double maxy)[nt];
            for (int k = 0; k < nt; k++)
            {
                int i0 = (int)t.Re[k] - 1, i1 = (int)t.Re[k + nt] - 1, i2 = (int)t.Re[k + 2 * nt] - 1;
                double dx1 = x.Re[i1] - x.Re[i0], dy1 = y.Re[i1] - y.Re[i0], dx2 = x.Re[i2] - x.Re[i0], dy2 = y.Re[i2] - y.Re[i0];
                double det = dx1 * dy2 - dx2 * dy1;
                tri[k] = (x.Re[i0], y.Re[i0], dy2 / det, -dx2 / det, -dy1 / det, dx1 / det,
                    Math.Min(x.Re[i0], Math.Min(x.Re[i1], x.Re[i2])), Math.Max(x.Re[i0], Math.Max(x.Re[i1], x.Re[i2])),
                    Math.Min(y.Re[i0], Math.Min(y.Re[i1], y.Re[i2])), Math.Max(y.Re[i0], Math.Max(y.Re[i1], y.Re[i2])));
            }
            for (int q = 0; q < xi.Numel; q++)
            {
                double px = xi.Re[q], py = yi.Re[q]; r.Re[q] = double.NaN;
                for (int k = 0; k < nt; k++)
                {
                    var T = tri[k];
                    if (px < T.minx - eps || px > T.maxx + eps || py < T.miny - eps || py > T.maxy + eps) continue;
                    double dx = px - T.x0, dy = py - T.y0;
                    double l1 = T.a11 * dx + T.a12 * dy, l2 = T.a21 * dx + T.a22 * dy;
                    if (l1 >= -eps && l2 >= -eps && l1 + l2 <= 1 + eps) { r.Re[q] = k + 1; break; }
                }
            }
            return Out(r);
        });
        Reg("tsearchn", (it, a, n) =>
        {
            if (a.Length != 3) throw new OctaveException("Invalid call to tsearchn");
            var x = M(a[0], "tsearchn"); var t = M(a[1], "tsearchn"); var xi = M(a[2], "tsearchn");
            if (x.C != xi.C) throw new OctaveException("tsearchn: number of columns of X and XI must match");
            if (t.Numel > 0 && t.Re.Max() > x.R) throw new OctaveException("tsearchn: triangulation T must not access points outside X");
            int d = x.C, nt = t.R, mi = xi.R; var P = Rows(x); var Q = Rows(xi);
            var idx = new MatrixValue(mi, 1); Array.Fill(idx.Re, double.NaN);
            var pm = new MatrixValue(mi, d + 1); Array.Fill(pm.Re, double.NaN);
            var left = Enumerable.Range(0, mi).ToList();
            for (int k = 0; k < nt && left.Count > 0; k++)
            {
                var v = Enumerable.Range(0, t.C).Select(j => (int)t.Re[k + j * nt] - 1).ToArray();
                var last = P[v[d]]; var A = new double[d, d]; // rows: T(i)-T(end); solve b*A = (P-T(end))
                for (int i = 0; i < d; i++) for (int j = 0; j < d; j++) A[j, i] = P[v[i]][j] - last[j];
                var still = new List<int>();
                foreach (var q in left)
                {
                    var rhs = new double[d]; for (int j = 0; j < d; j++) rhs[j] = Q[q][j] - last[j];
                    var b = SolveSmall(A, rhs);
                    if (b == null) { still.Add(q); continue; }
                    double s = b.Sum(); bool inside = b.All(z => z >= -1e-12) && 1 - s >= -1e-12;
                    if (!inside) { still.Add(q); continue; }
                    idx.Re[q] = k + 1;
                    for (int j = 0; j < d; j++) pm.Re[q + j * mi] = b[j];
                    pm.Re[q + d * mi] = 1 - s;
                }
                left = still;
            }
            return n <= 1 ? Out(idx) : Out(idx, pm);
        });
        Reg("__voronoi__", (it, a, n) =>
        {
            string caller = a.Length > 0 && a[0] is MatrixValue cm && cm.IsChar ? cm.AsString() : "__voronoi__";
            if (a.Length < 2) throw new OctaveException($"Invalid call to {caller}");
            var m = M(a[1], caller); int d = m.C; var P = Rows(m); int np = m.R;
            if (np <= d) throw new OctaveException($"{caller}: number of points must be greater than their dimension");
            var T = Delaunay(P);
            // circumcenters, merged when coincident
            var verts = new List<double[]>(); var vmap = new int[T.Count];
            double scale = 0; for (int j = 0; j < d; j++) scale = Math.Max(scale, P.Max(p => p[j]) - P.Min(p => p[j]));
            double tol = 1e-10 * Math.Max(scale, 1);
            for (int k = 0; k < T.Count; k++)
            {
                var s = new Simplex { V = T[k] }; Circum(P, s);
                int found = -1;
                for (int q = 0; q < verts.Count && found < 0; q++) { double dd = 0; for (int j = 0; j < d; j++) dd = Math.Max(dd, Math.Abs(verts[q][j] - s.C[j])); if (dd <= tol) found = q; }
                if (found < 0) { found = verts.Count; verts.Add(s.C); }
                vmap[k] = found;
            }
            // vertex order: farthest from the point centroid first (ties by coordinates)
            var cen = new double[d]; for (int j = 0; j < d; j++) cen[j] = P.Average(p => p[j]);
            var order = Enumerable.Range(0, verts.Count).OrderByDescending(q => Math.Round(verts[q].Select((v, j) => (v - cen[j]) * (v - cen[j])).Sum(), 10))
                .ThenBy(q => verts[q][0]).ThenBy(q => d > 1 ? verts[q][1] : 0).ToList();
            var rank = new int[verts.Count]; for (int q = 0; q < order.Count; q++) rank[order[q]] = q + 2; // 1 = Inf
            var C = new MatrixValue(verts.Count + 1, d);
            for (int j = 0; j < d; j++) C.Re[j * C.R] = double.PositiveInfinity;
            for (int q = 0; q < verts.Count; q++) for (int j = 0; j < d; j++) C.Re[rank[q] - 1 + j * C.R] = verts[q][j];
            // hull membership: point is on a boundary face
            var faceCnt = new Dictionary<string, int>();
            foreach (var s in T) for (int k = 0; k <= d; k++) { var key = string.Join(",", s.Where((_, i) => i != k).OrderBy(v => v)); faceCnt[key] = faceCnt.GetValueOrDefault(key) + 1; }
            var onHull = new bool[np];
            foreach (var (key, c) in faceCnt) if (c == 1) foreach (var s in key.Split(',')) onHull[int.Parse(s)] = true;
            var cells = new List<int>[np]; for (int i = 0; i < np; i++) cells[i] = new List<int>();
            for (int k = 0; k < T.Count; k++) foreach (var v in T[k]) if (!cells[v].Contains(rank[vmap[k]])) cells[v].Add(rank[vmap[k]]);
            var F = new CellValue(np, 1); var inf = new MatrixValue(np, 1);
            for (int i = 0; i < np; i++)
            {
                var c = cells[i];
                if (d == 2 && c.Count > 1)
                {
                    var pi = P[i];
                    c = c.OrderBy(q => Math.Atan2(C.Re[q - 1 + C.R] - pi[1], C.Re[q - 1] - pi[0])).ToList();
                }
                if (onHull[i]) { inf.Re[i] = 1; c.Insert(0, 1); }
                if (T.Count == 0) c = new List<int>();
                F.Data[i] = new MatrixValue(1, c.Count, c.Select(v => (double)v).ToArray());
            }
            return Out(C, F, it.CallFunction("logical", new Value[] { inf }, 1)[0]);
        });
    }
}
