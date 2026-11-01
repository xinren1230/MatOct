// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Runtime;

/// <summary>Octave indexing semantics: A(I), A(I,J), logical masks, ':' and growth on assignment.</summary>
public static class Indexing
{
    internal sealed class Idx
    {
        public int[] I = Array.Empty<int>();
        public bool Colon;
        public int R, C;     // shape of the index value
        public bool Logical;
        public int Max => I.Length == 0 ? -1 : I.Max();
    }

    internal static Idx Convert(Value v, int extent, string ctx, bool allowOutOfRange)
    {
        if (v is Sparse.SparseValue spv0) v = spv0.ToDense();
        if (v is MagicColon) return new Idx { I = Enumerable.Range(0, extent).ToArray(), Colon = true, R = extent, C = 1 };
        if (v is MatrixValue m)
        {
            if (m.IsChar && m.Numel == 1 && m.Re[0] == ':')
                return new Idx { I = Enumerable.Range(0, extent).ToArray(), Colon = true, R = extent, C = 1 };
            if (m.IsLogical)
            {
                var list = new List<int>();
                for (int k = 0; k < m.Numel; k++) if (m.Re[k] != 0) list.Add(k);
                if (!allowOutOfRange && list.Count > 0 && list[^1] >= extent)
                    throw new OctaveException($"{ctx}: out of bound {extent} (dimensions are {ctx})", "Octave:index-out-of-bounds");
                bool row = m.R == 1 && m.C != 1;
                return new Idx { I = list.ToArray(), R = row ? 1 : list.Count, C = row ? list.Count : 1, Logical = true };
            }
            var arr = new int[m.Numel];
            for (int k = 0; k < arr.Length; k++)
            {
                double d = m.Re[k];
                if (d != Math.Floor(d))
                    throw new OctaveException($"subscript indices must be either positive integers or logicals", "Octave:index-out-of-bounds");
                if (d < 1)
                    throw new OctaveException(d == 0 ? "index (0): out of bound; value 0 out of bound " + extent
                        : $"index ({d}): out of bound; value {d} out of bound {extent}", "Octave:index-out-of-bounds");
                arr[k] = (int)d - 1;
            }
            return new Idx { I = arr, R = m.R, C = m.C };
        }
        throw new OctaveException($"subscript indices must be either positive integers or logicals");
    }

    static OctaveException OOB(int idx, int extent, ArrayValue a, int pos, int n) =>
        n == 1
            ? new OctaveException($"index ({idx + 1}): out of bound {extent} (dimensions are {a.R}x{a.C})", "Octave:index-out-of-bounds")
            : new OctaveException($"{(pos == 0 ? $"index ({idx + 1},_)" : $"index (_,{idx + 1})")}: out of bound {extent} (dimensions are {a.R}x{a.C})", "Octave:index-out-of-bounds");

    /// <summary>Normalize >2 subscripts: trailing ones must be 1 or ':'.</summary>
    static bool IsOne(Value a) => a is MatrixValue m && m.Numel == 1 && m.Re[0] == 1 && !m.IsLogical;

    /// <summary>Drop trailing subscripts equal to 1 (or ':' over a singleton dim) when they are redundant.</summary>
    internal static Value[] Normalize(Value[] args, ArrayValue? a = null)
    {
        int n = args.Length;
        int nd = a?.NDims ?? 2;
        bool canDropColon = a != null && a.Numel > 0;
        while (n > 2 && n > nd && (IsOne(args[n - 1]) || (canDropColon && args[n - 1] is MagicColon))) n--;
        return n == args.Length ? args : args.Take(n).ToArray();
    }

    /// <summary>Dims of a folded to n subscripts (last absorbs the rest).</summary>
    static int[] Fold(int[] d, int n)
    {
        var r = new int[n];
        for (int k = 0; k < n; k++)
            r[k] = k < n - 1 ? (k < d.Length ? d[k] : 1) : NDArray.Prod(d, k);
        return r;
    }

    static ArrayValue ReadND(ArrayValue a, Value[] args)
    {
        int n = args.Length;
        var fd = Fold(a.Dims, n);
        var idx = new int[n][];
        for (int k = 0; k < n; k++)
        {
            var ix = Convert(args[k], fd[k], "index", false);
            foreach (var v in ix.I)
                if (v >= fd[k])
                    throw new OctaveException($"index ({string.Join(",", Enumerable.Range(0, n).Select(q => q == k ? (v + 1).ToString() : "_"))}): out of bound; value {v + 1} out of bound {fd[k]}", "Octave:index-out-of-bounds");
            idx[k] = ix.I;
        }
        var od = idx.Select(x => x.Length).ToArray();
        int total = NDArray.Prod(od);
        var lin = new int[total];
        if (total > 0)
        {
            var sub = new int[n]; var src = new int[n]; int t = 0;
            do
            {
                for (int k = 0; k < n; k++) src[k] = idx[k][sub[k]];
                lin[t++] = NDArray.Linear(src, fd);
            } while (NDArray.Next(sub, od));
        }
        var r = a.Gather(lin, 1, total);
        r.SetDims(od);
        return r;
    }

    static ArrayValue AssignND(ArrayValue a, Value[] args, ArrayValue rhs, NumKind? kind)
    {
        int n = args.Length;
        bool aEmpty = a.Numel == 0;
        var fd = Fold(a.Dims, n);
        if (n < a.NDims && !aEmpty) { /* folded assignment: no growth on the folded dim */ }
        var rd = rhs.Dims.Where(x => x != 1).ToList();
        var idx = new int[n][];
        int rk = 0;
        for (int k = 0; k < n; k++)
        {
            int ext = fd[k];
            if (args[k] is MagicColon && aEmpty)
            {
                // take the extent from the next non-singleton rhs dim
                var rdims = rhs.Dims;
                ext = rhs.Numel == 1 ? 1 : (k < rdims.Length ? rdims[k] : 1);
            }
            idx[k] = Convert(args[k], ext, "index", true).I;
        }
        _ = rd; _ = rk;
        var cnt = idx.Select(x => x.Length).ToArray();
        int count = NDArray.Prod(cnt);
        if (rhs.Numel != 1 && rhs.Numel != count)
            throw new OctaveException($"=: nonconformant arguments (op1 is {string.Join("x", cnt)}, op2 is {rhs.DimString})");
        var nd = new int[n];
        for (int k = 0; k < n; k++) nd[k] = Math.Max(fd[k], idx[k].Length == 0 ? 0 : idx[k].Max() + 1);
        ArrayValue target;
        if (!nd.SequenceEqual(fd))
        {
            if (n < a.NDims && !aEmpty) throw new OctaveException("Octave:index out of bound; resizing a folded N-D array is not allowed", "Octave:index-out-of-bounds");
            target = NDArray.ResizeND(a, nd);
        }
        else { target = Clone(a); }
        var full = nd;
        if (count > 0)
        {
            var sub = new int[n]; var dst = new int[n]; int t = 0;
            do
            {
                for (int k = 0; k < n; k++) dst[k] = idx[k][sub[k]];
                target.SetFrom(NDArray.Linear(dst, full), rhs, rhs.Numel == 1 ? 0 : t);
                t++;
            } while (NDArray.Next(sub, cnt));
        }
        if (n >= a.NDims) target.SetDims(nd); else target.SetDims(a.Dims);
        if (target is MatrixValue tm && kind != null) tm.CastInPlace(kind.Value);
        return Narrow(target);
    }

    static ArrayValue DeleteND(ArrayValue a, Value[] args)
    {
        int n = args.Length;
        var fd = Fold(a.Dims, n);
        int which = -1;
        for (int k = 0; k < n; k++)
            if (!(args[k] is MagicColon || IsFull(args[k], fd[k])))
            {
                if (which >= 0) throw new OctaveException("a null assignment can only have one non-colon index");
                which = k;
            }
        if (which < 0) return a.EmptyLike(0, 0);
        var del = new HashSet<int>(Convert(args[which], fd[which], "index", false).I);
        var keepK = Enumerable.Range(0, fd[which]).Where(i => !del.Contains(i)).ToArray();
        var od = (int[])fd.Clone(); od[which] = keepK.Length;
        int total = NDArray.Prod(od);
        var lin = new int[total];
        if (total > 0)
        {
            var sub = new int[n]; var src = new int[n]; int t = 0;
            do
            {
                Array.Copy(sub, src, n); src[which] = keepK[sub[which]];
                lin[t++] = NDArray.Linear(src, fd);
            } while (NDArray.Next(sub, od));
        }
        var r = a.Gather(lin, 1, total);
        r.SetDims(od);
        return r;
    }

    public static ArrayValue Read(ArrayValue a, Value[] args)
    {
        var res = ReadRaw(a, args);
        // Octave narrows complex results whose imaginary parts are all zero
        if (!ReferenceEquals(res, a) && res is MatrixValue m && m.Im != null) m.Narrow();
        return res;
    }

    static ArrayValue ReadRaw(ArrayValue a, Value[] args)
    {
        args = Normalize(args, a);
        if (args.Length == 0) return a;
        if (args.Length > 2 || (args.Length == 2 && a.ND != null)) return ReadND(a, args);
        if (args.Length == 1)
        {
            int n = a.Numel;
            var ix = Convert(args[0], n, "index", false);
            foreach (var k in ix.I) if (k >= n) throw OOB(k, n, a, 0, 1);
            int r, c;
            if (ix.Colon) { r = n; c = 1; }
            else if (ix.Logical)
            {
                // mask on a vector keeps the source orientation
                if (a.R == 1 && a.C >= 1 && (args[0] as MatrixValue)!.R == 1) { r = 1; c = ix.I.Length; }
                else if (a.R == 1 && args[0] is MatrixValue lm && (lm.R == 1 || lm.C == 1)) { r = 1; c = ix.I.Length; }
                else { r = ix.I.Length; c = 1; }
            }
            else if ((ix.R == 1 || ix.C == 1) && !(args[0] is MatrixValue ndm && ndm.ND != null) && (a.R == 1 || a.C == 1) && !(a.R == 1 && a.C == 1))
            {
                if (a.R == 1) { r = 1; c = ix.I.Length; } else { r = ix.I.Length; c = 1; }
            }
            else { r = ix.R; c = ix.C; }
            var gres = a.Gather(ix.I, r, c);
            if (!ix.Colon && !ix.Logical && args[0] is MatrixValue ixm && ixm.ND != null) gres.SetDims((int[])ixm.Dims.Clone());
            return gres;
        }
        var ri = Convert(args[0], a.R, "index", false);
        var ci = Convert(args[1], a.C, "index", false);
        foreach (var k in ri.I) if (k >= a.R) throw OOB(k, a.R, a, 0, 2);
        foreach (var k in ci.I) if (k >= a.C) throw OOB(k, a.C, a, 1, 2);
        var lin = new int[ri.I.Length * ci.I.Length];
        int t = 0;
        foreach (var j in ci.I) foreach (var i in ri.I) lin[t++] = i + j * a.R;
        return a.Gather(lin, ri.I.Length, ci.I.Length);
    }

    /// <summary>A(args) = rhs. 'a' may be null (undefined variable). Returns the new array.</summary>
    public static ArrayValue Assign(ArrayValue? a, Value[] args, ArrayValue rhs)
    {
        args = Normalize(args, a);
        bool deleting = a != null && rhs is MatrixValue dm && dm.R == 0 && dm.C == 0 && !dm.IsChar && dm.Kind == NumKind.Double;
        if (deleting) return Delete(a!, args);
        a ??= rhs.EmptyLike(0, 0);
        if (a is CellValue && rhs is MatrixValue && !deleting) rhs = new CellValue(1, 1, new Value[] { rhs });
        var kind = ResultKind(a, rhs);
        a = Coerce(a, rhs);
        if (args.Length > 2 || (args.Length == 2 && a.ND != null)) return AssignND(a, args, rhs, kind);
        var res = Assign2(a, args, rhs);
        if (kind != null && res is MatrixValue rm2) rm2.CastInPlace(kind.Value);
        return res;
    }

    static NumKind? ResultKind(ArrayValue a, ArrayValue rhs)
    {
        if (a is not MatrixValue am || rhs is not MatrixValue rm) return null;
        if (am.IsInteger) return am.Kind;
        if (rm.IsInteger) return rm.Kind;
        if (am.IsSingle || rm.IsSingle) return NumKind.Single;
        return null;
    }

    static ArrayValue Assign2(ArrayValue a, Value[] args, ArrayValue rhs)
    {
        if (args.Length == 1)
        {
            int n = a.Numel;
            var ix = Convert(args[0], n, "index", true);
            int count = ix.I.Length;
            if (rhs.Numel != 1 && rhs.Numel != count)
                throw new OctaveException($"=: nonconformant arguments (op1 is 1x{count}, op2 is {rhs.R}x{rhs.C})");
            int max = ix.Max;
            ArrayValue target;
            if (max >= n)
            {
                int nr, nc;
                if (a.Numel == 0 && a.R == 0 && a.C == 0) { nr = 1; nc = max + 1; if (rhs.C == 1 && rhs.R > 1 && ix.Colon == false && ix.C == 1 && ix.R > 1) { nr = max + 1; nc = 1; } }
                else if (a.R == 1) { nr = 1; nc = max + 1; }
                else if (a.C == 1) { nr = max + 1; nc = 1; }
                else throw new OctaveException($"Octave:index out of bound; value {max + 1} out of bound {n}", "Octave:index-out-of-bounds");
                target = a.Resized(nr, nc);
            }
            else target = Clone(a);
            for (int k = 0; k < count; k++) target.SetFrom(ix.I[k], rhs, rhs.Numel == 1 ? 0 : k);
            return Narrow(target);
        }
        else
        {
            bool aEmpty = a.Numel == 0;
            var ri = Convert(args[0], args[0] is MagicColon && aEmpty ? Math.Max(rhs.R, a.R) : a.R, "index", true);
            var ci = Convert(args[1], args[1] is MagicColon && aEmpty ? (args[0] is MagicColon ? rhs.C : (rhs.Numel / Math.Max(1, ri.I.Length))) : a.C, "index", true);
            if (args[0] is MagicColon && aEmpty && !(args[1] is MagicColon))
                ri = Convert(args[0], rhs.Numel / Math.Max(1, ci.I.Length) == 0 ? rhs.R : (rhs.Numel == 1 ? 1 : rhs.Numel / Math.Max(1, ci.I.Length)), "index", true);
            int count = ri.I.Length * ci.I.Length;
            if (rhs.Numel != 1 && rhs.Numel != count)
                throw new OctaveException($"=: nonconformant arguments (op1 is {ri.I.Length}x{ci.I.Length}, op2 is {rhs.R}x{rhs.C})");
            int nr = Math.Max(a.R, ri.Max + 1), nc = Math.Max(a.C, ci.Max + 1);
            var target = (nr != a.R || nc != a.C) ? a.Resized(nr, nc) : Clone(a);
            int t = 0;
            foreach (var j in ci.I)
                foreach (var i in ri.I)
                    target.SetFrom(i + j * nr, rhs, rhs.Numel == 1 ? 0 : t++);
            return Narrow(target);
        }
    }


    static ArrayValue Narrow(ArrayValue a) => a is MatrixValue m ? m.Narrow() : a;

    static ArrayValue Clone(ArrayValue a) => a switch
    {
        MatrixValue m => m.Copy(),
        CellValue c => c.Copy(),
        StructValue s => s.Copy(),
        _ => a
    };

    /// <summary>Type promotion of the target when assigning rhs into it.</summary>
    static ArrayValue Coerce(ArrayValue a, ArrayValue rhs)
    {
        if (a.GetType() == rhs.GetType())
        {
            if (a is MatrixValue am && rhs is MatrixValue rm && am.Kind != rm.Kind)
            {
                var c = am.Copy();
                c.Kind = (am.Numel == 0) ? rm.Kind : NumKind.Double;
                if (am.IsChar && rm.IsChar) c.Kind = NumKind.Char;
                return c;
            }
            return a;
        }
        if (a.Numel == 0 && a is MatrixValue) return rhs.EmptyLike(a.R, a.C);
        if (a is CellValue) throw new OctaveException("conversion to cell array from " + rhs.ClassName + " not possible; use '{' instead of '('");
        if (a is StructValue && rhs is MatrixValue) throw new OctaveException("invalid assignment to struct array element");
        throw new OctaveException($"operator = undefined for '{a.ClassName}' by '{rhs.ClassName}' operations");
    }

    public static ArrayValue Delete(ArrayValue a, Value[] args)
    {
        if (args.Length > 2 || (args.Length == 2 && a.ND != null)) return DeleteND(a, args);
        if (args.Length == 1)
        {
            var ix = Convert(args[0], a.Numel, "index", false);
            var del = new HashSet<int>(ix.I);
            var keep = Enumerable.Range(0, a.Numel).Where(k => !del.Contains(k)).ToArray();
            if (!ix.Colon && del.Count == 0) return Clone(a);
            if (ix.Colon) return a.Gather(Array.Empty<int>(), 0, 0);
            if (a.C == 1 && a.R != 1) return a.Gather(keep, keep.Length, 1);
            return a.Gather(keep, 1, keep.Length);
        }
        bool rAll = args[0] is MagicColon || IsFull(args[0], a.R);
        bool cAll = args[1] is MagicColon || IsFull(args[1], a.C);
        if (rAll)
        {
            var ci = new HashSet<int>(Convert(args[1], a.C, "index", false).I);
            var cols = Enumerable.Range(0, a.C).Where(j => !ci.Contains(j)).ToArray();
            var lin = cols.SelectMany(j => Enumerable.Range(0, a.R).Select(i => i + j * a.R)).ToArray();
            return a.Gather(lin, a.R, cols.Length);
        }
        if (cAll)
        {
            var ri = new HashSet<int>(Convert(args[0], a.R, "index", false).I);
            var rows = Enumerable.Range(0, a.R).Where(i => !ri.Contains(i)).ToArray();
            var lin = Enumerable.Range(0, a.C).SelectMany(j => rows.Select(i => i + j * a.R)).ToArray();
            return a.Gather(lin, rows.Length, a.C);
        }
        throw new OctaveException("a null assignment can only have one non-colon index");
    }

    static bool IsFull(Value v, int extent)
    {
        if (v is not MatrixValue m) return false;
        var set = new HashSet<double>(m.IsLogical ? Enumerable.Range(0, m.Numel).Where(k => m.Re[k] != 0).Select(k => (double)k + 1) : m.Re);
        for (int k = 1; k <= extent; k++) if (!set.Contains(k)) return false;
        return true;
    }
}
