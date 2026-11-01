// MatRewrite - GPL-3.0-or-later
// tensorprod / uniquetol / misc ports of Octave scripts.
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static bool Bool(Value v) => v is MatrixValue m && m.Numel > 0 && m.Re.Take(m.Numel).All(x => x != 0);
    static MatrixValue RowD(IEnumerable<double> v) { var a = v.ToArray(); return new MatrixValue(1, a.Length, a, null); }

    static void RegisterPhase8Misc()
    {
        Reg("hadamard", (it, a, n) =>
        {
            if (a.Length != 1) Usage("hadamard");
            double N = D(a[0], "hadamard"); int k = 0;
            while (N > 1 && Math.Floor(N / 2) == N / 2) { k++; N /= 2; }
            if (N != 1) k -= 2;
            if (k < 0) N = -1;
            Value h;
            if (N == 1) h = Dn(1);
            else if (N == 3 || N == 5)
            {
                double[] tu = N == 3 ? new double[] { -1, 1, -1, 1, 1, 1, -1, -1, -1, 1, -1 } : new double[] { 1, -1, -1, 1, 1, 1, 1, -1, 1, -1, 1, -1, -1, -1, -1, 1, 1, -1, -1 };
                double[] tl = N == 3 ? new double[] { -1, -1, 1, -1, -1, -1, 1, 1, 1, -1, 1 } : new double[] { 1, -1, -1, 1, 1, -1, -1, -1, -1, 1, -1, 1, -1, 1, 1, 1, 1, -1, -1 };
                var t = (MatrixValue)Fc(it, "toeplitz", RowD(tu), RowD(tl));
                if (N == 5) t = (MatrixValue)Fc(it, "fliplr", t);
                int s = tu.Length + 1; var tt = t;
                h = IdxMat(s, s, (i, j) => i == 0 || j == 0 ? 1 : tt.Re[(i - 1) + (j - 1) * (s - 1)]);
            }
            else if (N == 7) { const string H = "1111111111111111111111111111110000000100011111011110101010100001011001000011010111111001000111110111110000000101100010011011111000111010100010000101100110001100011111101000001010001011101111010101101111001000011100001001111010011111000001000101111001011111000000111010000011100111101110000101100011011001100110011100011010011011001001101000111001110011000001111001111110011000000110100110100111011010101101001001010100101101001011100000011010111100110101111100101000010000101111010101010111010010110100001010101111101001010011000010111010110100111010000011010011001010000111010110000011111110011100110001100110001100110011010010001011101001001110100110110101001010101000111110110011010010011101000100101101100010111011100100100011100100101011010101001100011011001100010011011100110010"; h = IdxMat(28, 28, (i, j) => H[i * 28 + j] == '1' ? 1 : -1); }
            else throw new OctaveException("hadamard: N must be 2^k*p, for p = 1, 12, 20 or 28");
            Value h2 = IdxMat(2, 2, (i, j) => i == 1 && j == 1 ? -1 : 1);
            while (true)
            {
                if (k % 2 != 0) h = Fc(it, "kron", h2, h);
                k /= 2;
                if (k == 0) break;
                h2 = Fc(it, "kron", h2, h2);
            }
            return new[] { h };
        });
        Reg("magic", (it, a, n) =>
        {
            if (a.Length != 1) Usage("magic");
            double N = Math.Truncate(D(a[0], "magic"));
            if (N < 0) throw new OctaveException("magic: N must be non-negative");
            if (N == 2) return new Value[] { IdxMat(2, 2, (i, j) => new double[] { 4, 3, 1, 2 }[i * 2 + j]) };
            return new Value[] { Magic((int)N) };
        });

        Reg("uniquetol", (it, a, n) =>
        {
            const string F = "uniquetol";
            if (a.Length < 1) Usage(F);
            bool sparseIn = a[0] is Sparse.SparseValue;
            Value A0 = sparseIn ? Fc(it, "full", a[0]) : a[0];
            if (!(IsFlt(A0) && !((MatrixValue)A0).IsComplex))
                throw new OctaveException("uniquetol: A must be a real floating point array", "Octave:uniquetol:unsupported-type");
            var A = (MatrixValue)A0; bool single = A.IsSingle;
            var va = a.Skip(1).ToList(); double tol;
            if (va.Count == 0 || (va[0] is MatrixValue c0 && c0.IsChar)) tol = single ? 1e-6 : 1e-12;
            else
            {
                var t = va[0]; va.RemoveAt(0);
                if (!(IsFlt(t) && IsScal(t) && !((MatrixValue)t).IsComplex))
                    throw new OctaveException("uniquetol: TOL must be a real floating point scalar", "Octave:uniquetol:unsupported-type");
                tol = ((MatrixValue)t).Re[0];
            }
            if (va.Count % 2 != 0) throw new OctaveException("uniquetol: PROPERTY/VALUE arguments must occur in pairs");
            bool byRows = false, allIdx = false, calc = n > 1; double[]? ds = null;
            var sz = SizeOf(A);
            for (int k = 0; k < va.Count; k += 2)
            {
                if (!(va[k] is MatrixValue pk && pk.IsChar)) throw new OctaveException("uniquetol: PROPERTY must be a string");
                var name = pk.AsString().ToLowerInvariant(); var v = va[k + 1];
                if (name == "byrows")
                {
                    byRows = Bool(Fc(it, "logical", v));
                    if (byRows && sz.Length > 2) throw new OctaveException("uniquetol: A must be a 2-D array when \"ByRows\" is true");
                }
                else if (name == "outputallindices") allIdx = Bool(Fc(it, "logical", v)) && calc;
                else if (name == "datascale")
                {
                    if (!IsFlt(v) || ((MatrixValue)v).IsComplex || ((MatrixValue)v).Re.Take(NumelOf(v)).Any(x => x < 0 || double.IsNaN(x)))
                        throw new OctaveException("uniquetol: DataScale must be a positive floating point scalar or vector, without NaNs");
                    ds = ((MatrixValue)v).Re.Take(NumelOf(v)).ToArray();
                    if (ds.Length != 1 && ds.Length != sz[1]) throw new OctaveException("uniquetol: invalid DataScale size");
                }
                else throw new OctaveException($"uniquetol: unknown property '{pk.AsString()}'");
            }
            Value Fin(Value c) { if (single && c is MatrixValue cm && !cm.IsSingle) cm.CastInPlace(NumKind.Single); return sparseIn ? Fc(it, "sparse", c) : c; }
            MatrixValue ColV(IEnumerable<double> v) { var x = v.ToArray(); return new MatrixValue(x.Length, 1, x, null); }
            if (A.Numel == 0)
            {
                if (byRows)
                {
                    var s2 = sz.ToArray(); s2[1] = 1; var o = FillN(s2, 1);
                    return new Value[] { Fin(A), o, FillN(s2, 1) };
                }
                var c = new MatrixValue(sz[0] == 1 ? 1 : 0, sz[0] == 1 ? 0 : 1, Array.Empty<double>(), null);
                return new Value[] { Fin(c), new MatrixValue(0, 1, Array.Empty<double>(), null), new MatrixValue(0, 1, Array.Empty<double>(), null) };
            }
            int N = A.Numel; var av = A.Re.Take(N).ToArray();
            double[] tolv;
            if (ds == null) { var fin = av.Where(x => !double.IsInfinity(x) && !double.IsNaN(x)).Select(Math.Abs).ToArray(); double m = fin.Length > 0 ? fin.Max() : (av.Any(x => !double.IsInfinity(x)) ? double.NaN : double.NaN); tolv = new[] { tol * m }; }
            else tolv = ds.Select(d => tol * d).ToArray();
            if (byRows)
            {
                int nr = sz[0], nc = sz[1];
                var sr = Fn(it, "sortrows", 2, A);
                var As = (MatrixValue)sr[0]; var srtA = ((MatrixValue)sr[1]).Re.Select(x => (int)x).ToArray();
                var inv = new int[nr]; for (int i = 0; i < nr; i++) inv[srtA[i] - 1] = i + 1;
                double At(int i, int j) => As.Re[i + j * nr];
                var I = new List<int>(); var J = new int[nr]; var iaCells = new List<Value>(); int jj = 1;
                for (int i = 0; i < nr; i++)
                {
                    if (J[i] != 0) continue;
                    bool anyNonFin = false; for (int q = 0; q < nc; q++) if (double.IsNaN(At(i, q)) || double.IsInfinity(At(i, q))) anyNonFin = true;
                    var eq = new bool[nr];
                    for (int r = 0; r < nr; r++)
                    {
                        bool all = true;
                        for (int q = 0; q < nc && all; q++) { double tq = tolv.Length == 1 ? tolv[0] : tolv[q]; if (!(Math.Abs(At(r, q) - At(i, q)) <= tq)) all = false; }
                        eq[r] = all;
                    }
                    eq[i] = eq[i] || anyNonFin;
                    var idx = Enumerable.Range(0, nr).Where(r => eq[r]).ToList();
                    if (allIdx) iaCells.Add(ColV(idx.Select(r => (double)srtA[r]).OrderBy(x => x)));
                    I.Add(idx[0]);
                    foreach (var r in idx) J[r] = jj;
                    jj++;
                }
                var c = IdxMat(I.Count, nc, (r, q) => At(I[r], q));
                Value ia = allIdx ? new CellValue(iaCells.Count, 1, iaCells.ToArray()) : ColV(I.Select(r => (double)srtA[r]));
                var ic = ColV(inv.Select(k => (double)J[k - 1]));
                return new Value[] { Fin(c), ia, ic };
            }
            else
            {
                bool isRow = sz.Length == 2 && sz[0] == 1;
                double T = tolv[0];
                var sAi = Enumerable.Range(0, N).OrderBy(k => double.IsNaN(av[k]) ? 1 : 0).ThenBy(k => double.IsNaN(av[k]) ? 0 : av[k]).ToArray();
                var sortA = sAi.Select(k => av[k]).ToArray();
                int numnan = sortA.Count(double.IsNaN);
                var diffs = new double[Math.Max(N - 1, 0)]; for (int k = 0; k + 1 < N; k++) diffs[k] = sortA[k + 1] - sortA[k];
                if (sortA.Any(double.IsInfinity))
                {
                    var nin = sortA.Where(x => !double.IsInfinity(x) && !double.IsNaN(x)).ToArray();
                    double rep = nin.Length > 0 ? Math.Abs(nin[^1] - nin[0]) + 10 : double.NaN;
                    for (int k = 0; k < diffs.Length; k++) if (double.IsInfinity(diffs[k])) diffs[k] = rep;
                }
                var csdx = new double[diffs.Length]; double acc = 0; for (int k = 0; k < diffs.Length; k++) { acc += diffs[k]; csdx[k] = acc; }
                var ue = new bool[N]; ue[0] = true;
                if (csdx.Length > 0)
                {
                    var modv = (MatrixValue)Fc(it, "mod", ColV(csdx), Dn(T));
                    double mx = csdx.Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Max();
                    double ep = D(Fc(it, "eps", Dn(mx)), F);
                    double prev = 0;
                    for (int k = 0; k < csdx.Length; k++) { double cur = csdx[k] - modv.Re[k]; ue[k + 1] = cur - prev > ep; prev = cur; }
                }
                while (true)
                {
                    var ueIdx = Enumerable.Range(0, N).Where(k => ue[k]).ToList();
                    var move = new List<int>();
                    for (int q = 1; q < ueIdx.Count; q++) if (sortA[ueIdx[q]] - sortA[ueIdx[q - 1]] < T) move.Add(ueIdx[q]);
                    if (move.Count == 0) break;
                    var old = (bool[])ue.Clone();
                    foreach (var m in move) ue[m] = false;
                    foreach (var m in move) if (m + 1 < N - numnan) ue[m + 1] = true;
                    if (old.SequenceEqual(ue)) break;
                }
                var cList = Enumerable.Range(0, N).Where(k => ue[k]).Select(k => sortA[k]).ToList();
                var cum = new int[N]; int cc = 0; for (int k = 0; k < N; k++) { if (ue[k]) cc++; cum[k] = cc; }
                var ic = new double[N]; for (int k = 0; k < N; k++) ic[sAi[k]] = cum[k];
                var nanIdx = Enumerable.Range(0, N).Where(k => double.IsNaN(av[k])).ToList();
                var iaList = new List<Value>(); var iaD = new List<double>();
                if (allIdx) { int nu = cc; for (int u = 1; u <= nu; u++) iaList.Add(ColV(Enumerable.Range(0, N).Where(k => cum[k] == u).Select(k => sAi[k]).Where(k => !nanIdx.Contains(k)).Select(k => (double)(k + 1)).OrderBy(x => x))); }
                else iaD = Enumerable.Range(0, N).Where(k => ue[k]).Select(k => (double)(sAi[k] + 1)).ToList();
                if (nanIdx.Count > 0)
                {
                    int baseRow = nanIdx.Count == N ? 0 : cList.Count;
                    for (int q = 0; q < nanIdx.Count; q++)
                    {
                        int row = baseRow + q;
                        while (cList.Count <= row) cList.Add(0);
                        cList[row] = double.NaN; ic[nanIdx[q]] = row + 1;
                        if (allIdx) { while (iaList.Count <= row) iaList.Add(new MatrixValue(0, 0)); iaList[row] = Dn(nanIdx[q] + 1); }
                        else { while (iaD.Count <= row) iaD.Add(0); iaD[row] = nanIdx[q] + 1; }
                    }
                }
                var cm = ColV(cList); Value c = isRow ? Fc(it, "transpose", cm) : cm;
                Value ia = allIdx ? new CellValue(iaList.Count, 1, iaList.ToArray()) : ColV(iaD);
                return new Value[] { Fin(c), ia, ColV(ic) };
            }
        });

        Reg("tensorprod", (it, a, n) =>
        {
            const string F = "tensorprod";
            if (a.Length < 2 || a.Length > 6) Usage(F);
            if (!IsFlt(a[0])) throw new OctaveException("tensorprod: A must be a single or double precision array");
            if (!IsFlt(a[1])) throw new OctaveException("tensorprod: B must be a single or double precision array");
            var va = a.Skip(2).ToList();
            bool IsStr(Value v, string s) => v is MatrixValue m && m.IsChar && string.Equals(m.AsString(), s, StringComparison.OrdinalIgnoreCase);
            int numDimA = 0;
            if (va.Count > 0 && IsStr(va[^1], "NumDimensionsA")) throw new OctaveException("tensorprod: a value for the NumDimensionsA property must be provided");
            if (va.Count > 1 && IsStr(va[^2], "NumDimensionsA"))
            {
                var nv = va[^1];
                if (!(nv is MatrixValue nm && !nm.IsChar && !nm.IsLogical && IsScal(nv))) throw new OctaveException("tensorprod: value for NumDimensionsA must be a numeric scalar");
                double d = ((MatrixValue)nv).Re[0];
                if (!(d >= 1 && d == Math.Floor(d) && !double.IsInfinity(d))) throw new OctaveException("tensorprod: value for NumDimensionsA must be a positive integer");
                numDimA = (int)d; va.RemoveRange(va.Count - 2, 2);
            }
            bool IsNumV(Value v) => v is MatrixValue m && !m.IsChar && (IsVec(v) || SizeOf(v).SequenceEqual(new[] { 0, 0 }));
            double[] dimA, dimB; int ndimargs = va.Count;
            var A = a[0]; var B = a[1];
            if (ndimargs == 0) { dimA = Array.Empty<double>(); dimB = dimA; }
            else if (ndimargs == 1)
            {
                var v = va[0];
                if (v is MatrixValue m && m.IsChar)
                {
                    if (!IsStr(v, "all")) throw new OctaveException($"tensorprod: unknown option '{m.AsString()}'");
                    if (!SizeOf(A).SequenceEqual(SizeOf(B))) throw new OctaveException("tensorprod: size of A and B must be identical when using the 'all' option");
                    dimA = Enumerable.Range(1, SizeOf(A).Length).Select(x => (double)x).ToArray();
                }
                else if (v is MatrixValue mm && !mm.IsChar)
                {
                    if (!IsNumV(v)) throw new OctaveException("tensorprod: DIM must be a numeric vector of integers or []");
                    dimA = mm.Re.Take(mm.Numel).ToArray();
                }
                else throw new OctaveException("tensorprod: third argument must be a numeric vector of integers, [], or 'all'");
                dimB = dimA;
            }
            else if (ndimargs == 2)
            {
                if (!IsNumV(va[0])) throw new OctaveException("tensorprod: DIMA must be a numeric vector of integers or []");
                if (!IsNumV(va[1])) throw new OctaveException("tensorprod: DIMB must be a numeric vector of integers or []");
                var m1 = (MatrixValue)va[0]; var m2 = (MatrixValue)va[1];
                if (m1.Numel != m2.Numel) throw new OctaveException("tensorprod: an equal number of dimensions must be matched for A and B");
                dimA = m1.Re.Take(m1.Numel).ToArray(); dimB = m2.Re.Take(m2.Numel).ToArray();
            }
            else
            {
                foreach (var v in va)
                {
                    if (v is MatrixValue m && m.IsChar)
                    {
                        if (IsStr(v, "NumDimensionsA")) throw new OctaveException("tensorprod: misplaced 'NumDimensionsA' option");
                        if (IsStr(v, "all")) throw new OctaveException("tensorprod: misplaced 'all' option");
                        throw new OctaveException($"tensorprod: unknown option '{m.AsString()}'");
                    }
                    if (v is not MatrixValue) throw new OctaveException("tensorprod: optional arguments must be numeric vectors of integers, [], 'all', or 'NumDimensionsA'");
                }
                throw new OctaveException("tensorprod: too many dimension inputs given");
            }
            if (dimA.Concat(dimB).Any(d => !(d >= 1 && d == Math.Floor(d) && !double.IsInfinity(d)))) throw new OctaveException("tensorprod: dimensions must be positive integers");
            var sA0 = SizeOf(A); var sB0 = SizeOf(B);
            int Sz(int[] s, int d) => d <= s.Length ? s[d - 1] : 1;
            for (int k = 0; k < dimA.Length; k++)
                if (Sz(sA0, (int)dimA[k]) != Sz(sB0, (int)dimB[k])) throw new OctaveException("tensorprod: matched dimensions of A and B must have the same lengths");
            int ndA = Math.Max(sA0.Length, dimA.Length > 0 ? (int)dimA.Max() : 0);
            int ndB = Math.Max(sB0.Length, dimB.Length > 0 ? (int)dimB.Max() : 0);
            var sizeA = Enumerable.Range(1, ndA).Select(d => Sz(sA0, d)).ToList();
            var sizeB = Enumerable.Range(1, ndB).Select(d => Sz(sB0, d)).ToArray();
            if (numDimA > 0)
            {
                if (numDimA < ndA)
                    throw new OctaveException(ndimargs == 1 ? "tensorprod: highest dimension of dim must be less than or equal to NumDimensionsA"
                        : ndimargs == 2 ? "tensorprod: highest dimension of dimA must be less than or equal to NumDimensionsA"
                        : "tensorprod: NumDimensionsA cannot be smaller than the number of dimensions of A");
                while (sizeA.Count < numDimA) sizeA.Add(1);
                ndA = numDimA;
            }
            var dA = dimA.Select(x => (int)x).ToArray(); var dB = dimB.Select(x => (int)x).ToArray();
            var remA = Enumerable.Range(1, ndA).Where(d => !dA.Contains(d)).ToArray();
            var remB = Enumerable.Range(1, ndB).Where(d => !dB.Contains(d)).ToArray();
            double P(IEnumerable<int> dd, IList<int> s) => dd.Aggregate(1.0, (p, d) => p * s[d - 1]);
            Value Perm(Value X, int[] order) => order.Length < 2 ? X : Fc(it, "permute", X, RowD(order.Select(x => (double)x)));
            var newA = Fc(it, "reshape", Perm(A, remA.Concat(dA).ToArray()), RowD(new[] { P(remA, sizeA), P(dA, sizeA) }));
            var newB = Fc(it, "reshape", Perm(B, remB.Concat(dB).ToArray()), RowD(new[] { P(remB, sizeB), P(dB, sizeB) }));
            var C = Bx("*", newA, Fc(it, "transpose", newB));
            if (!IsScal(C))
            {
                var dims = remA.Select(d => (double)sizeA[d - 1]).Concat(remB.Select(d => (double)sizeB[d - 1])).ToList();
                if (dims.Count < 2) dims.Add(1);
                C = Fc(it, "reshape", C, RowD(dims));
            }
            return new[] { C };
        });
    }
}
