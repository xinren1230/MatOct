// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MatRewrite.Core.Runtime;
using MatRewrite.Core.Sparse;

namespace MatRewrite.Core.Builtins;

/// <summary>Sparse matrices (true CSC storage, see Sparse/SparseValue.cs).</summary>
public static partial class BuiltinLibrary
{
    static SparseValue SpArg(Value v, string fn) => v switch
    {
        SparseValue s => s,
        MatrixValue m => SparseValue.FromDense(m.IsChar ? m.AsDouble() : m),
        _ => throw new OctaveException($"{fn}: wrong type argument '{v.ClassName}'")
    };

    static MatrixValue Shadow(SparseValue s) => new(s.R, s.C, Array.Empty<double>(), s.Im != null ? Array.Empty<double>() : null, s.Logical ? NumKind.Logical : NumKind.Double);

    static void RegisterSparse()
    {
        // metadata builtins: answer from shape/class without densifying
        foreach (var name in new[] { "size", "numel", "rows", "columns", "length", "ndims", "isempty", "class", "isnumeric", "isreal",
                                     "islogical", "isfloat", "isa", "isscalar", "isvector", "ismatrix", "issquare", "isrow", "iscolumn" })
        {
            if (!cur!.Builtins.TryGetValue(name, out var orig)) continue;
            cur.Builtins[name] = (it, a, n) => orig(it, a.Select(x => x is SparseValue s ? Shadow(s) : x).ToArray(), n);
        }

        Reg1("sparse", a =>
        {
            if (a.Length == 1) return a[0] is SparseValue s0 ? s0 : SpArg(a[0], "sparse");
            if (a.Length == 2) return SparseValue.Zeros(I(a[0], "sparse"), I(a[1], "sparse"));
            var Iv = M(Dn(a[0]), "sparse"); var Jv = M(Dn(a[1]), "sparse"); var V = M(Dn(a[2]), "sparse");
            int n = Math.Max(Iv.Numel, Math.Max(Jv.Numel, V.Numel));
            foreach (var x in new[] { Iv, Jv, V }) if (x.Numel != 1 && x.Numel != n) throw new OctaveException("sparse: dimension mismatch");
            double G(MatrixValue x, int k) => x.Numel == 1 ? x.Re[0] : x.Re[k];
            int rows = a.Length >= 5 ? I(a[3], "sparse") : (n == 0 ? 0 : (int)Enumerable.Range(0, n).Max(k => G(Iv, k)));
            int cols = a.Length >= 5 ? I(a[4], "sparse") : (n == 0 ? 0 : (int)Enumerable.Range(0, n).Max(k => G(Jv, k)));
            bool sum = !(a.Length >= 6 && a[5] is MatrixValue o && o.IsChar && o.AsString() == "unique");
            var II = new int[n]; var JJ = new int[n]; var VV = new double[n]; double[]? VI = V.Im != null ? new double[n] : null;
            for (int k = 0; k < n; k++)
            {
                int i = (int)G(Iv, k) - 1, j = (int)G(Jv, k) - 1;
                if (i < 0 || j < 0) throw new OctaveException("sparse: row and column indices must be positive");
                if (i >= rows || j >= cols) throw new OctaveException($"sparse: row index {i + 1} out of bound {rows}");
                II[k] = i; JJ[k] = j; VV[k] = G(V, k); if (VI != null) VI[k] = V.Numel == 1 ? V.Im![0] : V.Im![k];
            }
            return SparseValue.FromTriplets(rows, cols, II, JJ, VV, VI, V.IsLogical, sum);
        });
        Reg1("full", a => a[0] is SparseValue s ? s.ToDense() : a[0]);
        Reg1("issparse", a => Bool(a[0] is SparseValue));
        Reg1("nnz", a => a[0] is SparseValue s ? Num(s.Nnz) : Num(M(a[0], "nnz").Re.Select((x, k) => x != 0 || (M(a[0], "nnz").Im?[k] ?? 0) != 0).Count(b => b)));
        Reg1("nzmax", a => a[0] is SparseValue s ? Num(Math.Max(1, s.Nnz)) : Num(M(a[0], "nzmax").Numel));
        Reg1("nonzeros", a =>
        {
            var s = SpArg(a[0], "nonzeros");
            return new MatrixValue(s.Nnz, 1, s.Re[..s.Nnz], s.Im?[..s.Nnz], s.Logical ? NumKind.Logical : NumKind.Double);
        });
        var denseFind = cur!.Builtins["find"];
        Reg("find", (it, a, n) =>
        {
            if (a[0] is not SparseValue s) return denseFind(it, a, n);
            if (a.Length > 1) return denseFind(it, new Value[] { s.ToDense() }.Concat(a.Skip(1)).ToArray(), n);
            int nz = s.Nnz;
            bool row = s.R == 1 && s.C != 1;
            MatrixValue Vec(Func<int, int, double> f)
            {
                var m = row ? new MatrixValue(1, nz) : new MatrixValue(nz, 1); int k = 0;
                for (int j = 0; j < s.C; j++) for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++) m.Re[k++] = f(p, j);
                return m;
            }
            if (n <= 1) return new Value[] { Vec((p, j) => s.Ri[p] + j * s.R + 1) };
            var outs = new List<Value> { Vec((p, j) => s.Ri[p] + 1), Vec((p, j) => j + 1) };
            if (n >= 3)
            {
                var v = Vec((p, j) => s.Re[p]);
                if (s.Im != null) { v.Im = new double[nz]; int k = 0; for (int p = 0; p < nz; p++) v.Im[k++] = s.Im[p]; }
                if (s.Logical) v.Kind = NumKind.Logical;
                outs.Add(v);
            }
            return outs.ToArray();
        });
        Reg1("speye", a =>
        {
            int r, c;
            if (a.Length > 2) Usage("speye");
            if (a.Length == 0) r = c = 1;
            else if (a.Length == 1) { var d = M(a[0], "speye"); if (d.Numel is not (1 or 2)) Usage("speye"); r = (int)d.Re[0]; c = d.Numel > 1 ? (int)d.Re[1] : r; }
            else
            {
                if (!(a[0] is MatrixValue m0 && m0.Numel == 1 && a[1] is MatrixValue m1 && m1.Numel == 1)) throw new OctaveException("speye: M and N must be scalar dimensions");
                r = I(a[0], "speye"); c = I(a[1], "speye");
            }
            r = Math.Max(0, r); c = Math.Max(0, c);
            int k = Math.Min(r, c);
            return SparseValue.FromTriplets(r, c, Enumerable.Range(0, k).ToArray(), Enumerable.Range(0, k).ToArray(), Enumerable.Repeat(1.0, k).ToArray());
        });
        Reg1("spones", a => { var s = SpArg(a[0], "spones"); return new SparseValue(s.R, s.C, (int[])s.Cp.Clone(), s.Ri[..s.Nnz], Enumerable.Repeat(1.0, s.Nnz).ToArray()); });
        Reg1("spzeros", a => SparseValue.Zeros(I(a[0], "spzeros"), a.Length > 1 ? I(a[1], "spzeros") : I(a[0], "spzeros")));
        Reg1("spalloc", a => SparseValue.Zeros(I(a[0], "spalloc"), a.Length > 1 ? I(a[1], "spalloc") : I(a[0], "spalloc")));
        Reg("spfun", (it, a, n) =>
        {
            if (a.Length != 2) Usage("spfun");
            if (!(a[1] is SparseValue || a[1] is MatrixValue { IsChar: false })) throw new OctaveException("spfun: S must be numeric");
            var s = SpArg(a[1], "spfun");
            var vals = new MatrixValue(s.Nnz, 1, s.Re[..s.Nnz], s.Im?[..s.Nnz]);
            var fv = (a[0] is FunctionHandleValue fh ? it.CallHandle(fh, new Value[] { vals }, 1) : it.CallFunction(S(a[0], "spfun"), new Value[] { vals }, 1))[0];
            var r = M(fv is SparseValue fs ? fs.ToDense() : fv, "spfun");
            if (r.Numel != s.Nnz) throw new OctaveException("spfun: function must return a value per element");
            return new Value[] { new SparseValue(s.R, s.C, (int[])s.Cp.Clone(), s.Ri[..s.Nnz], (double[])r.Re.Clone(), (double[]?)r.Im?.Clone()).Prune() };
        });
        Value Rand(Interpreter it, Value[] a, string fn, Func<Random, double> gen, bool sym)
        {
            int r, c; double dens;
            if (a.Length == 0 || (!sym && a.Length == 2) || a.Length > 4) Usage(fn);
            int NonNeg(Value v, string what)
            {
                if (!(v is MatrixValue mv && mv.Numel == 1 && mv.Re[0] >= 0 && mv.Re[0] == Math.Floor(mv.Re[0])))
                    throw new OctaveException($"{fn}: {what} must be a non-negative integer");
                return (int)((MatrixValue)v).Re[0];
            }
            if (a.Length == 1) { var p = SpArg(a[0], fn); var I2 = new List<int>(); var J2 = new List<int>(); var V2 = new List<double>();
                for (int j = 0; j < p.C; j++) for (int q = p.Cp[j]; q < p.Cp[j + 1]; q++) { I2.Add(p.Ri[q]); J2.Add(j); V2.Add(gen(it.Rng)); }
                return SparseValue.FromTriplets(p.R, p.C, I2, J2, V2); }
            if (sym) { r = c = NonNeg(a[0], "N"); dens = D(a[1], fn); }
            else { r = NonNeg(a[0], "M"); c = NonNeg(a[1], "N"); dens = D(a[2], fn); }
            if (dens < 0 || dens > 1) throw new OctaveException($"{fn}: density D must be between 0 and 1");
            if (!sym && a.Length == 4) return RandCond(it, r, c, dens, M(a[3], fn).Re, fn);
            long total = (long)r * c;
            int k = (int)Math.Round(dens * total);
            var picked = new HashSet<long>();
            if (k > total / 2) { for (long t = 0; t < total; t++) picked.Add(t); while (picked.Count > k) picked.Remove((long)(it.Rng.NextDouble() * total)); }
            else while (picked.Count < k) picked.Add((long)(it.Rng.NextDouble() * total));
            var II = new List<int>(); var JJ = new List<int>(); var VV = new List<double>();
            foreach (var t in picked)
            {
                int i = (int)(t % r), j = (int)(t / r); double v = gen(it.Rng);
                if (sym) { if (i < j) continue; II.Add(i); JJ.Add(j); VV.Add(v); if (i != j) { II.Add(j); JJ.Add(i); VV.Add(v); } }
                else { II.Add(i); JJ.Add(j); VV.Add(v); }
            }
            return SparseValue.FromTriplets(r, c, II, JJ, VV, null, false, false);
        }
        Value RandCond(Interpreter it, int r, int c, double dens, double[] rc, string fn)
        {
            int k = Math.Min(r, c);
            var sv = new double[k];
            if (rc.Length == 1)
            {
                if (rc[0] < 0 || rc[0] > 1) throw new OctaveException($"{fn}: RC must be between 0 and 1");
                for (int i = 0; i < k; i++) sv[i] = k == 1 ? 1 : Math.Pow(rc[0], (double)i / (k - 1));
            }
            else
            {
                if (rc.Length > k) throw new OctaveException($"{fn}: RC must be a scalar or vector of length min (M, N)");
                for (int i = 0; i < rc.Length; i++) sv[i] = rc[i];
            }
            var A = new double[r, c];
            for (int i = 0; i < k; i++) A[i, i] = sv[i];
            long target = (long)Math.Round(dens * r * c);
            int Nnz() { int q = 0; foreach (var x in A) if (x != 0) q++; return q; }
            int guard = 0;
            while (Nnz() < target && guard++ < 10 * (r + c) + 100)
            {
                double th = 2 * Math.PI * it.Rng.NextDouble(), cs = Math.Cos(th), sn = Math.Sin(th);
                if (it.Rng.NextDouble() < 0.5 && r > 1)
                {
                    int i = it.Rng.Next(r), j = it.Rng.Next(r); if (i == j) continue;
                    for (int q = 0; q < c; q++) { double x = A[i, q], y = A[j, q]; A[i, q] = cs * x - sn * y; A[j, q] = sn * x + cs * y; }
                }
                else if (c > 1)
                {
                    int i = it.Rng.Next(c), j = it.Rng.Next(c); if (i == j) continue;
                    for (int q = 0; q < r; q++) { double x = A[q, i], y = A[q, j]; A[q, i] = cs * x - sn * y; A[q, j] = sn * x + cs * y; }
                }
                else break;
            }
            var II = new List<int>(); var JJ = new List<int>(); var VV = new List<double>();
            for (int j = 0; j < c; j++) for (int i = 0; i < r; i++) if (A[i, j] != 0) { II.Add(i); JJ.Add(j); VV.Add(A[i, j]); }
            return SparseValue.FromTriplets(r, c, II, JJ, VV);
        }
        static double Gauss(Random rng) { double u = 1 - rng.NextDouble(), v = rng.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }
        Reg("sprand", (it, a, n) => new[] { Rand(it, a, "sprand", r => { double x; do x = r.NextDouble(); while (x == 0); return x; }, false) });
        Reg("sprandn", (it, a, n) => new[] { Rand(it, a, "sprandn", Gauss, false) });
        Reg("sprandsym", (it, a, n) => new[] { Rand(it, a, "sprandsym", Gauss, true) });
        Reg("spdiags", (it, a, n) =>
        {
            if (a.Length == 4)
            {
                var B = M(Dn(a[0]), "spdiags"); var d = M(Dn(a[1]), "spdiags"); int r = I(a[2], "spdiags"), c = I(a[3], "spdiags");
                var II = new List<int>(); var JJ = new List<int>(); var VV = new List<double>();
                for (int k = 0; k < d.Numel; k++)
                {
                    int dk = (int)d.Re[k];
                    for (int j = Math.Max(0, dk); j < Math.Min(c, r + dk); j++)
                    {
                        int i = j - dk;
                        // Octave: for m >= n, B(j, k) is used; otherwise B(i, k)
                        int bi = r >= c ? j : i;
                        double v = B.Numel == 1 ? B.Re[0] : (bi < B.R ? B.Re[bi + k * B.R] : 0);
                        II.Add(i); JJ.Add(j); VV.Add(v);
                    }
                }
                return new Value[] { SparseValue.FromTriplets(r, c, II, JJ, VV) };
            }
            // extraction: [B, d] = spdiags(A) / B = spdiags(A, d)
            var A = SpArg(a[0], "spdiags");
            int[] ds;
            if (a.Length >= 2) ds = M(Dn(a[1]), "spdiags").Re.Select(x => (int)x).ToArray();
            else { var set = new SortedSet<int>(); for (int j = 0; j < A.C; j++) for (int p = A.Cp[j]; p < A.Cp[j + 1]; p++) set.Add(j - A.Ri[p]); ds = set.ToArray(); }
            int len = Math.Min(A.R, A.C);
            var Bm = new MatrixValue(len, ds.Length);
            for (int k = 0; k < ds.Length; k++)
                for (int j = Math.Max(0, ds[k]); j < Math.Min(A.C, A.R + ds[k]); j++)
                {
                    int i = j - ds[k]; int bi = A.R >= A.C ? j : i;
                    if (bi < len) Bm.Re[bi + k * len] = A.Get(i, j).Real;
                }
            var dv = MatrixValue.Col(ds.Select(x => (double)x));
            return n >= 2 ? new Value[] { Bm, dv } : new Value[] { Bm };
        });
        Reg0("spy", (it, a) =>
        {
            var s = SpArg(a[0], "spy");
            var xs = new List<double>(); var ys = new List<double>();
            for (int j = 0; j < s.C; j++) for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++) { xs.Add(j + 1); ys.Add(s.Ri[p] + 1); }
            it.CallFunction("plot", new Value[] { MatrixValue.Row(xs), MatrixValue.Row(ys), MatrixValue.Str(".") }, 0);
            it.CallFunction("axis", new Value[] { MatrixValue.Str("ij") }, 0);
        });
        Reg("eigs", (it, a, n) => EigsSvds(it, a, n, false));
        Reg("svds", (it, a, n) => EigsSvds(it, a, n, true));
    }

    static Value Dn(Value v) => v is SparseValue s ? s.ToDense() : v;

    static Value[] EigsSvds(Interpreter it, Value[] a, int nargout, bool svd)
    {
        string fn = svd ? "svds" : "eigs";
        if (a.Length == 0) Usage(fn);
        if (!svd)
        {
            if (a.Length > 1 && !(a[1] is MatrixValue { IsChar: false } || a[1] is SparseValue)) throw new OctaveException("eigs: second argument must be numeric");
            int nA = a[0] is SparseValue s0 ? s0.R : (a[0] as MatrixValue)?.R ?? 0;
            int kk = 6; int ki = 1;
            if (a.Length > 1 && a[1] is MatrixValue m1 && m1.Numel == 1 && !(a[0] is MatrixValue m0 && m0.Numel == 1 && false)) kk = (int)m1.Re[0];
            if (a.Length > 2 && a[1] is MatrixValue { Numel: 0 } && a[2] is MatrixValue m2 && !m2.IsChar && m2.Numel == 1) kk = (int)m2.Re[0];
            _ = ki;
            if (kk > nA) throw new OctaveException($"eigs: requested number of eigenvalues K ({kk}) exceeds available eigenvalues ({nA})");
            if (a.Length > 2 && a[2] is MatrixValue sg && sg.IsChar)
            {
                string md = sg.AsString().ToLowerInvariant();
                var Ad = a[0] is SparseValue s1 ? s1.ToDense() : M(a[0], fn);
                bool rsym = !Ad.IsComplex && Ad.R == Ad.C;
                if (rsym) for (int i = 0; i < Ad.R && rsym; i++) for (int j = 0; j < i; j++) if (Ad.Re[i + j * Ad.R] != Ad.Re[j + i * Ad.R]) { rsym = false; break; }
                if (md is "la" or "sa" or "be" && !rsym) throw new OctaveException($"eigs: \"{md}\" requires real symmetric problem");
                if (md is "lr" or "sr" or "li" or "si" && rsym) throw new OctaveException($"eigs: \"{md}\" requires complex or unsymmetric problem");
                if (md is not ("lm" or "sm" or "la" or "sa" or "be" or "lr" or "sr" or "li" or "si")) throw new OctaveException($"eigs: unrecognized value for SIGMA: {sg.AsString()}");
            }
        }
        var A = a[0] is SparseValue s ? s.ToDense() : M(a[0], fn);
        if (A.R * (long)A.C > 9_000_000) throw new OctaveException($"{fn}: matrix too large for the dense fallback (max 3000x3000)");
        int k = a.Length > 1 ? I(a[1], fn) : 6;
        Value sigma = a.Length > 2 ? a[2] : (svd ? Num(double.PositiveInfinity) : MatrixValue.Str("lm"));
        int nmax = svd ? Math.Min(A.R, A.C) : A.R;
        k = Math.Min(k, nmax);
        if (svd)
        {
            var outs = it.CallFunction("svd", new Value[] { A, Num(0) }, 3);
            var S = (MatrixValue)outs[1]; var U = (MatrixValue)outs[0]; var V = (MatrixValue)outs[2];
            int m = Math.Min(S.R, S.C);
            var sv = Enumerable.Range(0, m).Select(i => S.Re[i + i * S.R]).ToArray();
            int[] order;
            if (sigma is MatrixValue sm && sm.IsChar ? sm.AsString().ToUpperInvariant() is "L" or "LM" : double.IsPositiveInfinity(D(sigma, fn)))
                order = Enumerable.Range(0, m).OrderByDescending(i => sv[i]).Take(k).ToArray();
            else
            {
                double t = sigma is MatrixValue sm2 && sm2.IsChar ? 0 : D(sigma, fn);
                order = Enumerable.Range(0, m).OrderBy(i => Math.Abs(sv[i] - t)).Take(k).OrderByDescending(i => sv[i]).ToArray();
            }
            if (nargout <= 1) return new Value[] { MatrixValue.Col(order.Select(i => sv[i])) };
            var Uk = new MatrixValue(U.R, k); var Vk = new MatrixValue(V.R, k); var Sk = new MatrixValue(k, k);
            for (int c = 0; c < k; c++)
            {
                Array.Copy(U.Re, order[c] * U.R, Uk.Re, c * U.R, U.R);
                Array.Copy(V.Re, order[c] * V.R, Vk.Re, c * V.R, V.R);
                Sk.Re[c + c * k] = sv[order[c]];
            }
            return new Value[] { Uk, Sk, Vk };
        }
        else
        {
            Value B = a.Length > 3 && a[3] is not StructValue ? Dn(a[3]) : null!;
            var outs = B != null ? it.CallFunction("eig", new Value[] { A, B }, 2) : it.CallFunction("eig", new Value[] { A }, 2);
            var Vv = (MatrixValue)outs[0]; var Dd = (MatrixValue)outs[1];
            int m = Dd.R;
            var ev = Enumerable.Range(0, m).Select(i => new Complex(Dd.Re[i + i * m], Dd.Im?[i + i * m] ?? 0)).ToArray();
            IEnumerable<int> idx = Enumerable.Range(0, m);
            string mode = sigma is MatrixValue sm && sm.IsChar ? sm.AsString().ToLowerInvariant() : "num";
            double target = mode == "num" ? D(sigma, fn) : 0;
            int[] order = mode switch
            {
                "lm" => idx.OrderByDescending(i => ev[i].Magnitude).Take(k).ToArray(),
                "sm" => idx.OrderBy(i => ev[i].Magnitude).Take(k).ToArray(),
                "la" or "lr" => idx.OrderByDescending(i => ev[i].Real).Take(k).ToArray(),
                "sa" or "sr" => idx.OrderBy(i => ev[i].Real).Take(k).ToArray(),
                "li" => idx.OrderByDescending(i => ev[i].Imaginary).Take(k).ToArray(),
                "si" => idx.OrderBy(i => ev[i].Imaginary).Take(k).ToArray(),
                "be" => BothEnds(idx.OrderBy(i => ev[i].Real).ToArray(), k),
                "num" => idx.OrderBy(i => (ev[i] - target).Magnitude).Take(k).ToArray(),
                _ => throw new OctaveException($"eigs: unrecognized sigma value '{mode}'")
            };
            bool cplx = order.Any(i => ev[i].Imaginary != 0);
            if (nargout <= 1)
            {
                var d = new MatrixValue(k, 1, NumKind.Double, cplx);
                for (int c = 0; c < k; c++) { d.Re[c] = ev[order[c]].Real; if (cplx) d.Im![c] = ev[order[c]].Imaginary; }
                return new Value[] { d };
            }
            var Vk = new MatrixValue(Vv.R, k, NumKind.Double, Vv.Im != null);
            var Dk = new MatrixValue(k, k, NumKind.Double, cplx);
            for (int c = 0; c < k; c++)
            {
                Array.Copy(Vv.Re, order[c] * Vv.R, Vk.Re, c * Vv.R, Vv.R);
                if (Vv.Im != null) Array.Copy(Vv.Im, order[c] * Vv.R, Vk.Im!, c * Vv.R, Vv.R);
                Dk.Re[c + c * k] = ev[order[c]].Real; if (cplx) Dk.Im![c + c * k] = ev[order[c]].Imaginary;
            }
            return nargout >= 3 ? new Value[] { Vk, Dk, Num(0) } : new Value[] { Vk, Dk };
        }
    }

    static int[] BothEnds(int[] sorted, int k)
    {
        var r = new List<int>(); int lo = 0, hi = sorted.Length - 1; bool top = true;
        while (r.Count < k && lo <= hi) { r.Add(top ? sorted[hi--] : sorted[lo++]); top = !top; }
        return r.ToArray();
    }
}
