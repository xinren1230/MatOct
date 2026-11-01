// MatRewrite - GPL-3.0-or-later
// Phase 10: incomplete factorizations (__ilu0__, __iluc__, __ilutp__, __ichol0__, __icholt__) on row-sparse working storage.
using MatRewrite.Core.Runtime;
using MatRewrite.Core.Sparse;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    /// <summary>Row-wise sparse storage: per row a sorted column->value map.</summary>
    static SortedDictionary<int, double>[] SpRows(SparseValue a)
    {
        var rows = new SortedDictionary<int, double>[a.R];
        for (int i = 0; i < a.R; i++) rows[i] = new();
        for (int j = 0; j < a.C; j++)
            for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) rows[a.Ri[p]][j] = a.Re[p];
        return rows;
    }
    static SparseValue FromRows(SortedDictionary<int, double>[] rows, int r, int c)
    {
        var I = new List<int>(); var J = new List<int>(); var V = new List<double>();
        for (int i = 0; i < rows.Length; i++) foreach (var kv in rows[i]) if (kv.Value != 0) { I.Add(i); J.Add(kv.Key); V.Add(kv.Value); }
        return SparseValue.FromTriplets(r, c, I, J, V);
    }
    static int MiluMode(Value v) => v is MatrixValue m && m.IsChar ? m.AsString() switch { "row" => 1, "col" => 2, _ => 0 } : 0;

    /// <summary>Row-oriented incomplete LU. pattern=true keeps A's pattern (ILU0); otherwise drop by droptol*norm(A(:,j)).</summary>
    static (SparseValue L, SparseValue U) IluCore(SparseValue a, bool pattern, double droptol, int milu, string fn)
    {
        int n = a.R;
        var rows = SpRows(a);
        var colNorm = new double[n];
        for (int j = 0; j < n; j++) { double s = 0; for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) s += a.Re[p] * a.Re[p]; colNorm[j] = Math.Sqrt(s); }
        var rowNorm = new double[n];
        for (int i = 0; i < n; i++) rowNorm[i] = Math.Sqrt(rows[i].Values.Sum(v => v * v));
        var U = new SortedDictionary<int, double>[n];
        var L = new SortedDictionary<int, double>[n];
        for (int i = 0; i < n; i++)
        {
            var w = new SortedDictionary<int, double>(rows[i]);
            var pat = pattern ? new HashSet<int>(rows[i].Keys) : null;
            double dropped = 0;
            int k = -1;
            while (true)
            {
                int next = -1;
                foreach (var key in w.Keys) if (key > k && key < i) { next = key; break; }
                if (next < 0) break;
                k = next;
                double ukk = U[k].TryGetValue(k, out var d) ? d : 0;
                if (ukk == 0) throw new OctaveException($"{fn}: encountered a pivot equal to 0");
                double lik = w[k] / ukk;
                if (!pattern && droptol > 0 && Math.Abs(lik) < droptol * colNorm[k]) { dropped += w[k]; w.Remove(k); continue; }
                w[k] = lik;
                foreach (var kv in U[k])
                {
                    if (kv.Key <= k) continue;
                    double upd = lik * kv.Value;
                    if (pattern && !pat!.Contains(kv.Key)) { dropped -= upd; continue; }
                    w[kv.Key] = (w.TryGetValue(kv.Key, out var o) ? o : 0) - upd;
                }
            }
            L[i] = new(); U[i] = new();
            foreach (var kv in w)
            {
                if (kv.Key < i) L[i][kv.Key] = kv.Value;
                else
                {
                    if (!pattern && droptol > 0 && kv.Key != i && Math.Abs(kv.Value) < droptol * rowNorm[i]) { dropped += kv.Value; continue; }
                    U[i][kv.Key] = kv.Value;
                }
            }
            if (milu != 0) U[i][i] = (U[i].TryGetValue(i, out var dd) ? dd : 0) + dropped;
            L[i][i] = 1;
            if (!U[i].TryGetValue(i, out var piv) || piv == 0) throw new OctaveException($"{fn}: encountered a pivot equal to 0");
        }
        return (FromRows(L, n, n), FromRows(U, n, n));
    }

    static SparseValue IcholCore(SparseValue a, bool pattern, double droptol, bool michol)
    {
        int n = a.R;
        if (a.Im != null)
            for (int j = 0; j < n; j++) for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++)
                    if (a.Ri[p] == j && a.Im[p] != 0) throw new OctaveException("ichol: non-real pivot encountered.  The matrix must be Hermitian.");
        // column storage of the lower triangle
        var cols = new SortedDictionary<int, double>[n];
        var colNorm = new double[n];
        for (int j = 0; j < n; j++)
        {
            cols[j] = new();
            double s = 0;
            for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) { s += a.Re[p] * a.Re[p]; if (a.Ri[p] >= j) cols[j][a.Ri[p]] = a.Re[p]; }
            colNorm[j] = Math.Sqrt(s);
        }
        var pat = pattern ? cols.Select(c => new HashSet<int>(c.Keys)).ToArray() : null;
        var diagAdd = new double[n];
        // left-looking: column j updated by previous columns k with L(j,k) != 0
        var rowRefs = new List<int>[n]; for (int i = 0; i < n; i++) rowRefs[i] = new();
        for (int j = 0; j < n; j++)
        {
            var cj = cols[j];
            if (michol) cj[j] = (cj.TryGetValue(j, out var d0) ? d0 : 0) + diagAdd[j];
            foreach (var k in rowRefs[j])
            {
                double ljk = cols[k][j];
                foreach (var kv in cols[k])
                {
                    if (kv.Key < j) continue;
                    double upd = kv.Value * ljk;
                    if (pattern && !pat![j].Contains(kv.Key))
                    {
                        if (michol) { diagAdd[kv.Key] -= upd; cj[j] = cj.GetValueOrDefault(j) - upd; }
                        continue;
                    }
                    cj[kv.Key] = cj.GetValueOrDefault(kv.Key) - upd;
                }
            }
            double djj = cj.GetValueOrDefault(j);
            if (djj == 0) throw new OctaveException("ichol: encountered a pivot equal to 0");
            if (djj < 0 || double.IsNaN(djj)) throw new OctaveException("ichol: negative pivot encountered");
            double s = Math.Sqrt(djj); cj[j] = s;
            foreach (var key in cj.Keys.Where(x => x > j).ToList())
            {
                double v = cj[key] / s;
                if (!pattern && Math.Abs(v) < droptol * colNorm[j])
                {
                    cj.Remove(key);
                    if (michol) { diagAdd[key] += v * s; cj[j] = cj[j]; }
                    continue;
                }
                cj[key] = v;
                rowRefs[key].Add(j);
            }
        }
        var I = new List<int>(); var J = new List<int>(); var V = new List<double>();
        for (int j = 0; j < n; j++) foreach (var kv in cols[j]) if (kv.Value != 0) { I.Add(kv.Key); J.Add(j); V.Add(kv.Value); }
        return SparseValue.FromTriplets(n, n, I, J, V);
    }

    static SparseValue SpKron(SparseValue x, SparseValue y)
    {
        var I = new List<int>(); var J = new List<int>(); var V = new List<double>();
        for (int jx = 0; jx < x.C; jx++)
            for (int p = x.Cp[jx]; p < x.Cp[jx + 1]; p++)
                for (int jy = 0; jy < y.C; jy++)
                    for (int q = y.Cp[jy]; q < y.Cp[jy + 1]; q++)
                    { I.Add(x.Ri[p] * y.R + y.Ri[q]); J.Add(jx * y.C + jy); V.Add(x.Re[p] * y.Re[q]); }
        return SparseValue.FromTriplets(x.R * y.R, x.C * y.C, I, J, V);
    }

    static void RegisterPhase10Sparse()
    {
        SparseBridge.Aware.Add("kron"); SparseBridge.Aware.Add("gallery");
        var kron0 = cur!.Builtins["kron"];
        Reg("kron", (it, a, n) =>
        {
            if (a.Length == 2 && a.Any(v => v is SparseValue) && a.All(v => v is SparseValue || v is MatrixValue { IsComplex: false, ND: null })
                && !(a[0] is SparseValue { Im: not null }) && !(a[1] is SparseValue { Im: not null }))
                return new Value[] { SpKron(SpArg(a[0], "kron"), SpArg(a[1], "kron")) };
            return kron0(it, a.Select(v => v is SparseValue sv ? sv.ToDense() : v).ToArray(), n);
        });
        var gal0 = cur.Builtins["gallery"];
        Reg("gallery", (it, a, n) =>
        {
            var r = gal0(it, a.Select(v => v is SparseValue sv ? sv.ToDense() : v).ToArray(), n);
            if (a.Length > 0 && a[0] is MatrixValue nm && nm.IsChar && nm.AsString().ToLowerInvariant() is "tridiag" or "poisson" or "neumann" or "wathen"
                && r.Length > 0 && r[0] is MatrixValue rm && !rm.IsComplex)
            { r = (Value[])r.Clone(); r[0] = SparseValue.FromDense(rm); }
            return r;
        });
        Reg("__ilu0__", (it, a, n) =>
        {
            var s = SpArg(a[0], "ilu");
            for (int j = 0; j < s.C; j++)
            {
                bool has = false;
                for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++) if (s.Ri[p] == j && s.Re[p] != 0) has = true;
                if (!has) throw new OctaveException("ilu: A has a zero on the diagonal");
            }
            var (L, U) = IluCore(s, true, 0, a.Length > 1 ? MiluMode(a[1]) : 0, "ilu");
            return n <= 1 ? new Value[] { Fc(it, "plus", Fc(it, "minus", L, Fc(it, "speye", Num(s.R))), U) } : new Value[] { L, U };
        });
        Reg("__iluc__", (it, a, n) =>
        {
            var s = SpArg(a[0], "ilu");
            var (L, U) = IluCore(s, false, a.Length > 1 ? D(a[1], "ilu") : 0, a.Length > 2 ? MiluMode(a[2]) : 0, "ilu");
            return n <= 1 ? new Value[] { Fc(it, "plus", Fc(it, "minus", L, Fc(it, "speye", Num(s.R))), U) } : new Value[] { L, U };
        });
        Reg("__ilutp__", (it, a, n) =>
        {
            var s = SpArg(a[0], "ilu");
            var (L, U) = IluCore(s, false, a.Length > 1 ? D(a[1], "ilu") : 0, a.Length > 3 ? MiluMode(a[3]) : 0, "ilu");
            if (n <= 1) return new Value[] { Fc(it, "plus", Fc(it, "minus", L, Fc(it, "speye", Num(s.R))), U) };
            if (n == 2) return new Value[] { L, U };
            return new Value[] { L, U, Fc(it, "speye", Num(s.R)) };
        });
        Reg("__ichol0__", (it, a, n) =>
            Out(IcholCore(SpArg(a[0], "ichol"), true, 0, a.Length > 1 && a[1] is MatrixValue m && m.IsChar && m.AsString() == "on")));
        Reg("__icholt__", (it, a, n) =>
            Out(IcholCore(SpArg(a[0], "ichol"), false, a.Length > 1 ? D(a[1], "ichol") : 0, a.Length > 2 && a[2] is MatrixValue m && m.IsChar && m.AsString() == "on")));
    }
}
