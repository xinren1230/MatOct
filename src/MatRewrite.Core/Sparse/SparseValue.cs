// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Sparse;

/// <summary>Compressed-column (CSC) sparse matrix (double or logical, real or complex).</summary>
public sealed class SparseValue : Value
{
    public int R, C;
    public int[] Cp;
    public int[] Ri;
    public double[] Re;
    public double[]? Im;
    public bool Logical;

    public SparseValue(int r, int c, int[] cp, int[] ri, double[] re, double[]? im = null, bool logical = false)
    { R = r; C = c; Cp = cp; Ri = ri; Re = re; Im = im; Logical = logical; }

    public static SparseValue Zeros(int r, int c, bool logical = false) => new(r, c, new int[c + 1], Array.Empty<int>(), Array.Empty<double>(), null, logical);

    public override int Rows => R;
    public override int Cols => C;
    public override string ClassName => Logical ? "logical" : "double";
    public int Nnz => Cp[C];
    public bool IsComplex => Im != null;

    public SparseValue Copy() => new(R, C, (int[])Cp.Clone(), (int[])Ri.Clone(), (double[])Re.Clone(), (double[]?)Im?.Clone(), Logical);

    public static SparseValue FromDense(MatrixValue m, bool? logical = null)
    {
        if (m.ND != null) throw new OctaveException("sparse: N-D arrays are not supported");
        int r = m.R, c = m.C;
        var cp = new int[c + 1];
        var ri = new List<int>(); var re = new List<double>(); var im = m.Im != null ? new List<double>() : null;
        bool lg = logical ?? m.IsLogical;
        for (int j = 0; j < c; j++)
        {
            for (int i = 0; i < r; i++)
            {
                int k = i + j * r;
                double x = m.Re[k], y = m.Im?[k] ?? 0;
                if (x != 0 || y != 0) { ri.Add(i); re.Add(lg ? 1 : x); im?.Add(lg ? 0 : y); }
            }
            cp[j + 1] = ri.Count;
        }
        var s = new SparseValue(r, c, cp, ri.ToArray(), re.ToArray(), im?.ToArray(), lg);
        if (s.Im != null && s.Im.All(v => v == 0)) s.Im = null;
        return s;
    }

    public MatrixValue ToDense()
    {
        var m = new MatrixValue(R, C, Logical ? NumKind.Logical : NumKind.Double, Im != null);
        for (int j = 0; j < C; j++)
            for (int p = Cp[j]; p < Cp[j + 1]; p++)
            {
                m.Re[Ri[p] + j * R] = Re[p];
                if (Im != null) m.Im![Ri[p] + j * R] = Im[p];
            }
        return m;
    }

    /// <summary>Build from 0-based triplets; duplicates summed (OR for logical) or last-wins.</summary>
    public static SparseValue FromTriplets(int r, int c, IList<int> I, IList<int> J, IList<double> V, IList<double>? Vi = null, bool logical = false, bool sumDup = true)
    {
        int n = I.Count;
        var count = new int[c + 1];
        for (int k = 0; k < n; k++) count[J[k] + 1]++;
        for (int j = 0; j < c; j++) count[j + 1] += count[j];
        var pos = (int[])count.Clone();
        var ri = new int[n]; var re = new double[n]; var im = Vi != null ? new double[n] : null;
        for (int k = 0; k < n; k++) { int q = pos[J[k]]++; ri[q] = I[k]; re[q] = V[k]; if (im != null) im[q] = Vi![k]; }
        var cp = new int[c + 1];
        var ori = new List<int>(n); var ore = new List<double>(n); var oim = im != null ? new List<double>(n) : null;
        var idx = new List<int>();
        for (int j = 0; j < c; j++)
        {
            idx.Clear();
            for (int q = count[j]; q < count[j + 1]; q++) idx.Add(q);
            idx.Sort((a, b) => ri[a] != ri[b] ? ri[a].CompareTo(ri[b]) : a.CompareTo(b));
            int start = ori.Count;
            foreach (var q in idx)
            {
                if (ori.Count > start && ori[^1] == ri[q])
                {
                    if (sumDup) { ore[^1] = logical ? ((ore[^1] != 0 || re[q] != 0) ? 1 : 0) : ore[^1] + re[q]; if (oim != null) oim[^1] += im![q]; }
                    else { ore[^1] = re[q]; if (oim != null) oim[^1] = im![q]; }
                }
                else { ori.Add(ri[q]); ore.Add(logical ? (re[q] != 0 ? 1 : 0) : re[q]); oim?.Add(im![q]); }
            }
            int w = start;
            for (int t = start; t < ori.Count; t++)
                if (ore[t] != 0 || (oim != null && oim[t] != 0)) { ori[w] = ori[t]; ore[w] = ore[t]; if (oim != null) oim[w] = oim[t]; w++; }
            ori.RemoveRange(w, ori.Count - w); ore.RemoveRange(w, ore.Count - w); oim?.RemoveRange(w, oim.Count - w);
            cp[j + 1] = ori.Count;
        }
        var s = new SparseValue(r, c, cp, ori.ToArray(), ore.ToArray(), oim?.ToArray(), logical);
        if (s.Im != null && s.Im.All(v => v == 0)) s.Im = null;
        return s;
    }

    public Complex Get(int i, int j)
    {
        int lo = Cp[j], hi = Cp[j + 1] - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (Ri[mid] == i) return new Complex(Re[mid], Im?[mid] ?? 0);
            if (Ri[mid] < i) lo = mid + 1; else hi = mid - 1;
        }
        return Complex.Zero;
    }

    public void Set(int i, int j, double re, double im = 0)
    {
        if (Logical) { re = (re != 0 || im != 0) ? 1 : 0; im = 0; }
        int lo = Cp[j], hi = Cp[j + 1];
        int p = lo;
        while (p < hi && Ri[p] < i) p++;
        bool exists = p < hi && Ri[p] == i;
        bool zero = re == 0 && im == 0;
        if (exists)
        {
            if (!zero) { Re[p] = re; if (Im != null) Im[p] = im; else if (im != 0) { Im = new double[Re.Length]; Im[p] = im; } return; }
            Ri = Remove(Ri, p); Re = Remove(Re, p); if (Im != null) Im = Remove(Im, p);
            for (int k = j + 1; k <= C; k++) Cp[k]--;
            return;
        }
        if (zero) return;
        Ri = Insert(Ri, p, i); Re = Insert(Re, p, re);
        if (Im != null) Im = Insert(Im, p, im);
        else if (im != 0) { Im = new double[Re.Length]; Im[p] = im; }
        for (int k = j + 1; k <= C; k++) Cp[k]++;
    }

    static T[] Insert<T>(T[] a, int p, T v) { var r = new T[a.Length + 1]; Array.Copy(a, r, p); r[p] = v; Array.Copy(a, p, r, p + 1, a.Length - p); return r; }
    static T[] Remove<T>(T[] a, int p) { var r = new T[a.Length - 1]; Array.Copy(a, r, p); Array.Copy(a, p + 1, r, p, a.Length - p - 1); return r; }

    public SparseValue Transpose(bool conj)
    {
        int nz = Nnz;
        var cnt = new int[R + 1];
        for (int p = 0; p < nz; p++) cnt[Ri[p] + 1]++;
        for (int i = 0; i < R; i++) cnt[i + 1] += cnt[i];
        var pos = (int[])cnt.Clone();
        var ri = new int[nz]; var re = new double[nz]; var im = Im != null ? new double[nz] : null;
        for (int j = 0; j < C; j++)
            for (int p = Cp[j]; p < Cp[j + 1]; p++)
            {
                int q = pos[Ri[p]]++;
                ri[q] = j; re[q] = Re[p]; if (im != null) im[q] = conj ? -Im![p] : Im![p];
            }
        return new SparseValue(C, R, cnt, ri, re, im, Logical);
    }

    public SparseValue MapNz(Func<double, double> f, bool? logical = null)
        => new SparseValue(R, C, (int[])Cp.Clone(), (int[])Ri.Clone(), Re.Take(Nnz).Select(f).ToArray(), null, logical ?? Logical).Prune();

    public SparseValue Prune()
    {
        bool ok = true;
        for (int p = 0; p < Nnz; p++) if (Re[p] == 0 && (Im == null || Im[p] == 0)) { ok = false; break; }
        if (ok) return this;
        var I = new List<int>(); var J = new List<int>(); var V = new List<double>(); var Vi = Im != null ? new List<double>() : null;
        for (int j = 0; j < C; j++)
            for (int p = Cp[j]; p < Cp[j + 1]; p++)
                if (Re[p] != 0 || (Im != null && Im[p] != 0)) { I.Add(Ri[p]); J.Add(j); V.Add(Re[p]); Vi?.Add(Im![p]); }
        return FromTriplets(R, C, I, J, V, Vi, Logical);
    }

    public CSparse.Double.SparseMatrix ToCSparse()
    {
        if (Im != null) throw new OctaveException("sparse solver: complex sparse matrices are not supported");
        return new CSparse.Double.SparseMatrix(R, C, Re.Take(Nnz).ToArray(), Ri.Take(Nnz).ToArray(), (int[])Cp.Clone());
    }

    public static SparseValue FromCSparse(CSparse.Storage.CompressedColumnStorage<double> m)
    {
        var I = new List<int>(); var J = new List<int>(); var V = new List<double>();
        for (int j = 0; j < m.ColumnCount; j++) for (int p = m.ColumnPointers[j]; p < m.ColumnPointers[j + 1]; p++) { I.Add(m.RowIndices[p]); J.Add(j); V.Add(m.Values[p]); }
        return FromTriplets(m.RowCount, m.ColumnCount, I, J, V);
    }
}
