using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Exact int64/uint64 overlays for elementwise builtins (0.23).</summary>
public static partial class BuiltinLibrary
{
    static bool Any64(Value[] a) => a.Any(v => v is MatrixValue m && m.Is64 && !m.IsComplex && m.ND == null);
    static BigInteger Ex(MatrixValue m, int k) => m.Kind == NumKind.UInt64 ? m.U64(k) : m.Kind == NumKind.Int64 ? m.I64(k) : new BigInteger(m.Re[k]);
    static bool Integral(MatrixValue m) => m.Is64 || (!m.IsComplex && m.Re.All(d => !double.IsNaN(d) && !double.IsInfinity(d) && d == Math.Floor(d)));
    static MatrixValue Mk(int r, int c, IEnumerable<BigInteger> v, NumKind k) => MatrixValue.FromI64(r, c, v.Select(x => Int64Ops.Sat(x, k)).ToArray(), k);
    static MatrixValue Same(MatrixValue m) => MatrixValue.FromI64(m.R, m.C, Enumerable.Range(0, m.Numel).Select(i => m.Kind == NumKind.UInt64 ? (long)m.U64(i) : m.I64(i)).ToArray(), m.Kind);

    static void Wrap64(string name, Func<Interpreter, Value[], int, Value[]?> f)
    {
        if (!cur!.Builtins.TryGetValue(name, out var orig)) return;
        cur!.Builtins[name] = (it, a, n) => { if (Any64(a)) { var r = f(it, a, n); if (r != null) return r; } return orig(it, a, n); };
    }

    static bool IsPrime64(BigInteger n)
    {
        if (n < 2) return false;
        foreach (int p in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }) { if (n == p) return true; if (n % p == 0) return false; }
        var d = n - 1; int s = 0; while (d.IsEven) { d >>= 1; s++; }
        foreach (int b in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            var x = BigInteger.ModPow(b, d, n);
            if (x.IsOne || x == n - 1) continue;
            bool comp = true;
            for (int r = 1; r < s; r++) { x = x * x % n; if (x == n - 1) { comp = false; break; } }
            if (comp) return false;
        }
        return true;
    }
    static BigInteger PollardRho(BigInteger n)
    {
        if (n.IsEven) return 2;
        for (BigInteger c = 1; c < 100; c++)
        {
            BigInteger x = 2, y = 2, d = 1;
            while (d.IsOne) { x = (x * x + c) % n; y = (y * y + c) % n; y = (y * y + c) % n; d = BigInteger.GreatestCommonDivisor(BigInteger.Abs(x - y), n); }
            if (d != n) return d;
        }
        return n;
    }
    static void RegisterInt64()
    {
        foreach (var nm in new[] { "floor", "ceil", "round", "fix", "real", "conj" })
            Wrap64(nm, (it, a, n) => a.Length == 1 && a[0] is MatrixValue m && m.Is64 ? new Value[] { Same(m) } : null);
        Wrap64("abs", (it, a, n) => a.Length == 1 && a[0] is MatrixValue m && m.Is64 ? new Value[] { Mk(m.R, m.C, Enumerable.Range(0, m.Numel).Select(i => BigInteger.Abs(Ex(m, i))), m.Kind) } : null);
        foreach (var nm in new[] { "rem", "mod" })
        {
            bool isMod = nm == "mod";
            Wrap64(nm, (it, a, n) =>
            {
                if (a.Length != 2 || a[0] is not MatrixValue x || a[1] is not MatrixValue y || !Integral(x) || !Integral(y)) return null;
                if (!Int64Ops.Bcast(x, y, out int r, out int c)) return null;
                var k = x.Is64 ? x.Kind : y.Kind;
                var res = new BigInteger[r * c];
                for (int i = 0; i < res.Length; i++)
                {
                    var X = Ex(x, x.IsScalar ? 0 : i); var Y = Ex(y, y.IsScalar ? 0 : i);
                    if (Y.IsZero) { res[i] = X; continue; }
                    var q = BigInteger.Remainder(X, Y);
                    if (isMod && !q.IsZero && q.Sign != Y.Sign) q += Y;
                    res[i] = q;
                }
                return new Value[] { Mk(r, c, res, k) };
            });
        }
        foreach (var nm in new[] { "max", "min" })
        {
            int want = nm == "max" ? 1 : -1;
            Wrap64(nm, (it, a, n) =>
            {
                if (a.Length == 1 && a[0] is MatrixValue x && x.Is64 && (x.R == 1 || x.C == 1) && x.Numel > 0 && n <= 2)
                {
                    int bi = 0; var best = Ex(x, 0);
                    for (int i = 1; i < x.Numel; i++) { var v = Ex(x, i); if (v.CompareTo(best) == want) { best = v; bi = i; } }
                    return new Value[] { Mk(1, 1, new[] { best }, x.Kind), MatrixValue.Scalar(bi + 1) };
                }
                if (a.Length == 2 && n <= 1 && a[0] is MatrixValue p && a[1] is MatrixValue q && Integral(p) && Integral(q) && Int64Ops.Bcast(p, q, out int r, out int c))
                {
                    var k = p.Is64 ? p.Kind : q.Kind;
                    return new Value[] { Mk(r, c, Enumerable.Range(0, r * c).Select(i => { var u = Ex(p, p.IsScalar ? 0 : i); var v = Ex(q, q.IsScalar ? 0 : i); return v.CompareTo(u) == want ? v : u; }), k) };
                }
                return null;
            });
        }
        Wrap64("sum", (it, a, n) =>
        {
            if (a.Length == 0 || a[0] is not MatrixValue x || !x.Is64) return null;
            string type = "native"; int dim = 0; int nd = 0;
            for (int i = 1; i < a.Length; i++)
            {
                if (IsString(a[i])) { type = S(a[i], "sum").ToLowerInvariant(); if (type == "default") type = "native"; }
                else if (a[i] is MatrixValue dm && dm.IsScalar) { dim = (int)dm.Re[0]; nd++; }
                else return null;
            }
            if (type is not ("native" or "double") || nd > 1) return null;
            if (dim == 0) dim = x.R == 1 ? 2 : 1;
            if (dim > 2) return new Value[] { type == "double" ? x.AsDouble() : Same(x) };
            int R = dim == 1 ? 1 : x.R, C = dim == 1 ? x.C : 1;
            var res = new BigInteger[R * C];
            for (int i = 0; i < x.R; i++) for (int j = 0; j < x.C; j++) res[dim == 1 ? j : i] += Ex(x, i + j * x.R);
            if (type == "double") { var d = new MatrixValue(R, C); for (int i = 0; i < res.Length; i++) d.Re[i] = (double)res[i]; return new Value[] { d }; }
            return new Value[] { Mk(R, C, res, x.Kind) };
        });
        foreach (var nm in new[] { "bitand", "bitor", "bitxor" })
        {
            string op = nm;
            Wrap64(nm, (it, a, n) =>
            {
                if (a.Length != 2 || a[0] is not MatrixValue x || a[1] is not MatrixValue y || !Integral(x) || !Integral(y)) return null;
                if (!Int64Ops.Bcast(x, y, out int r, out int c)) return null;
                if (x.Is64 && y.Is64 && x.Kind != y.Kind) return null;
                var k = x.Is64 ? x.Kind : y.Kind;
                return new Value[] { Mk(r, c, Enumerable.Range(0, r * c).Select(i =>
                {
                    var u = (ulong)(Ex(x, x.IsScalar ? 0 : i) & ulong.MaxValue); var v = (ulong)(Ex(y, y.IsScalar ? 0 : i) & ulong.MaxValue);
                    ulong w = op == "bitand" ? u & v : op == "bitor" ? u | v : u ^ v;
                    return k == NumKind.UInt64 ? new BigInteger(w) : new BigInteger((long)w);
                }), k) };
            });
        }
        Wrap64("bitshift", (it, a, n) =>
        {
            if (a.Length is < 2 or > 3 || a[0] is not MatrixValue x || !x.Is64 || a[1] is not MatrixValue s || !Integral(s)) return null;
            if (!Int64Ops.Bcast(x, s, out int r, out int c)) return null;
            int bits = a.Length == 3 ? (int)D(a[2], "bitshift") : 64;
            ulong mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
            return new Value[] { Mk(r, c, Enumerable.Range(0, r * c).Select(i =>
            {
                var X = Ex(x, x.IsScalar ? 0 : i); int sh = (int)s.Re[s.IsScalar ? 0 : i];
                if (x.Kind == NumKind.Int64) { if (X.Sign < 0) return X >> -sh; }
                ulong u = (ulong)X; ulong w = sh >= 64 || sh <= -64 ? 0 : sh >= 0 ? u << sh : u >> -sh; w &= mask;
                if (x.Kind == NumKind.Int64) w &= long.MaxValue;
                return new BigInteger(w);
            }), x.Kind) };
        });
        Wrap64("bitcmp", (it, a, n) =>
        {
            if (a.Length is < 1 or > 2 || a[0] is not MatrixValue x || !x.Is64) return null;
            int bits = a.Length == 2 ? (int)D(a[1], "bitcmp") : 64;
            if (x.Kind == NumKind.Int64) return new Value[] { Mk(x.R, x.C, Enumerable.Range(0, x.Numel).Select(i => -Ex(x, i) - 1), x.Kind) };
            ulong mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
            return new Value[] { Mk(x.R, x.C, Enumerable.Range(0, x.Numel).Select(i => new BigInteger((x.U64(i) ^ mask) & mask)), x.Kind) };
        });
        foreach (var nm in new[] { "num2str", "int2str" })
            Wrap64(nm, (it, a, n) => a.Length == 1 && a[0] is MatrixValue m && m.Is64 && m.IsScalar ? new Value[] { Str(m.ExactString(0)) } : null);
        Wrap64("sort", (it, a, n) =>
        {
            if (a.Length == 0 || a[0] is not MatrixValue x || !x.Is64 || !(x.R == 1 || x.C == 1)) return null;
            bool desc = false;
            for (int i = 1; i < a.Length; i++)
            {
                if (IsString(a[i])) { var md = S(a[i], "sort").ToLowerInvariant(); if (md == "descend") desc = true; else if (md != "ascend") return null; }
                else if (a[i] is MatrixValue dm && dm.IsScalar) { if ((dm.Re[0] == 1 && x.R != 1 && false) || (dm.Re[0] == 1 && x.R == 1 && x.C > 1) || (dm.Re[0] == 2 && x.C == 1 && x.R > 1)) return null; }
                else return null;
            }
            var idx = Enumerable.Range(0, x.Numel).ToArray();
            var vals = idx.Select(i => Ex(x, i)).ToArray();
            var ord = (desc ? idx.OrderByDescending(i => vals[i]) : idx.OrderBy(i => vals[i])).ToArray();
            var res = Mk(x.R, x.C, ord.Select(i => vals[i]), x.Kind);
            var ix = new MatrixValue(x.R, x.C); for (int i = 0; i < ord.Length; i++) ix.Re[i] = ord[i] + 1;
            return new Value[] { res, ix };
        });
        Wrap64("nth_element", (it, a, n) =>
        {
            if (a.Length != 2 || a[0] is not MatrixValue x || !x.Is64 || !(x.R == 1 || x.C == 1) || a[1] is not MatrixValue km) return null;
            var vals = Enumerable.Range(0, x.Numel).Select(i => Ex(x, i)).OrderBy(v => v).ToArray();
            if (km.Re.Any(k => k < 1 || k > vals.Length || k != Math.Floor(k))) return null;
            var sel = km.Re.Select(k => vals[(int)k - 1]).ToArray();
            return new Value[] { x.R == 1 ? Mk(1, sel.Length, sel, x.Kind) : Mk(sel.Length, 1, sel, x.Kind) };
        });
        Wrap64("isprime", (it, a, n) =>
        {
            if (a.Length != 1 || a[0] is not MatrixValue x || !x.Is64) return null;
            var r = new MatrixValue(x.R, x.C, NumKind.Logical);
            for (int i = 0; i < x.Numel; i++) r.Re[i] = IsPrime64(Ex(x, i)) ? 1 : 0;
            return new Value[] { r };
        });
        cur!.Builtins["__pollardrho__"] = (it, a, n) =>
        {
            if (a.Length != 1 || a[0] is not MatrixValue x || x.Numel != 1) throw new OctaveException("Invalid call to __pollardrho__.");
            var N = Ex(x, 0);
            var f = PollardRho(N);
            return new Value[] { x.Is64 ? Mk(1, 1, new[] { f }, x.Kind) : MatrixValue.Scalar((double)f) };
        };
        if (cur!.Builtins.TryGetValue("isequal", out var iseq0))
            cur!.Builtins["isequal"] = (it, a, n) =>
            {
                if (a.Length >= 2 && a.All(v => v is Sparse.SparseValue))
                {
                    var sp = a.Cast<Sparse.SparseValue>().ToArray();
                    static List<(int, int, double, double)> Nz(Sparse.SparseValue s)
                    {
                        var l = new List<(int, int, double, double)>();
                        for (int j = 0; j < s.C; j++) for (int p = s.Cp[j]; p < s.Cp[j + 1]; p++) { double im = s.Im?[p] ?? 0; if (s.Re[p] != 0 || im != 0) l.Add((s.Ri[p], j, s.Re[p], im)); }
                        return l;
                    }
                    var f = Nz(sp[0]);
                    foreach (var g in sp.Skip(1)) { if (g.R != sp[0].R || g.C != sp[0].C || !Nz(g).SequenceEqual(f)) return new Value[] { MatrixValue.Bool(false) }; }
                    return new Value[] { MatrixValue.Bool(true) };
                }
                return iseq0(it, a.Select(v => v is Sparse.SparseValue s ? s.ToDense() : v).ToArray(), n);
            };
        if (cur!.Builtins.TryGetValue("iscomplex", out var isc0))
            cur!.Builtins["iscomplex"] = (it, a, n) => a.Length == 1 && a[0] is Sparse.SparseValue sv ? new Value[] { MatrixValue.Bool(sv.IsComplex) } : isc0(it, a, n);
        Wrap64("isequal", (it, a, n) =>
        {
            if (a.Length < 2 || a.Any(v => v is not MatrixValue mm || mm.IsComplex || mm.ND != null)) return null;
            var ms = a.Cast<MatrixValue>().ToArray();
            if (!ms.All(Integral)) return null;
            var f = ms[0];
            foreach (var g in ms.Skip(1))
            {
                if (g.R != f.R || g.C != f.C) return new Value[] { MatrixValue.Bool(false) };
                for (int i = 0; i < f.Numel; i++) if (Ex(f, i) != Ex(g, i)) return new Value[] { MatrixValue.Bool(false) };
            }
            return new Value[] { MatrixValue.Bool(true) };
        });
    }
}
