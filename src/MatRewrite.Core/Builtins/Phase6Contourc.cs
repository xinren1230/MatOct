using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static void RegisterContourc()
        {
            Reg("__contourc__", (it, a, n) =>
            {
                if (a.Length != 4) throw new OctaveException("Invalid call to __contourc__");
                var x = M(a[0], "__contourc__").Re; var y = M(a[1], "__contourc__").Re;
                var z = M(a[2], "__contourc__"); var lev = M(a[3], "__contourc__").Re;
                int R = z.R, C = z.C;
                var outc = new List<double[]>();
                foreach (var L in lev)
                {
                    // edge key: horizontal edge (i,j)-(i,j+1) => (i*C+j)*2, vertical (i,j)-(i+1,j) => (i*C+j)*2+1
                    double Z(int i, int j) => z.Re[i + j * R];
                    bool Up(int i, int j) => Z(i, j) >= L;
                    (double, double) Pt(int key)
                    {
                        int b = key / 2, i = b / C, j = b % C;
                        if (key % 2 == 0) { double f = (L - Z(i, j)) / (Z(i, j + 1) - Z(i, j)); return (x[j] + f * (x[j + 1] - x[j]), y[i]); }
                        else { double f = (L - Z(i, j)) / (Z(i + 1, j) - Z(i, j)); return (x[j], y[i] + f * (y[i + 1] - y[i])); }
                    }
                    var adj = new Dictionary<int, List<int>>();
                    void Link(int p, int q) { if (!adj.TryGetValue(p, out var l1)) adj[p] = l1 = new(); l1.Add(q); if (!adj.TryGetValue(q, out var l2)) adj[q] = l2 = new(); l2.Add(p); }
                    for (int i = 0; i < R - 1; i++)
                        for (int j = 0; j < C - 1; j++)
                        {
                            if (double.IsNaN(Z(i, j)) || double.IsNaN(Z(i, j + 1)) || double.IsNaN(Z(i + 1, j)) || double.IsNaN(Z(i + 1, j + 1))) continue;
                            int bottom = (i * C + j) * 2, top = ((i + 1) * C + j) * 2, left = (i * C + j) * 2 + 1, right = (i * C + j + 1) * 2 + 1;
                            var cr = new List<int>();
                            if (Up(i, j) != Up(i, j + 1)) cr.Add(bottom);
                            if (Up(i, j + 1) != Up(i + 1, j + 1)) cr.Add(right);
                            if (Up(i + 1, j + 1) != Up(i + 1, j)) cr.Add(top);
                            if (Up(i + 1, j) != Up(i, j)) cr.Add(left);
                            if (cr.Count == 2) Link(cr[0], cr[1]);
                            else if (cr.Count == 4)
                            {
                                double c = (Z(i, j) + Z(i, j + 1) + Z(i + 1, j) + Z(i + 1, j + 1)) / 4;
                                if ((c >= L) == Up(i, j)) { Link(bottom, right); Link(top, left); }
                                else { Link(bottom, left); Link(top, right); }
                            }
                        }
                    var used = new HashSet<(int, int)>();
                    bool Take(int p, int q) { var k = p < q ? (p, q) : (q, p); return used.Add(k); }
                    List<int> Walk(int start)
                    {
                        var path = new List<int> { start }; int cur = start;
                        while (true)
                        {
                            int nxt = -1;
                            foreach (var q in adj[cur]) if (Take(cur, q)) { nxt = q; break; }
                            if (nxt < 0) break;
                            path.Add(nxt); cur = nxt;
                            if (nxt == start) break;
                        }
                        return path;
                    }
                    var paths = new List<List<int>>();
                    foreach (var kv in adj.OrderBy(k => k.Key)) if (kv.Value.Count == 1 && adj[kv.Key].Any(q => !used.Contains(kv.Key < q ? (kv.Key, q) : (q, kv.Key)))) paths.Add(Walk(kv.Key));
                    foreach (var kv in adj.OrderBy(k => k.Key)) if (kv.Value.Any(q => !used.Contains(kv.Key < q ? (kv.Key, q) : (q, kv.Key)))) paths.Add(Walk(kv.Key));
                    foreach (var p in paths)
                    {
                        var pts = p.Select(Pt).ToList();
                        var dedup = new List<(double, double)>();
                        dedup.AddRange(pts);
                        if (dedup.Count < 2) continue;
                        if (p[0] != p[^1])
                        {
                            var (sx, sy) = dedup[0]; var (ex, ey) = dedup[^1];
                            if (ex < sx || (ex == sx && ey > sy)) dedup.Reverse();
                        }
                        outc.Add(new[] { L, dedup.Count });
                        foreach (var q in dedup) outc.Add(new[] { q.Item1, q.Item2 });
                    }
                }
                var r = new MatrixValue(2, outc.Count);
                for (int k = 0; k < outc.Count; k++) { r.Re[2 * k] = outc[k][0]; r.Re[2 * k + 1] = outc[k][1]; }
                return Out(r);
            });
        }
    }
}
