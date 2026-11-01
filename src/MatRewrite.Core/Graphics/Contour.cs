// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Graphics;

/// <summary>Marching-squares contouring on a rectilinear grid (Z is [row = y, col = x]).</summary>
public static class Contour
{
    /// <summary>Octave-like automatic levels: "nice" tick values strictly inside (min, max).</summary>
    public static double[] AutoLevels(double[,] z, int n = 10)
    {
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var v in z) if (double.IsFinite(v)) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
        if (!(hi > lo)) return double.IsFinite(lo) ? new[] { lo } : Array.Empty<double>();
        return NiceLevels(lo, hi, n);
    }

    public static double[] NiceLevels(double lo, double hi, int n)
    {
        double raw = (hi - lo) / Math.Max(n, 1);
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double f = raw / mag;
        double step = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * mag;
        var r = new List<double>();
        for (double v = Math.Ceiling(lo / step) * step; v <= hi + step * 1e-9; v += step)
        {
            double q = Math.Round(v / step) * step;
            if (q > lo + step * 1e-9 && q < hi - step * 1e-9 || r.Count == 0 && n <= 1) r.Add(q);
        }
        if (r.Count == 0) r.Add((lo + hi) / 2);
        return r.ToArray();
    }

    /// <summary>Contour lines for one level, chained into polylines.</summary>
    public static List<(double[] x, double[] y)> Lines(double[] xs, double[] ys, double[,] z, double level)
    {
        int R = z.GetLength(0), C = z.GetLength(1);
        var segs = new List<((double, double) a, (double, double) b)>();
        (double, double) P(int i0, int j0, int i1, int j1)
        {
            double v0 = z[i0, j0], v1 = z[i1, j1];
            double t = v1 == v0 ? 0.5 : (level - v0) / (v1 - v0);
            return (xs[j0] + t * (xs[j1] - xs[j0]), ys[i0] + t * (ys[i1] - ys[i0]));
        }
        for (int i = 0; i + 1 < R; i++)
            for (int j = 0; j + 1 < C; j++)
            {
                double a = z[i, j], b = z[i, j + 1], c = z[i + 1, j + 1], d = z[i + 1, j];
                if (!(double.IsFinite(a) && double.IsFinite(b) && double.IsFinite(c) && double.IsFinite(d))) continue;
                int code = (a >= level ? 1 : 0) | (b >= level ? 2 : 0) | (c >= level ? 4 : 0) | (d >= level ? 8 : 0);
                if (code == 0 || code == 15) continue;
                var eB = P(i, j, i, j + 1);         // bottom a-b
                var eR = P(i, j + 1, i + 1, j + 1); // right b-c
                var eT = P(i + 1, j, i + 1, j + 1); // top d-c
                var eL = P(i, j, i + 1, j);         // left a-d
                double center = (a + b + c + d) / 4;
                switch (code)
                {
                    case 1: case 14: segs.Add((eL, eB)); break;
                    case 2: case 13: segs.Add((eB, eR)); break;
                    case 3: case 12: segs.Add((eL, eR)); break;
                    case 4: case 11: segs.Add((eR, eT)); break;
                    case 6: case 9: segs.Add((eB, eT)); break;
                    case 7: case 8: segs.Add((eL, eT)); break;
                    case 5: if (center >= level) { segs.Add((eL, eT)); segs.Add((eB, eR)); } else { segs.Add((eL, eB)); segs.Add((eR, eT)); } break;
                    case 10: if (center >= level) { segs.Add((eL, eB)); segs.Add((eR, eT)); } else { segs.Add((eL, eT)); segs.Add((eB, eR)); } break;
                }
            }
        segs.RemoveAll(sg => Key(sg.a) == Key(sg.b)); // degenerate (level exactly at a grid vertex)
        return Chain(segs);
    }

    static (long, long) Key((double x, double y) p) => ((long)Math.Round(p.x * 1e9), (long)Math.Round(p.y * 1e9));

    static List<(double[] x, double[] y)> Chain(List<((double x, double y) a, (double x, double y) b)> segs)
    {
        var used = new bool[segs.Count];
        var at = new Dictionary<(long, long), List<int>>();
        for (int k = 0; k < segs.Count; k++)
            foreach (var p in new[] { segs[k].a, segs[k].b })
            {
                var key = Key(p);
                if (!at.TryGetValue(key, out var l)) at[key] = l = new List<int>();
                l.Add(k);
            }
        var res = new List<(double[], double[])>();
        for (int s = 0; s < segs.Count; s++)
        {
            if (used[s]) continue;
            used[s] = true;
            var line = new LinkedList<(double x, double y)>();
            line.AddLast(segs[s].a); line.AddLast(segs[s].b);
            for (int dir = 0; dir < 2; dir++)
            {
                while (true)
                {
                    var end = dir == 0 ? line.Last!.Value : line.First!.Value;
                    int next = -1;
                    foreach (var k in at[Key(end)]) if (!used[k]) { next = k; break; }
                    if (next < 0) break;
                    used[next] = true;
                    var other = Key(segs[next].a) == Key(end) ? segs[next].b : segs[next].a;
                    if (dir == 0) line.AddLast(other); else line.AddFirst(other);
                }
            }
            res.Add((line.Select(p => p.x).ToArray(), line.Select(p => p.y).ToArray()));
        }
        return res;
    }

    public static List<(double level, double[] x, double[] y)> All(double[] xs, double[] ys, double[,] z, double[] levels)
    {
        var r = new List<(double, double[], double[])>();
        foreach (var lv in levels) foreach (var (x, y) in Lines(xs, ys, z, lv)) r.Add((lv, x, y));
        return r;
    }
}
