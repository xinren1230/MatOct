// MatRewrite - GPL-3.0-or-later
// Port of Octave's libinterp/corefcn/__ilu__.cc (ilu_0, ilu_crout, ilu_tp) on compressed-column storage.
using System.Numerics;
using MatRewrite.Core.Runtime;
using MatRewrite.Core.Sparse;

namespace MatRewrite.Core.Builtins;

static class IluPort
{
    internal sealed class Csc
    {
        public int N; public int[] Cp; public int[] Ri; public Complex[] V;
        public Csc(int n, int[] cp, int[] ri, Complex[] v) { N = n; Cp = cp; Ri = ri; V = v; }
        public int Nnz => Cp[N];
    }

    internal static Csc From(SparseValue s)
    {
        int nz = s.Cp[s.C];
        var v = new Complex[nz];
        for (int p = 0; p < nz; p++) v[p] = new Complex(s.Re[p], s.Im?[p] ?? 0);
        return new Csc(s.C, (int[])s.Cp.Clone(), s.Ri[..nz], v);
    }

    internal static SparseValue To(Csc a, bool cplx)
    {
        // drop explicit zeros like Octave's sparse constructor results do when displayed/compared
        var cp = new int[a.N + 1]; var ri = new List<int>(); var re = new List<double>(); var im = new List<double>();
        for (int j = 0; j < a.N; j++)
        {
            var ents = new List<(int r, Complex v)>();
            for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) ents.Add((a.Ri[p], a.V[p]));
            ents.Sort((x, y) => x.r.CompareTo(y.r));
            int last = -1;
            foreach (var (r, v) in ents)
            {
                if (r == last) { re[^1] += v.Real; im[^1] += v.Imaginary; continue; }
                ri.Add(r); re.Add(v.Real); im.Add(v.Imaginary); last = r;
            }
            cp[j + 1] = ri.Count;
        }
        // prune zeros
        var cp2 = new int[a.N + 1]; var ri2 = new List<int>(); var re2 = new List<double>(); var im2 = new List<double>();
        for (int j = 0; j < a.N; j++) { for (int p = cp[j]; p < cp[j + 1]; p++) if (re[p] != 0 || im[p] != 0) { ri2.Add(ri[p]); re2.Add(re[p]); im2.Add(im[p]); } cp2[j + 1] = ri2.Count; }
        bool anyIm = cplx && im2.Any(x => x != 0);
        return new SparseValue(a.N, a.N, cp2, ri2.ToArray(), re2.ToArray(), anyIm ? im2.ToArray() : null);
    }

    internal static Csc Transpose(Csc a)
    {
        int n = a.N; var cnt = new int[n + 1];
        for (int p = 0; p < a.Nnz; p++) cnt[a.Ri[p] + 1]++;
        for (int i = 0; i < n; i++) cnt[i + 1] += cnt[i];
        var cp = (int[])cnt.Clone(); var ri = new int[a.Nnz]; var v = new Complex[a.Nnz]; var pos = (int[])cnt.Clone();
        for (int j = 0; j < n; j++) for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) { int q = pos[a.Ri[p]]++; ri[q] = j; v[q] = a.V[p]; }
        return new Csc(n, cp, ri, v);
    }

    static Csc Filter(Csc a, Func<int, int, bool> keep)
    {
        var cp = new int[a.N + 1]; var ri = new List<int>(); var v = new List<Complex>();
        for (int j = 0; j < a.N; j++) { for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) if (keep(a.Ri[p], j)) { ri.Add(a.Ri[p]); v.Add(a.V[p]); } cp[j + 1] = ri.Count; }
        return new Csc(a.N, cp, ri.ToArray(), v.ToArray());
    }
    static Csc Triu(Csc a) => Filter(a, (r, c) => r <= c);
    static Csc TrilM1(Csc a) => Filter(a, (r, c) => r > c);
    static Csc Eye(int n) { var cp = new int[n + 1]; var ri = new int[n]; var v = new Complex[n]; for (int i = 0; i < n; i++) { cp[i + 1] = i + 1; ri[i] = i; v[i] = 1; } return new Csc(n, cp, ri, v); }
    static Csc Add(Csc a, Csc b)
    {
        var cp = new int[a.N + 1]; var ri = new List<int>(); var v = new List<Complex>();
        for (int j = 0; j < a.N; j++)
        {
            var d = new SortedDictionary<int, Complex>();
            for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) d[a.Ri[p]] = d.GetValueOrDefault(a.Ri[p]) + a.V[p];
            for (int p = b.Cp[j]; p < b.Cp[j + 1]; p++) d[b.Ri[p]] = d.GetValueOrDefault(b.Ri[p]) + b.V[p];
            foreach (var kv in d) { ri.Add(kv.Key); v.Add(kv.Value); }
            cp[j + 1] = ri.Count;
        }
        return new Csc(a.N, cp, ri.ToArray(), v.ToArray());
    }
    // M(:, perm)
    static Csc ColPerm(Csc a, int[] perm)
    {
        var cp = new int[a.N + 1]; var ri = new List<int>(); var v = new List<Complex>();
        for (int j = 0; j < a.N; j++) { int s = perm[j]; for (int p = a.Cp[s]; p < a.Cp[s + 1]; p++) { ri.Add(a.Ri[p]); v.Add(a.V[p]); } cp[j + 1] = ri.Count; }
        return new Csc(a.N, cp, ri.ToArray(), v.ToArray());
    }
    // M(perm, :)
    static Csc RowPerm(Csc a, int[] perm)
    {
        var inv = new int[a.N]; for (int i = 0; i < a.N; i++) inv[perm[i]] = i;
        var r = new Csc(a.N, (int[])a.Cp.Clone(), a.Ri.Select(x => inv[x]).ToArray(), (Complex[])a.V.Clone());
        return r;
    }
    static double[] ColNorms(Csc a) { var r = new double[a.N]; for (int j = 0; j < a.N; j++) { double s = 0; for (int p = a.Cp[j]; p < a.Cp[j + 1]; p++) { var m = a.V[p].Magnitude; s += m * m; } r[j] = Math.Sqrt(s); } return r; }
    static double[] RowNorms(Csc a) { var r = new double[a.N]; for (int p = 0; p < a.Nnz; p++) { var m = a.V[p].Magnitude; r[a.Ri[p]] += m * m; } for (int i = 0; i < a.N; i++) r[i] = Math.Sqrt(r[i]); return r; }

    static bool Gt(Complex a, Complex b, bool cplx)
    {
        if (!cplx) return a.Real > b.Real;
        double ma = a.Magnitude, mb = b.Magnitude;
        if (ma != mb) return ma > mb;
        return a.Phase > b.Phase;
    }

    // ------------------------------------------------------------ ilu_0
    public static (SparseValue L, SparseValue U) Ilu0(SparseValue s, string milu)
    {
        bool cplx = s.Im != null;
        var sm = From(s); int n = sm.N;
        int opt = milu == "row" ? 1 : milu == "col" ? 2 : 0;
        if (opt == 1) sm = Transpose(sm);
        var cidx = sm.Cp; var ridx = sm.Ri; var data = sm.V;
        var iw = new int[n]; var uptr = new int[n]; Array.Fill(iw, -1);
        Complex tl = 0;
        for (int k = 0; k < n; k++)
        {
            int j1 = cidx[k], j2 = cidx[k + 1];
            if (j1 == j2) throw new OctaveException("ilu: A has a zero on the diagonal");
            for (int j0 = j1; j0 < j2; j0++) iw[ridx[j0]] = j0;
            Complex r = 0; int j = j1; int jrow = ridx[j1];
            while (jrow < k && j < j2)
            {
                if (opt == 1) { tl = data[j] / data[uptr[jrow]]; data[j] = tl; }
                for (int jj = uptr[jrow] + 1; jj < cidx[jrow + 1]; jj++)
                {
                    int jw = iw[ridx[jj]];
                    if (jw != -1) { if (opt == 1) data[jw] -= tl * data[jj]; else data[jw] -= data[j] * data[jj]; }
                    else { if (opt == 1) r += tl * data[jj]; else if (opt == 2) r += data[j] * data[jj]; }
                }
                j++;
                jrow = j < ridx.Length ? ridx[j] : int.MaxValue;
            }
            if (j >= j2 || k != jrow) throw new OctaveException("ilu: A has a zero on the diagonal");
            uptr[k] = j;
            if (opt != 0) data[uptr[k]] -= r;
            if (opt != 1) for (int jj = uptr[k] + 1; jj < cidx[k + 1]; jj++) data[jj] /= data[uptr[k]];
            if (data[j] == Complex.Zero) throw new OctaveException("ilu: encountered a pivot equal to 0");
            for (int i = j1; i < j2; i++) iw[ridx[i]] = -1;
        }
        if (opt == 1) sm = Transpose(sm);
        return (To(Add(Eye(n), TrilM1(sm)), cplx), To(Triu(sm), cplx));
    }

    // ------------------------------------------------------------ ilu_crout
    public static (SparseValue L, SparseValue U) Iluc(SparseValue s, double droptol, string milu)
    {
        bool cplx = s.Im != null;
        var sm = From(s); int n = sm.N;
        var colsNorm = ColNorms(sm); var rowsNorm = RowNorms(sm);
        var smU = Transpose(Triu(sm)); var smL = TrilM1(sm);
        int opt = milu == "row" ? 1 : milu == "col" ? 2 : 0;
        var cidx_l = new int[n + 1]; var cidx_u = new int[n + 1];
        var ridx_l = new List<int>(); var data_l = new List<Complex>(); var ridx_u = new List<int>(); var data_u = new List<Complex>();
        var colsList = new int[n]; var rowsList = new int[n];
        var wl = new Complex[n]; var wu = new Complex[n]; var Ufirst = new int[n]; var Lfirst = new int[n]; var crSum = new Complex[n];
        int colsLen = 0, rowsLen = 0;
        for (int k = 0; k < n; k++)
        {
            for (int i = smL.Cp[k]; i < smL.Cp[k + 1]; i++) wl[smL.Ri[i]] = smL.V[i];
            for (int i = smU.Cp[k]; i < smU.Cp[k + 1]; i++) wu[smU.Ri[i]] = smU.V[i];
            for (int j = 0; j < rowsLen; j++)
            {
                int rj = rowsList[j];
                if (Ufirst[rj] != -1)
                    for (int jj = Ufirst[rj]; jj < cidx_u[rj + 1]; jj++) wu[ridx_u[jj]] -= data_u[jj] * data_l[Lfirst[rj]];
            }
            for (int j = 0; j < colsLen; j++)
            {
                int cj = colsList[j];
                if (Lfirst[cj] != -1)
                    for (int jj = Lfirst[cj]; jj < cidx_l[cj + 1]; jj++) wl[ridx_l[jj]] -= data_l[jj] * data_u[Ufirst[cj]];
            }
            int startU = data_u.Count, startL = data_l.Count;
            data_u.Add(wu[k]); ridx_u.Add(k); int wlenU = 1, wlenL = 0;
            for (int i = k + 1; i < n; i++)
            {
                if (wu[i] != Complex.Zero)
                {
                    if (wu[i].Magnitude < droptol * rowsNorm[k]) { if (opt == 1) crSum[k] += wu[i]; else if (opt == 2) crSum[i] += wu[i]; }
                    else { data_u.Add(wu[i]); ridx_u.Add(i); wlenU++; }
                }
                if (wl[i] != Complex.Zero)
                {
                    if (wl[i].Magnitude < droptol * colsNorm[k]) { if (opt == 2) crSum[k] += wl[i]; else if (opt == 1) crSum[i] += wl[i]; }
                    else { data_l.Add(wl[i]); ridx_l.Add(i); wlenL++; }
                }
                wu[i] = 0; wl[i] = 0;
            }
            wu[k] = 0; wl[k] = 0;
            if (opt != 0) data_u[startU] += crSum[k];
            if (data_u[startU] == Complex.Zero) throw new OctaveException("ilu: encountered a pivot equal to 0");
            for (int i = startL; i < startL + wlenL; i++) data_l[i] /= data_u[startU];
            cidx_u[k + 1] = cidx_u[k] + wlenU; cidx_l[k + 1] = cidx_l[k] + wlenL;
            if (k < n - 1)
            {
                Ufirst[k] = wlenU > 0 ? cidx_u[k] : -1;
                Lfirst[k] = wlenL > 0 ? cidx_l[k] : -1;
                colsLen = 0; rowsLen = 0;
                for (int i = 0; i <= k; i++)
                {
                    if (Ufirst[i] != -1)
                    {
                        int jj = ridx_u[Ufirst[i]];
                        if (jj < k + 1)
                        {
                            if (Ufirst[i] < cidx_u[i + 1])
                            {
                                Ufirst[i]++;
                                if (Ufirst[i] == cidx_u[i + 1]) Ufirst[i] = -1; else jj = ridx_u[Ufirst[i]];
                            }
                        }
                        if (jj == k + 1) colsList[colsLen++] = i;
                    }
                    if (Lfirst[i] != -1)
                    {
                        int jj = ridx_l[Lfirst[i]];
                        if (jj < k + 1)
                            if (Lfirst[i] < cidx_l[i + 1])
                            {
                                Lfirst[i]++;
                                if (Lfirst[i] == cidx_l[i + 1]) Lfirst[i] = -1; else jj = ridx_l[Lfirst[i]];
                            }
                        if (jj == k + 1) rowsList[rowsLen++] = i;
                    }
                }
            }
        }
        var L = new Csc(n, cidx_l, ridx_l.ToArray(), data_l.ToArray());
        var U = Transpose(new Csc(n, cidx_u, ridx_u.ToArray(), data_u.ToArray()));
        return (To(Add(L, Eye(n)), cplx), To(U, cplx));
    }

    // ------------------------------------------------------------ ilu_tp
    public static Value[] Ilutp(SparseValue s, double droptol, double thresh, string milu, double udiag, int nargout)
    {
        bool cplx = s.Im != null;
        var sm0 = From(s); int n = sm0.N;
        int opt = milu == "row" ? 1 : milu == "col" ? 2 : 0;
        var rcNorm = opt == 1 ? RowNorms(sm0) : ColNorms(sm0);
        var sm = opt == 1 ? Transpose(sm0) : sm0;
        var cidx_in = sm.Cp; var ridx_in = sm.Ri; var data_in = sm.V;
        var cidx_l = new int[n + 1]; var cidx_u = new int[n + 1];
        var ridx_l = new List<int>(); var data_l = new List<Complex>(); var ridx_u = new List<int>(); var data_u = new List<Complex>();
        var iwL = new SortedSet<int>(); var iwU = new SortedSet<int>();
        var w = new Complex[n]; var iperm = new int[n]; var perm = new int[n]; var uptr = new int[n];
        for (int i = 0; i < n; i++) { perm[i] = i; iperm[i] = i; }
        Complex tl = 0, partialCol = 0, partialRow = 0;
        for (int k = 0; k < n; k++)
        {
            for (int j = cidx_in[k]; j < cidx_in[k + 1]; j++)
            {
                int pp = iperm[ridx_in[j]];
                w[pp] = data_in[j];
                if (pp > k) iwL.Add(ridx_in[j]); else iwU.Add(pp);
            }
            Complex totalSum = 0;
            // iterate iw_u in increasing order while it can grow (only with entries > current)
            int cur = -1;
            while (true)
            {
                var view = iwU.GetViewBetween(cur + 1, int.MaxValue);
                if (view.Count == 0) break;
                int jrow = view.Min;
                if (jrow >= k) break;
                cur = jrow;
                if (opt == 2) partialCol = w[jrow];
                if (w[jrow] != Complex.Zero)
                {
                    if (opt == 1) { partialRow = w[jrow]; tl = w[jrow] / data_u[uptr[jrow]]; }
                    for (int jj = cidx_l[jrow]; jj < cidx_l[jrow + 1]; jj++)
                    {
                        int pp = iperm[ridx_l[jj]];
                        var aux = w[pp];
                        if (opt == 1) { w[pp] -= tl * data_l[jj]; partialRow += tl * data_l[jj]; }
                        else { tl = data_l[jj] * w[jrow]; w[pp] -= tl; if (opt == 2) partialCol += tl; }
                        if (aux == Complex.Zero && w[pp] != Complex.Zero) { if (pp > k) iwL.Add(ridx_l[jj]); else iwU.Add(pp); }
                    }
                    if (w[jrow].Magnitude < droptol * rcNorm[k] && w[jrow] != Complex.Zero)
                    {
                        if (opt == 2) totalSum += partialCol; else if (opt == 1) totalSum += partialRow;
                        w[jrow] = 0; iwU.Remove(jrow); continue;
                    }
                    else if (opt == 1) w[jrow] = tl;
                }
            }
            if (thresh > 0 && k < n - 1)
            {
                double maximum = w[k].Magnitude / thresh; int maxInd = perm[k]; int it2 = -1;
                foreach (var e in iwL) { int pp = iperm[e]; if (w[pp].Magnitude > maximum) { maximum = w[pp].Magnitude; maxInd = e; it2 = e; } }
                int ppm = iperm[maxInd];
                if (maxInd != perm[k])
                {
                    iwL.Remove(it2);
                    if (w[k] != Complex.Zero) iwL.Add(perm[k]); else iwU.Add(k);
                    iperm[perm[ppm]] = k; iperm[perm[k]] = ppm;
                    (perm[k], perm[ppm]) = (perm[ppm], perm[k]);
                    (w[k], w[ppm]) = (w[ppm], w[k]);
                }
            }
            if (droptol > 0)
                foreach (var e in iwL.ToList())
                {
                    int pp = iperm[e];
                    if (w[pp].Magnitude < droptol * rcNorm[k]) { if (opt != 0) totalSum += w[pp]; w[pp] = 0; iwL.Remove(e); }
                }
            if (opt != 0)
            {
                if (Gt(totalSum, 0, cplx) && w[k] == Complex.Zero) iwU.Add(k);
                w[k] += totalSum;
            }
            if (w[k] == Complex.Zero)
            {
                if (udiag != 1) throw new OctaveException("ilu: encountered a pivot equal to 0");
                w[k] = droptol; iwU.Add(k);
            }
            if (opt != 1) foreach (var e in iwL) { int pp = iperm[e]; w[pp] = w[pp] / w[k]; }
            int wlenU = 0;
            foreach (var e in iwU) { if (w[e] != Complex.Zero) { data_u.Add(w[e]); ridx_u.Add(e); wlenU++; } w[e] = 0; }
            int wlenL = 0;
            foreach (var e in iwL) { int pp = iperm[e]; if (w[pp] != Complex.Zero) { data_l.Add(w[pp]); ridx_l.Add(e); wlenL++; } w[pp] = 0; }
            if (opt == 1) uptr[k] = data_u.Count - 1;
            cidx_u[k + 1] = cidx_u[k] + wlenU; cidx_l[k + 1] = cidx_l[k] + wlenL;
            iwL.Clear(); iwU.Clear();
        }
        Csc L, U;
        if (opt == 1)
        {
            var Uv = new Csc(n, cidx_l, ridx_l.ToArray(), data_l.ToArray());
            var cp = new int[n + 1]; var ri = new List<int>(); var v = new List<Complex>();
            var dg = new Complex[n];
            for (int i = 0; i < n; i++)
            {
                dg[i] = data_u[uptr[i]];
                for (int j = cidx_u[i]; j < cidx_u[i + 1]; j++) { if (ridx_u[j] == i) continue; ri.Add(ridx_u[j]); v.Add(data_u[j]); }
                cp[i + 1] = ri.Count;
            }
            var Lv = new Csc(n, cp, ri.ToArray(), v.ToArray());
            var D = new Csc(n, Enumerable.Range(0, n + 1).ToArray(), Enumerable.Range(0, n).ToArray(), dg);
            U = Add(Transpose(Uv), ColPerm(D, perm));
            L = Transpose(Lv);
        }
        else
        {
            L = new Csc(n, cidx_l, ridx_l.ToArray(), data_l.ToArray());
            U = new Csc(n, cidx_u, ridx_u.ToArray(), data_u.ToArray());
        }
        var I = Eye(n);
        if (opt == 1)
        {
            var L0 = Add(L, I);
            if (nargout == 3) return new Value[] { To(L0, cplx), To(ColPerm(U, perm), cplx), To(ColPerm(I, perm), false) };
            return new Value[] { To(L0, cplx), To(U, cplx) };
        }
        if (nargout == 3) return new Value[] { To(Add(RowPerm(L, perm), I), cplx), To(U, cplx), To(RowPerm(I, perm), false) };
        return new Value[] { To(Add(L, ColPerm(I, perm)), cplx), To(U, cplx) };
    }
}
