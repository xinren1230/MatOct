// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Runtime;

/// <summary>Shape helpers for N-dimensional column-major arrays.</summary>
public static class NDArray
{
    public static int Prod(int[] d, int from = 0, int to = int.MaxValue)
    {
        int p = 1;
        for (int k = from; k < Math.Min(to, d.Length); k++) p *= d[k];
        return p;
    }

    public static int[] Pad(int[] d, int n)
    {
        if (d.Length >= n) return d;
        var r = new int[n];
        for (int k = 0; k < n; k++) r[k] = k < d.Length ? d[k] : 1;
        return r;
    }

    public static int[] Normalize(int[] d)
    {
        int n = d.Length;
        while (n > 2 && d[n - 1] == 1) n--;
        return Pad(d.Take(n).ToArray(), 2);
    }

    public static bool SameDims(int[] a, int[] b) => Normalize(a).SequenceEqual(Normalize(b));

    /// <summary>Linear index of subscripts (0-based) in dims.</summary>
    public static int Linear(int[] sub, int[] dims)
    {
        int lin = 0, stride = 1;
        for (int k = 0; k < sub.Length; k++) { lin += sub[k] * stride; stride *= k < dims.Length ? dims[k] : 1; }
        return lin;
    }

    /// <summary>Advance a subscript counter; returns false after the last element.</summary>
    public static bool Next(int[] sub, int[] dims)
    {
        for (int k = 0; k < sub.Length; k++)
        {
            if (++sub[k] < dims[k]) return true;
            sub[k] = 0;
        }
        return false;
    }

    public static ArrayValue Reshape(ArrayValue a, int[] dims)
    {
        if (Prod(dims) != a.Numel)
            throw new OctaveException($"reshape: can't reshape {a.DimString} array to {string.Join("x", dims)} array");
        var c = Clone(a);
        c.SetDims(dims);
        return c;
    }

    public static ArrayValue Clone(ArrayValue a) => a switch
    {
        MatrixValue m => m.Copy(),
        CellValue c => c.Copy(),
        StructValue s => s.Copy(),
        _ => a
    };

    /// <summary>New array of newDims with old content at the same subscripts, defaults elsewhere.</summary>
    public static ArrayValue ResizeND(ArrayValue a, int[] newDims)
    {
        int n = Math.Max(newDims.Length, a.NDims);
        var od = Pad(a.Dims, n); var nd = Pad(newDims, n);
        var t = a.EmptyLike(1, Prod(nd));
        if (t is MatrixValue tm && a is MatrixValue am) { tm.Kind = am.Kind; if (am.Im != null) tm.Im = new double[tm.Re.Length]; }
        if (a.Numel > 0)
        {
            var sub = new int[n];
            int src = 0;
            do
            {
                bool inside = true;
                for (int k = 0; k < n; k++) if (sub[k] >= nd[k]) { inside = false; break; }
                if (inside) t.SetFrom(Linear(sub, nd), a, src);
                src++;
            } while (Next(sub, od));
        }
        t.SetDims(nd);
        return t;
    }

    public static ArrayValue Permute(ArrayValue a, int[] perm, bool inverse = false)
    {
        int n = Math.Max(perm.Length, a.NDims);
        if (perm.Length < a.NDims) throw new OctaveException("permute: PERM does not contain all dimensions");
        var seen = new bool[n];
        foreach (var p in perm)
        {
            if (p < 0 || p >= n || seen[p]) throw new OctaveException("permute: PERM is not a valid permutation vector");
            seen[p] = true;
        }
        if (inverse)
        {
            var inv = new int[n];
            for (int k = 0; k < n; k++) inv[perm[k]] = k;
            perm = inv;
        }
        var od = Pad(a.Dims, n);
        var nd = new int[n];
        for (int k = 0; k < n; k++) nd[k] = od[perm[k]];
        var lin = new int[a.Numel];
        if (a.Numel > 0)
        {
            var sub = new int[n]; var osub = new int[n];
            int t = 0;
            do
            {
                for (int k = 0; k < n; k++) osub[perm[k]] = sub[k];
                lin[t++] = Linear(osub, od);
            } while (Next(sub, nd));
        }
        var r = a.Gather(lin, 1, lin.Length);
        r.SetDims(nd);
        return r;
    }

    public static ArrayValue Squeeze(ArrayValue a)
    {
        if (a.ND == null) return a;
        var d = a.Dims.Where(x => x != 1).ToList();
        while (d.Count < 2) d.Add(1);
        var c = Clone(a);
        c.SetDims(d.ToArray());
        return c;
    }

    /// <summary>Concatenate along dimension dim (1-based) for any number of dims.</summary>
    public static ArrayValue Cat(int dim, List<Value> items)
    {
        if (dim < 1) throw new OctaveException("cat: DIM must be a valid dimension");
        if (dim == 1 && items.All(v => v.NDims == 2)) return (ArrayValue)Concat.Vert(items);
        if (dim == 2 && items.All(v => v.NDims == 2)) return (ArrayValue)Concat.Horz(items);
        var arrs = items.Where(v => v.Numel > 0 || v.NDims > 2).ToList();
        if (arrs.Count == 0) return items.Count > 0 && items[0] is ArrayValue a0 ? a0 : MatrixValue.Empty();
        // pick a common type using the 2-D concat rules on one element of each
        var proto = Concat.Horz(arrs.Select(v => v is ArrayValue av && av.Numel > 0 ? (Value)av.Gather(new[] { 0 }, 1, 1) : v is ArrayValue ev && v.NDims > 2 ? (Value)ev.EmptyLike(0, 0) : v).ToList());
        int n = Math.Max(dim, arrs.Max(v => v.NDims));
        var baseD = Pad(arrs[0].Dims, n);
        int total = 0;
        foreach (var v in arrs)
        {
            var d = Pad(v.Dims, n);
            for (int k = 0; k < n; k++)
                if (k != dim - 1 && d[k] != baseD[k])
                    throw new OctaveException($"concatenation operator not implemented for '{string.Join("x", baseD)}' by '{string.Join("x", d)}' operations; dimension mismatch");
            total += d[dim - 1];
        }
        var outD = (int[])baseD.Clone(); outD[dim - 1] = total;
        var res = ((ArrayValue)proto).EmptyLike(1, Prod(outD));
        if (res is MatrixValue rm && proto is MatrixValue pm) { rm.Kind = pm.Kind; if (pm.Im != null) rm.Im = new double[rm.Re.Length]; }
        int offset = 0;
        foreach (var v in arrs)
        {
            var src = Coerce((ArrayValue)v, res);
            var d = Pad(v.Dims, n);
            if (src.Numel > 0)
            {
                var sub = new int[n]; int k = 0;
                do
                {
                    var tsub = (int[])sub.Clone(); tsub[dim - 1] += offset;
                    res.SetFrom(Linear(tsub, outD), src, k++);
                } while (Next(sub, d));
            }
            offset += d[dim - 1];
        }
        res.SetDims(outD);
        return res;
    }

    static ArrayValue Coerce(ArrayValue v, ArrayValue target)
    {
        if (target is CellValue && v is not CellValue) return new CellValue(1, 1, new Value[] { v });
        if (target is MatrixValue tm && v is MatrixValue vm && NumTypes.IsInt(tm.Kind) && vm.Kind != tm.Kind) return vm.CastTo(tm.Kind);
        if (target is MatrixValue tm2 && v is MatrixValue vm2 && tm2.Kind == NumKind.Single && vm2.Kind != NumKind.Single) return vm2.CastTo(NumKind.Single);
        return v;
    }

    /// <summary>Extract the 2-D page p (0-based, over dims 3..n) of an N-D array.</summary>
    public static ArrayValue Page(ArrayValue a, int p)
    {
        int pageSize = a.Dims[0] * a.Dims[1];
        var lin = Enumerable.Range(p * pageSize, pageSize).ToArray();
        return a.Gather(lin, a.Dims[0], a.Dims[1]);
    }

    /// <summary>Label like "(:,:,2,1)" for page p.</summary>
    public static string PageLabel(int[] dims, int p)
    {
        var sb = new System.Text.StringBuilder("(:,:");
        for (int k = 2; k < dims.Length; k++) { sb.Append(',').Append(p % dims[k] + 1); p /= dims[k]; }
        return sb.Append(')').ToString();
    }
}
