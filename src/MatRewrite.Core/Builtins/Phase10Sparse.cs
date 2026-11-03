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

    static SparseValue IcholCore(SparseValue a0, bool pattern, double droptol, bool michol)
    {
        // faithful port of Octave's ichol_0 / ichol_t (libinterp/corefcn/__ichol__.cc)
        int n = a0.C; bool cplx = a0.Im != null;
        var cidx = new int[n + 1]; var ridxL = new List<int>(); var dataL = new List<System.Numerics.Complex>();
        for (int c = 0; c < n; c++)
        {
            for (int p = a0.Cp[c]; p < a0.Cp[c + 1]; p++)
                if (a0.Ri[p] >= c) { ridxL.Add(a0.Ri[p]); dataL.Add(new System.Numerics.Complex(a0.Re[p], cplx ? a0.Im![p] : 0)); }
            cidx[c + 1] = ridxL.Count;
        }
        int[] ridx = ridxL.ToArray(); var data = dataL.ToArray();
        System.Numerics.Complex Mult(System.Numerics.Complex x, System.Numerics.Complex y) => cplx ? x * System.Numerics.Complex.Conjugate(y) : new System.Numerics.Complex(x.Real * y.Real, 0);
        void Check(System.Numerics.Complex pv)
        {
            if (cplx && Math.Abs(pv.Imaginary) > 2.220446049250313e-16) throw new OctaveException("ichol: non-real pivot encountered.  The matrix must be Hermitian.");
            if (pv.Real < 0) throw new OctaveException("ichol: negative pivot encountered");
        }
        System.Numerics.Complex Sqrt(System.Numerics.Complex v) => cplx ? System.Numerics.Complex.Sqrt(v) : new System.Numerics.Complex(Math.Sqrt(v.Real), 0);
        var Lfirst = new int[n]; var Llist = new int[n];
        for (int q = 0; q < n; q++) { Lfirst[q] = -1; Llist[q] = -1; }
        SparseValue Build(int[] cp, int[] ri, System.Numerics.Complex[] d, int len)
        {
            var re = new double[len]; double[]? im = cplx ? new double[len] : null;
            for (int q = 0; q < len; q++) { re[q] = d[q].Real; if (im != null) im[q] = d[q].Imaginary; }
            return new SparseValue(n, n, cp, ri[..len], re, im);
        }
        if (pattern)
        {
            var iw = new int[n]; var drops = new System.Numerics.Complex[n];
            for (int q = 0; q < n; q++) iw[q] = -1;
            for (int k = 0; k < n; k++)
            {
                int j1 = cidx[k], j2 = cidx[k + 1];
                for (int j = j1; j < j2; j++) iw[ridx[j]] = j;
                int jrow = Llist[k];
                while (jrow != -1)
                {
                    int jjrow = Lfirst[jrow], jend = cidx[jrow + 1];
                    for (int jj = jjrow; jj < jend; jj++)
                    {
                        int r = ridx[jj], jw = iw[r];
                        var tl = Mult(data[jj], data[jjrow]);
                        if (jw != -1) data[jw] -= tl;
                        else if (michol) { drops[r] -= tl; drops[k] -= tl; }
                    }
                    if (jjrow + 1 < jend)
                    {
                        Lfirst[jrow]++; int j = jrow; jrow = Llist[jrow];
                        Llist[j] = Llist[ridx[Lfirst[j]]]; Llist[ridx[Lfirst[j]]] = j;
                    }
                    else jrow = Llist[jrow];
                }
                if (michol && j1 < j2) data[j1] += drops[k];
                if (j1 == j2 || ridx[j1] != k) throw new OctaveException("ichol: encountered a pivot equal to 0");
                Check(data[j1]);
                data[j1] = Sqrt(data[j1]);
                if (k < n - 1)
                {
                    iw[ridx[j1]] = -1;
                    for (int q = j1 + 1; q < j2; q++) { iw[ridx[q]] = -1; data[q] /= data[j1]; }
                    Lfirst[k] = j1;
                    if (Lfirst[k] + 1 < j2) { Lfirst[k]++; int jj2 = ridx[Lfirst[k]]; Llist[k] = Llist[jj2]; Llist[jj2] = k; }
                }
            }
            return Build(cidx, ridx, data, data.Length);
        }
        // ichol_t
        var norms = new double[n];
        for (int k = 0; k < n; k++) for (int p = cidx[k]; p < cidx[k + 1]; p++) norms[k] += data[p].Magnitude;
        var cl = new int[n + 1]; var rl = new List<int>(); var dl = new List<System.Numerics.Complex>();
        var w = new System.Numerics.Complex[n]; var cdrops = new System.Numerics.Complex[n]; var mark = new bool[n]; var vec = new int[n];
        for (int k = 0; k < n; k++)
        {
            int ind = 0;
            for (int j = cidx[k]; j < cidx[k + 1]; j++) { w[ridx[j]] = data[j]; mark[ridx[j]] = true; if (ridx[j] != k) vec[ind++] = ridx[j]; }
            int jrow = Llist[k];
            while (jrow != -1)
            {
                int jjrow = Lfirst[jrow], jend = cl[jrow + 1];
                for (int jj = jjrow; jj < jend; jj++)
                {
                    int j = rl[jj];
                    if (!mark[j]) { mark[j] = true; vec[ind++] = j; }
                    w[j] -= Mult(dl[jj], dl[jjrow]);
                }
                if (jjrow + 1 < jend)
                {
                    Lfirst[jrow]++; int j = jrow; jrow = Llist[jrow];
                    Llist[j] = Llist[rl[Lfirst[j]]]; Llist[rl[Lfirst[j]]] = j;
                }
                else jrow = Llist[jrow];
            }
            Array.Sort(vec, 0, ind);
            int start = rl.Count; dl.Add(w[k]); rl.Add(k); int wlen = 1;
            for (int q = 0; q < ind; q++)
            {
                int jr = vec[q];
                if (w[jr] != System.Numerics.Complex.Zero)
                {
                    if (w[jr].Magnitude < droptol * norms[k]) { if (michol) { cdrops[k] += w[jr]; cdrops[jr] += w[jr]; } }
                    else { dl.Add(w[jr]); rl.Add(jr); wlen++; }
                }
                mark[jr] = false; vec[q] = 0; w[jr] = System.Numerics.Complex.Zero;
            }
            mark[k] = false; w[k] = System.Numerics.Complex.Zero;
            if (michol) dl[start] += cdrops[k];
            if (dl[start] == System.Numerics.Complex.Zero) throw new OctaveException("ichol: encountered a pivot equal to 0");
            Check(dl[start]);
            dl[start] = Sqrt(dl[start]);
            for (int jj = start + 1; jj < start + wlen; jj++) dl[jj] /= dl[start];
            cl[k + 1] = cl[k] + wlen;
            if (k < n - 1)
            {
                Lfirst[k] = cl[k];
                if (Lfirst[k] + 1 < cl[k + 1]) { Lfirst[k]++; int jj2 = rl[Lfirst[k]]; Llist[k] = Llist[jj2]; Llist[jj2] = k; }
            }
        }
        return Build(cl, rl.ToArray(), dl.ToArray(), dl.Count);
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
        SparseBridge.Aware.Add("kron"); SparseBridge.Aware.Add("gallery"); SparseBridge.Aware.Add("norm"); foreach (var nm0 in new[] { "__ichol0__", "__icholt__" }) SparseBridge.Aware.Add(nm0);
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
        foreach (var tname in new[] { "tril", "triu" })
        {
            var t0 = cur.Builtins[tname]; bool lower = tname == "tril";
            Reg(tname, (it, a, n) =>
            {
                if (a.Length >= 1 && a[0] is SparseValue s && (a.Length == 1 || (a.Length == 2 && a[1] is MatrixValue km && !km.IsChar && km.Numel == 1)))
                {
                    int k = a.Length == 2 ? (int)D(a[1], tname) : 0;
                    var cp = new int[s.C + 1]; var ri = new List<int>(); var re = new List<double>(); var im = s.Im == null ? null : new List<double>();
                    for (int j = 0; j < s.C; j++)
                    {
                        for (int q = s.Cp[j]; q < s.Cp[j + 1]; q++)
                        {
                            int d = j - s.Ri[q];
                            if (lower ? d <= k : d >= k) { ri.Add(s.Ri[q]); re.Add(s.Re[q]); im?.Add(s.Im![q]); }
                        }
                        cp[j + 1] = ri.Count;
                    }
                    return new Value[] { new SparseValue(s.R, s.C, cp, ri.ToArray(), re.ToArray(), im?.ToArray(), s.Logical) };
                }
                return SparseBridge.Call(tname, a, x => t0(it, x, n));
            });
            SparseBridge.Aware.Add(tname);
        }
        foreach (var vn in new[] { "var", "std" })
        {
            if (!cur.Builtins.TryGetValue(vn, out var v0)) continue;
            SparseBridge.Aware.Add(vn);
            Reg(vn, (it, a, n) =>
            {
                bool sp = a.Length > 0 && a[0] is SparseValue;
                var r = v0(it, a.Select(v => v is SparseValue sv ? sv.ToDense() : v).ToArray(), n);
                if (sp) for (int i = 0; i < r.Length; i++) if (r[i] is MatrixValue rm && rm.ND == null && rm.Kind == NumKind.Double) r[i] = SparseValue.FromDense(rm);
                return r;
            });
        }
        var norm0 = cur.Builtins["norm"];
        Reg("norm", (it, a, n) =>
        {
            if (a.Length >= 1 && a.Length <= 2 && a[0] is SparseValue s && (s.R > 1 && s.C > 1) && (long)s.R * s.C > 1_000_000)
            {
                string p = "fro";
                if (a.Length == 2) p = a[1] is MatrixValue pm && pm.IsChar ? pm.AsString().ToLowerInvariant() : D(a[1], "norm") is var pd && double.IsPositiveInfinity(pd) ? "inf" : pd == 1 ? "1" : pd == 2 ? "2" : "?";
                else p = "2";
                double Ab(int q) => s.Im == null ? Math.Abs(s.Re[q]) : Math.Sqrt(s.Re[q] * s.Re[q] + s.Im[q] * s.Im[q]);
                if (p == "fro") { double t = 0; for (int q = 0; q < s.Cp[s.C]; q++) { var v = Ab(q); t += v * v; } return new Value[] { MatrixValue.Scalar(Math.Sqrt(t)) }; }
                if (p == "1") { double best = 0; for (int j = 0; j < s.C; j++) { double t = 0; for (int q = s.Cp[j]; q < s.Cp[j + 1]; q++) t += Ab(q); best = Math.Max(best, t); } return new Value[] { MatrixValue.Scalar(best) }; }
                if (p == "inf") { var r = new double[s.R]; for (int q = 0; q < s.Cp[s.C]; q++) r[s.Ri[q]] += Ab(q); return new Value[] { MatrixValue.Scalar(r.Length == 0 ? 0 : r.Max()) }; }
            }
            return norm0(it, a.Select(v => v is SparseValue sv ? sv.ToDense() : v).ToArray(), n);
        });
        static string MS(Value[] a, int i) => a.Length > i && a[i] is MatrixValue m && m.IsChar ? m.AsString() : "";
        Value[] One(Interpreter it, int n, SparseValue s, SparseValue L, SparseValue U) =>
            n <= 1 ? new Value[] { Fc(it, "plus", Fc(it, "minus", L, Fc(it, "speye", Num(s.R))), U) } : new Value[] { L, U };
        Reg("__ilu0__", (it, a, n) => { var s = SpArg(a[0], "ilu"); var (L, U) = IluPort.Ilu0(s, MS(a, 1)); return One(it, n, s, L, U); });
        Reg("__iluc__", (it, a, n) => { var s = SpArg(a[0], "ilu"); var (L, U) = IluPort.Iluc(s, a.Length > 1 ? D(a[1], "ilu") : 0, MS(a, 2)); return One(it, n, s, L, U); });
        Reg("__ilutp__", (it, a, n) =>
        {
            var s = SpArg(a[0], "ilu");
            var r = IluPort.Ilutp(s, a.Length > 1 ? D(a[1], "ilu") : 0, a.Length > 2 ? D(a[2], "ilu") : 1, MS(a, 3), a.Length > 4 ? D(a[4], "ilu") : 0, Math.Max(n, 2));
            if (n <= 1) return One(it, n, s, (SparseValue)r[0], (SparseValue)r[1]);
            return r;
        });
        Reg("__ichol0__", (it, a, n) =>
            Out(IcholCore(SpArg(a[0], "ichol"), true, 0, a.Length > 1 && a[1] is MatrixValue m && m.IsChar && m.AsString() == "on")));
        Reg("__icholt__", (it, a, n) =>
            Out(IcholCore(SpArg(a[0], "ichol"), false, a.Length > 1 ? D(a[1], "ichol") : 0, a.Length > 2 && a[2] is MatrixValue m && m.IsChar && m.AsString() == "on")));
    }
}
