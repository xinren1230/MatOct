using MatRewrite.Core.Runtime;
using MatRewrite.Core.Graphics;

namespace MatRewrite.Core.Builtins;

/// <summary>0.21 compatibility fixes.</summary>
public static partial class BuiltinLibrary
{
    static void RegisterPhase18()
    {
        if (cur!.Builtins.TryGetValue("gammaln", out var gl0))
        {
            BuiltinFn gl = (it, a, n) =>
            {
                var r = gl0(it, a, n);
                if (a.Length == 1 && a[0] is MatrixValue m && !m.IsComplex && !m.IsChar && r.Length > 0 && r[0] is MatrixValue rm && m.Re.Any(x => x < 0 && x != Math.Floor(x)))
                {
                    rm = rm.Copy(); rm.Im = new double[rm.Re.Length];
                    for (int k = 0; k < m.Re.Length && k < rm.Re.Length; k++)
                    {
                        double x = m.Re[k];
                        if (x < 0 && x != Math.Floor(x))
                        {
                            rm.Re[k] = Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - MathNet.Numerics.SpecialFunctions.GammaLn(1 - x);
                            if (((long)Math.Floor(x)) % 2 != 0) rm.Im[k] = Math.PI;
                        }
                    }
                    r = new Value[] { rm };
                }
                return r;
            };
            cur!.Builtins["gammaln"] = gl; cur!.Builtins["lgamma"] = gl;
        }
        if (cur!.Builtins.TryGetValue("cat", out var cat0))
            cur!.Builtins["cat"] = (it, a, n) =>
            {
                if (a.Length >= 3 && a[0] is MatrixValue dm && dm.Numel == 1 && dm.Re[0] >= 1 && dm.Re[0] == Math.Floor(dm.Re[0]))
                {
                    int d = (int)dm.Re[0]; int[]? first = null; int pos = 0;
                    foreach (var v in a.Skip(1))
                    {
                        pos++;
                        var dims = v.Dims; if (dims.Length == 2 && dims[0] == 0 && dims[1] == 0) continue;
                        int L = Math.Max(Math.Max(dims.Length, d), first?.Length ?? 0);
                        var dd = new int[L]; for (int q = 0; q < L; q++) dd[q] = q < dims.Length ? dims[q] : 1;
                        if (first == null) { first = dd; continue; }
                        for (int q = 0; q < L; q++)
                        {
                            int fq = q < first.Length ? first[q] : 1;
                            if (q != d - 1 && fq != dd[q]) throw new OctaveException($"cat: dimension mismatch in dimension {q + 1} at position {pos}");
                        }
                    }
                }
                return cat0(it, a, n);
            };
        foreach (var (inm, imsg) in new[] { ("gray2ind", "gray2ind: I must be a grayscale or binary image"), ("ind2gray", "ind2gray: X must be an indexed image"), ("ind2rgb", "ind2rgb: X must be an indexed image") })
            if (cur!.Builtins.TryGetValue(inm, out var imf))
            {
                var msg = imsg;
                cur!.Builtins[inm] = (it, a, n) => a.Length > 0 && a[0] is MatRewrite.Core.Sparse.SparseValue ? throw new OctaveException(msg) : imf(it, a, n);
            }
        var intKeep = new HashSet<string> { "abs", "sign", "round", "floor", "ceil", "fix", "real", "imag", "conj" };
        foreach (var en in intKeep.Concat(new[] { "sqrt", "exp", "log", "log2", "log10", "log1p", "expm1", "sin", "cos", "tan", "asin", "acos", "atan",
                     "sinh", "cosh", "tanh", "asinh", "acosh", "atanh", "gamma", "lgamma", "gammaln", "erf", "erfc", "erfinv", "erfcinv", "cbrt", "sec", "csc", "cot", "angle", "arg" }))
            if (cur!.Builtins.TryGetValue(en, out var ef))
            {
                var nm = en; bool ik = intKeep.Contains(en);
                cur!.Builtins[en] = (it, a, n) =>
                {
                    var r = ef(it, a, n);
                    if (a.Length == 1 && a[0] is MatrixValue m && !m.IsChar && r.Length > 0 && r[0] is MatrixValue rm && rm.Kind == NumKind.Double)
                    {
                        if (m.Kind == NumKind.Single) { rm = rm.Copy(); rm.Kind = NumKind.Single; r[0] = rm; }
                        else if (ik && m.IsInteger && !rm.IsComplex) r[0] = Fc(it, NumTypes.Name(m.Kind), rm);
                    }
                    return r;
                };
            }
        foreach (var sn in new[] { "mad", "rms", "kendall", "meansq" })
            if (cur!.Builtins.TryGetValue(sn, out var sf))
                cur!.Builtins[sn] = (it, a, n) =>
                {
                    var r = sf(it, a, n);
                    if (a.Length > 0 && a[0] is MatrixValue m && m.Kind == NumKind.Single)
                        for (int i = 0; i < r.Length; i++)
                            if (r[i] is MatrixValue rm && rm.Kind == NumKind.Double) { rm = rm.Copy(); rm.Kind = NumKind.Single; r[i] = rm; }
                    return r;
                };
        if (cur!.Builtins.TryGetValue("legend", out var leg))
            cur!.Builtins["legend"] = (it, a, n) =>
            {
                for (int k = 0; k + 1 < a.Length; k++)
                    if (IsString(a[k]) && S(a[k], "legend").Equals("location", StringComparison.OrdinalIgnoreCase) && IsString(a[k + 1]) && S(a[k + 1], "legend").ToLowerInvariant() is "best" or "bestoutside")
                    {
                        it.Warning("legend: 'best' not yet implemented for location specifier, using 'northeast' instead");
                        a = a.ToArray(); a[k + 1] = Str(S(a[k + 1], "legend").ToLowerInvariant() == "best" ? "northeast" : "northeastoutside");
                    }
                bool hasAx = a.Length > 0 && a[0] is MatrixValue m0 && !m0.IsChar && m0.Numel >= 1;
                if (!hasAx)
                {
                    var ax = it.Graphics.Gca();
                    int nobj = ax.Children.Count(c => TypeOf(c) is not "text" and not "light");
                    var labels = new List<string>(); bool other = false;
                    for (int k = 0; k < a.Length; k++)
                    {
                        if (a[k] is CellValue c) { labels.AddRange(c.Data.Select(x => x is MatrixValue xm && xm.IsChar ? xm.RowString(0) : "")); continue; }
                        if (!IsString(a[k])) { other = true; break; }
                        var s = S(a[k], "legend").ToLowerInvariant();
                        if (s is "show" or "on" or "off" or "hide" or "boxon" or "boxoff" or "toggle") { other = true; break; }
                        if (s is "location" or "orientation" or "fontsize" or "fontname" or "fontweight" or "fontangle" or "textcolor" or "box" or "interpreter" or "numcolumns" or "edgecolor" or "color" or "autoupdate" or "textlocation") { k++; continue; }
                        labels.Add(S(a[k], "legend"));
                    }
                    if (!other)
                    {
                        if (nobj == 0)
                        {
                            if (labels.Count > 0) it.Warning("legend: plot data is empty; setting key labels has no effect");
                            return n > 0 ? new Value[] { MatrixValue.Empty() } : Array.Empty<Value>();
                        }
                        if (labels.Count > nobj) { it.Warning("legend: ignoring extra labels."); }
                        else if (labels.Count == 0 && a.Length == 0 && nobj > 20) it.Warning("legend: labeling only first 20 data objects");
                    }
                }
                return leg(it, a, n);
            };
    }
}
