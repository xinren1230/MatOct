using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static void RegisterPhase6()
        {
            Reg("__lin_interpn__", (it, a, n) =>
            {
                if (a.Length < 3 || a.Length % 2 == 0) throw new OctaveException("Invalid call to __lin_interpn__");
                int nd = (a.Length - 1) / 2;
                var v = M(a[nd], "__lin_interpn__");
                var vd = v.Dims.ToList(); while (vd.Count < nd) vd.Add(1);
                var grids = new double[nd][];
                for (int k = 0; k < nd; k++)
                {
                    var g = M(a[k], "__lin_interpn__");
                    if (g.Numel == vd[k]) grids[k] = g.Re.ToArray();
                    else
                    {
                        var ga = new double[vd[k]]; int stride = 1; for (int q = 0; q < k; q++) stride *= vd[q];
                        for (int i = 0; i < vd[k]; i++) ga[i] = g.Re[i * stride];
                        grids[k] = ga;
                    }
                }
                var qs = new MatrixValue[nd];
                for (int k = 0; k < nd; k++) qs[k] = M(a[nd + 1 + k], "__lin_interpn__");
                int nq = qs[0].Numel;
                var res = new double[nq];
                var strides = new int[nd]; strides[0] = 1; for (int k = 1; k < nd; k++) strides[k] = strides[k - 1] * vd[k - 1];
                var lo = new int[nd]; var fr = new double[nd];
                for (int q = 0; q < nq; q++)
                {
                    bool bad = false;
                    for (int k = 0; k < nd && !bad; k++)
                    {
                        double xq = qs[k].Numel == 1 ? qs[k].Re[0] : qs[k].Re[q]; var g = grids[k];
                        if (g.Length == 1) { if (xq != g[0]) bad = true; lo[k] = 0; fr[k] = 0; continue; }
                        bool inc = g[^1] >= g[0];
                        double mn = inc ? g[0] : g[^1], mx = inc ? g[^1] : g[0];
                        if (double.IsNaN(xq) || xq < mn || xq > mx) { bad = true; break; }
                        int lo_ = 0, hi = g.Length - 1;
                        while (hi - lo_ > 1) { int mid = (lo_ + hi) / 2; if (inc ? g[mid] <= xq : g[mid] >= xq) lo_ = mid; else hi = mid; }
                        lo[k] = lo_; fr[k] = (xq - g[lo_]) / (g[lo_ + 1] - g[lo_]);
                    }
                    if (bad) { res[q] = double.NaN; continue; }
                    double acc = 0;
                    for (int c = 0; c < (1 << nd); c++)
                    {
                        double w = 1; int off = 0;
                        for (int k = 0; k < nd; k++)
                        {
                            int bit = (c >> k) & 1;
                            if (grids[k].Length == 1 && bit == 1) { w = 0; break; }
                            w *= bit == 1 ? fr[k] : 1 - fr[k];
                            off += (lo[k] + bit) * strides[k];
                        }
                        if (w != 0) acc += w * v.Re[off];
                    }
                    res[q] = acc;
                }
                var r = new MatrixValue(qs[0].R, qs[0].C, res);
                if (qs[0].ND != null) r.SetDims((int[])qs[0].Dims.Clone());
                return Out(r);
            });
            RegisterContourc();
            RegG("__request_drawnow__", (it, a, n) => Array.Empty<Value>());
            Reg("cellindexmat", (it, a, n) =>
            {
                if (a.Length < 1) throw new OctaveException("Invalid call to cellindexmat");
                var c = a[0] as CellValue ?? throw new OctaveException("cellindexmat: X must be a cell array");
                var idx = a.Skip(1).ToArray();
                var outv = new Value[c.Numel];
                for (int k = 0; k < c.Numel; k++)
                {
                    var e = c.Data[k] as ArrayValue ?? throw new OctaveException("cellindexmat: element is not indexable");
                    outv[k] = Indexing.Read(e, idx);
                }
                var r = new CellValue(c.R, c.C, outv);
                if (c.ND != null) r.SetDims(c.Dims);
                return Out(r);
            });
        }
    }
}
