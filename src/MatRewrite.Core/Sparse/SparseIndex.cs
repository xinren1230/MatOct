// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Sparse;

public static class SparseIndex
{
    public static Value Read(SparseValue s, Value[] args)
    {
        args = Indexing.Normalize(args);
        if (args.Length == 2)
        {
            var ri = Indexing.Convert(args[0], s.R, "index", false);
            var ci = Indexing.Convert(args[1], s.C, "index", false);
            foreach (var i in ri.I) if (i >= s.R) throw new OctaveException($"index ({i + 1},_): out of bound {s.R} (dimensions are {s.R}x{s.C})");
            foreach (var j in ci.I) if (j >= s.C) throw new OctaveException($"index (_,{j + 1}): out of bound {s.C} (dimensions are {s.R}x{s.C})");
            int nr = ri.I.Length, nc = ci.I.Length;
            if (nr == 1 && nc == 1) return SparseValue.FromDense(Scalar(s, ri.I[0], ci.I[0]));
            // row map: original row -> positions in result
            Dictionary<int, List<int>>? rmap = null;
            if (!ri.Colon)
            {
                rmap = new();
                for (int k = 0; k < nr; k++) { if (!rmap.TryGetValue(ri.I[k], out var l)) rmap[ri.I[k]] = l = new(); l.Add(k); }
            }
            var I = new List<int>(); var J = new List<int>(); var V = new List<double>(); var Vi = s.Im != null ? new List<double>() : null;
            for (int jj = 0; jj < nc; jj++)
            {
                int j = ci.I[jj];
                for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++)
                {
                    if (rmap == null) { I.Add(s.Ri[p]); J.Add(jj); V.Add(s.Re[p]); Vi?.Add(s.Im![p]); }
                    else if (rmap.TryGetValue(s.Ri[p], out var l))
                        foreach (var k in l) { I.Add(k); J.Add(jj); V.Add(s.Re[p]); Vi?.Add(s.Im![p]); }
                }
            }
            return SparseValue.FromTriplets(nr, nc, I, J, V, Vi, s.Logical);
        }
        if (args.Length == 1 && args[0] is MatrixValue m && m.Numel == 1 && !m.IsLogical)
        {
            int k = (int)m.Re[0] - 1;
            if (k < 0 || k >= s.Numel || m.Re[0] != Math.Floor(m.Re[0])) return Fallback(s, args);
            return SparseValue.FromDense(Scalar(s, k % s.R, k / s.R));
        }
        if (args.Length == 1 && args[0] is MagicColon)
        {
            // A(:) -> numel x 1 sparse
            var I = new List<int>(); var J = new List<int>(); var V = new List<double>(); var Vi = s.Im != null ? new List<double>() : null;
            for (int j = 0; j < s.C; j++) for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++) { I.Add(s.Ri[p] + j * s.R); J.Add(0); V.Add(s.Re[p]); Vi?.Add(s.Im![p]); }
            return SparseValue.FromTriplets(s.R * s.C, 1, I, J, V, Vi, s.Logical);
        }
        return Fallback(s, args);
    }

    static MatrixValue Scalar(SparseValue s, int i, int j)
    {
        var z = s.Get(i, j);
        var m = new MatrixValue(1, 1, new[] { z.Real }, s.Im != null && z.Imaginary != 0 ? new[] { z.Imaginary } : null, s.Logical ? NumKind.Logical : NumKind.Double);
        return m;
    }

    static Value Fallback(SparseValue s, Value[] args)
    {
        var r = Indexing.Read(s.ToDense(), args);
        return r is MatrixValue rm && rm.ND == null ? SparseValue.FromDense(rm) : r;
    }

    public static Value Assign(SparseValue s, Value[] args, ArrayValue rhs)
    {
        args = Indexing.Normalize(args);
        MatrixValue? rv = rhs as MatrixValue ?? null;
        if (rv != null && rv.Numel == 1 && args.Length == 2 && args[0] is MatrixValue a0 && a0.Numel == 1 && !a0.IsLogical && args[1] is MatrixValue a1 && a1.Numel == 1 && !a1.IsLogical)
        {
            int i = (int)a0.Re[0] - 1, j = (int)a1.Re[0] - 1;
            if (i >= 0 && j >= 0 && i < s.R && j < s.C && a0.Re[0] == i + 1 && a1.Re[0] == j + 1 && !double.IsNaN(rv.Re[0]))
            {
                var c = s.Copy();
                c.Set(i, j, rv.Re[0], rv.Im?[0] ?? 0);
                return c;
            }
        }
        var d = Indexing.Assign(s.ToDense(), args, rhs);
        return d is MatrixValue dm && dm.ND == null ? SparseValue.FromDense(dm, s.Logical && dm.IsLogical) : d;
    }

    /// <summary>Assign a sparse rhs into a dense/other container (Octave keeps the container's type).</summary>
    public static ArrayValue DenseRhs(Value rhs) => rhs is SparseValue sv ? sv.ToDense() : (ArrayValue)rhs;
}
