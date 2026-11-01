// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;
namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase11()
    {
        RegisterPhase11Linalg(); RegisterPhase11Geometry();
        // eye (..., CLASS)
        var oldEye = cur!.Builtins["eye"];
        Reg("eye", (it, a, n) =>
        {
            if (a.Length > 0 && a[^1] is MatrixValue cm && cm.IsChar)
            {
                var cls = cm.AsString();
                if (cls == "like") throw new OctaveException("eye: \"like\" option is not supported");
                var r = oldEye(it, a[..^1], 1)[0];
                return new[] { it.CallFunction(cls, new[] { r }, 1)[0] };
            }
            return oldEye(it, a, n);
        });
        // single-precision input gives single-precision factors
        foreach (var fn in new[] { "svd", "qr", "lu", "chol", "eig", "schur", "hess", "inv", "pinv", "expm" })
        {
            if (!cur.Builtins.TryGetValue(fn, out var orig)) continue;
            cur.Builtins[fn] = (it, a, n) =>
            {
                var r = orig(it, a, n);
                if (a.Length > 0 && a[0] is MatrixValue m0 && m0.Kind == NumKind.Single)
                    for (int i = 0; i < r.Length; i++)
                        if (r[i] is MatrixValue rm && rm.Kind == NumKind.Double) r[i] = it.CallFunction("single", new[] { r[i] }, 1)[0];
                return r;
            };
        }
        // vecdim / "all" support for reductions: rewrite to a dim=1 reduction over a reshaped array
        var specs = new (string name, int pos)[] {
            ("sum",1),("prod",1),("sumsq",1),("any",1),("all",1),("mean",1),("median",1),("mode",1),
            ("max",2),("min",2),("std",2),("var",2),("range",1),("nnz",9),("meansq",1) };
        foreach (var (name, pos) in specs)
        {
            if (!cur!.Builtins.TryGetValue(name, out var orig)) continue;
            int p = pos;
            cur.Builtins[name] = (it, a, n) =>
            {
                if (a.Length <= p || a[0] is not MatrixValue x0) return orig(it, a, n);
                var d = a[p] as MatrixValue; if (d == null) return orig(it, a, n);
                Value x = a[0];
                var sz = ((MatrixValue)it.CallFunction("size", new[] { x }, 1)[0]).Re.Select(v => (int)v).ToList();
                Value[] na = (Value[])a.Clone();
                int[] outSz;
                if (d.IsChar)
                {
                    if (!string.Equals(d.AsString(), "all", StringComparison.OrdinalIgnoreCase)) return orig(it, a, n);
                    na[0] = it.CallFunction("reshape", new Value[] { x, Num(x0.Numel), Num(1) }, 1)[0];
                    na[p] = Num(1);
                    var r0 = orig(it, na, n);
                    return r0;
                }
                if (d.Numel <= 1) return orig(it, a, n);
                if (d.Re.Any(v => v < 1 || v != Math.Floor(v))) throw new OctaveException($"{name}: VECDIM must contain positive integers");
                var dims = d.Re.Select(v => (int)v).ToList();
                if (dims.Distinct().Count() != dims.Count) throw new OctaveException($"{name}: VECDIM must contain non-repeating positive integers");
                int nd = Math.Max(sz.Count, dims.Max());
                while (sz.Count < nd) sz.Add(1);
                var dset = dims.Select(v => v - 1).OrderBy(v => v).ToList();
                var rest = Enumerable.Range(0, nd).Where(k => !dset.Contains(k)).ToList();
                var perm = dset.Concat(rest).Select(k => (double)(k + 1)).ToArray();
                long red = 1; foreach (var k in dset) red *= sz[k];
                var shp = new List<double> { red }; foreach (var k in rest) shp.Add(sz[k]); if (shp.Count < 2) shp.Add(1);
                var px = it.CallFunction("permute", new Value[] { x, new MatrixValue(1, perm.Length, perm) }, 1)[0];
                na[0] = it.CallFunction("reshape", new Value[] { px, new MatrixValue(1, shp.Count, shp.ToArray()) }, 1)[0];
                na[p] = Num(1);
                var r = orig(it, na, n);
                outSz = sz.ToArray(); foreach (var k in dset) outSz[k] = 1;
                var os = new MatrixValue(1, outSz.Length, outSz.Select(v => (double)v).ToArray());
                for (int i = 0; i < r.Length; i++)
                    if (r[i] is MatrixValue rm && rm.Numel == outSz.Aggregate(1L, (s, v) => s * v))
                        r[i] = it.CallFunction("reshape", new Value[] { r[i], os }, 1)[0];
                return r;
            };
        }
    }
}

public static partial class BuiltinLibrary
{
    static Value RandState(Interpreter it, Value[] a, bool normal, string fn)
    {
        string key = S(a[0], fn).ToLowerInvariant();
        if (key is "single" or "double") return null!;
        if (key is not ("state" or "seed" or "twister"))
            throw new OctaveException($"{fn}: unrecognized string argument");
        var g = (normal ? it.RngN : it.Rng) as MtRandom;
        if (g == null) { g = new MtRandom(); if (normal) it.RngN = g; else it.Rng = g; }
        if (a.Length == 1) return MatrixValue.Col(g.GetState());
        if (a[1] is MatrixValue sm && sm.IsChar)
        {
            if (sm.AsString().ToLowerInvariant() == "reset") { g.Seed(5489u); return new MatrixValue(0, 0); }
            throw new OctaveException($"{fn}: unrecognized string argument");
        }
        var v = M(a[1], fn);
        if (v.Numel == 1) g.SeedArray(new[] { (uint)(long)Math.Abs(v.Re[0]) });
        else g.SetState(v.Re.ToArray());
        return new MatrixValue(0, 0);
    }
}
