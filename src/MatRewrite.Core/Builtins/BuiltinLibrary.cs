// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Registers all built-in functions. Split over several partial files by topic.</summary>
public static partial class BuiltinLibrary
{
    static Interpreter? cur;
    static void Reg(string name, BuiltinFn f) => cur!.Builtins[name] = f;
    static void Reg1(string name, Func<Value[], Value> f) => cur!.Builtins[name] = (it, a, n) => new[] { f(a) };
    static void RegI(string name, Func<Interpreter, Value[], Value> f) => cur!.Builtins[name] = (it, a, n) => new[] { f(it, a) };
    static void Reg0(string name, Action<Interpreter, Value[]> f) => cur!.Builtins[name] = (it, a, n) => { f(it, a); return Array.Empty<Value>(); };

    public static void RegisterAll(Interpreter it)
    {
        cur = it;
        RegisterMath();
        RegisterArray();
        RegisterLinalg();
        RegisterStrings();
        RegisterCellStruct();
        RegisterSystem();
        RegisterFiles();
        RegisterGraphics();
        RegisterGraphics3D();
        RegisterDebug();
        RegisterOde();
        RegisterSparse();
        RegisterClasses();
        RegisterCompat();
        RegisterCompat2();
        RegisterCompat3();
        RegisterHG();
        RegisterPhase6();
        RegisterPhase7();
        RegisterPhase13Tables();
        var keep13 = new[] { "isfolder", "isfile", "dir", "erase", "tempname", "dec2bin", "dec2hex", "mean", "unique", "hist", "ishold", "moment" }
            .Where(cur!.Builtins.ContainsKey).ToDictionary(k => k, k => cur!.Builtins[k]);
        RegisterPhase13IO();
        RegisterPhase13Strings();
        foreach (var kv in keep13) cur!.Builtins[kv.Key] = kv.Value;
        RegisterPhase13AppData(); RegisterPhase13Help(); RegisterFigureEdit(); RegisterFft();
        RegisterPaths();
        RegisterValidation();
        cur = null;
    }

    // ------------------------------------------------------------ helpers
    internal static MatrixValue M(Value v, string fn)
    {
        if (v is MatrixValue m) return m;
        throw new OctaveException($"{fn}: wrong type argument '{(v is CellValue ? "cell array" : v.ClassName)}'");
    }
    internal static double D(Value v, string fn)
    {
        var m = M(v, fn);
        if (m.Numel < 1) throw new OctaveException($"{fn}: argument must be a scalar, not empty");
        return m.Re[0];
    }
    internal static int I(Value v, string fn) => (int)D(v, fn);
    internal static string S(Value v, string fn)
    {
        if (v is MatrixValue m && (m.IsChar || m.Numel == 0)) return m.AsString();
        throw new OctaveException($"{fn}: argument must be a string");
    }
    internal static bool IsString(Value v) => v is MatrixValue m && m.IsChar && m.R <= 1;
    internal static Value Str(string s) => MatrixValue.Str(s);
    internal static Value Num(double d) => MatrixValue.Scalar(d);
    internal static Value Bool(bool b) => MatrixValue.Bool(b);
    internal static void NArgs(Value[] a, int min, int max, string fn)
    {
        if (a.Length < min || a.Length > max) throw new OctaveException($"Invalid call to {fn}", "Octave:invalid-fun-call");
    }

    /// <summary>Parse dims args as for zeros(n), zeros(r,c), zeros([r c]).</summary>
    internal static int[] DimsN(Value[] a, string fn, int start = 0)
    {
        var args = a.Skip(start).Where(v => !IsString(v)).ToArray();
        if (args.Length == 0) return new[] { 1, 1 };
        int[] d;
        if (args.Length == 1)
        {
            var m = M(args[0], fn);
            if (m.Numel == 1) { int n = Math.Max(0, (int)m.Re[0]); d = new[] { n, n }; }
            else d = m.Re.Select(x => Math.Max(0, (int)x)).ToArray();
        }
        else d = args.Select(x => Math.Max(0, I(x, fn))).ToArray();
        return NDArray.Normalize(d.Length == 0 ? new[] { 0, 0 } : d.Length == 1 ? new[] { d[0], d[0] } : d);
    }

    internal static (int r, int c) Dims(Value[] a, string fn, int start = 0)
    {
        var d = DimsN(a, fn, start);
        if (d.Length > 2) throw new OctaveException($"{fn}: N-d arrays are not supported here");
        return (d[0], d[1]);
    }

    /// <summary>Fill with N-D dims.</summary>
    internal static MatrixValue FillN(int[] d, double v, NumKind k = NumKind.Double)
    {
        var m = Fill(d[0], NDArray.Prod(d, 1), v, k);
        m.SetDims(d);
        return m;
    }

    internal static MatrixValue Fill(int r, int c, double v, NumKind k = NumKind.Double)
    {
        var m = new MatrixValue(r, c, k);
        if (v != 0) Array.Fill(m.Re, v);
        return m;
    }

    /// <summary>Default reduction dimension (1-based): first non-singleton.</summary>
    internal static MatrixValue ClassFill(Value[] a, string fn, double v)
    {
        var kind = NumKind.Double;
        if (a.Length >= 2 && IsString(a[^2]) && string.Equals(S(a[^2], fn), "like", StringComparison.OrdinalIgnoreCase))
        {
            var proto = a[^1]; a = a[..^2];
            var lm0 = FillN(DimsN(a, fn), v);
            if (proto is MatrixValue pm && !pm.IsChar) { if (pm.Kind != NumKind.Double) lm0 = lm0.CastInPlace(pm.Kind == NumKind.Logical && v != 0 && v != 1 ? NumKind.Double : pm.Kind); if (pm.Im != null) lm0.Im = new double[lm0.Re.Length]; }
            else if (proto is MatRewrite.Core.Sparse.SparseValue) return lm0;
            else if (proto is not MatrixValue) throw new OctaveException($"{fn}: invalid data type specified");
            return lm0;
        }
        if (a.Length > 0 && IsString(a[^1]))
        {
            var name = S(a[^1], fn);
            if (name != "like") kind = NumTypes.FromName(name) ?? throw new OctaveException($"{fn}: invalid class name '{name}'");
        }
        var m = FillN(DimsN(a, fn), v);
        return kind == NumKind.Double ? m : m.CastInPlace(kind);
    }

    internal static int DefDim(ArrayValue m)
    {
        var d = m.Dims;
        for (int k = 0; k < d.Length; k++) if (d[k] != 1) return k + 1;
        return 1;
    }

    /// <summary>(stride, len, outer) decomposition for working along dim (1-based).</summary>
    internal static (int stride, int len, int outer, int[] outDims) Split(ArrayValue m, int dim)
    {
        var d = NDArray.Pad(m.Dims, Math.Max(dim, 2));
        int stride = NDArray.Prod(d, 0, dim - 1), len = d[dim - 1], outer = NDArray.Prod(d, dim);
        var od = (int[])d.Clone(); od[dim - 1] = 1;
        return (stride, len, outer, od);
    }

    internal static int DimArg(Value[] a, int idx, ArrayValue m, string fn)
    {
        if (a.Length > idx && !(a[idx] is MatrixValue e && e.Numel == 0)) { int d = I(a[idx], fn); if (d < 1) throw new OctaveException($"{fn}: DIM must be a valid dimension"); return d; }
        return DefDim(m);
    }

    /// <summary>Apply f over each vector along dim; f returns one value.</summary>
    internal static MatrixValue Reduce(MatrixValue m, int dim, Func<double[], double> f, NumKind kind = NumKind.Double)
    {
        if (m.R == 0 && m.C == 0 && m.ND == null) return new MatrixValue(1, 1, new[] { f(Array.Empty<double>()) }, null, kind);
        var (stride, len, outer, od) = Split(m, dim);
        var r = new MatrixValue(1, stride * outer, kind);
        var buf = new double[len];
        for (int o = 0; o < outer; o++)
            for (int s = 0; s < stride; s++)
            {
                int b = s + o * stride * len;
                for (int k = 0; k < len; k++) buf[k] = m.Re[b + k * stride];
                r.Re[s + o * stride] = f(buf);
            }
        r.SetDims(od);
        return r;
    }

    internal static MatrixValue ReduceC(MatrixValue m, int dim, Func<Complex[], Complex> f)
    {
        if (m.R == 0 && m.C == 0 && m.ND == null) return MatrixValue.Cplx(f(Array.Empty<Complex>()));
        var (stride, len, outer, od) = Split(m, dim);
        var r = new MatrixValue(1, stride * outer, NumKind.Double, true);
        var buf = new Complex[len];
        for (int o = 0; o < outer; o++)
            for (int s = 0; s < stride; s++)
            {
                int b = s + o * stride * len;
                for (int k = 0; k < len; k++) buf[k] = m.At(b + k * stride);
                var z = f(buf); r.Re[s + o * stride] = z.Real; r.Im![s + o * stride] = z.Imaginary;
            }
        r.SetDims(od);
        return r.Narrow();
    }

    /// <summary>Apply f to each vector along dim, f returns the same-length vector.</summary>
    internal static MatrixValue AlongDim(MatrixValue m, int dim, Func<double[], double[]> f, NumKind kind = NumKind.Double)
    {
        var r = new MatrixValue(m.R, m.C, kind) { ND = m.ND };
        var (stride, len, outer, _) = Split(m, dim);
        var buf = new double[len];
        for (int o = 0; o < outer; o++)
            for (int s = 0; s < stride; s++)
            {
                int b = s + o * stride * len;
                for (int k = 0; k < len; k++) buf[k] = m.Re[b + k * stride];
                var res = f(buf);
                for (int k = 0; k < len; k++) r.Re[b + k * stride] = res[k];
            }
        return r;
    }

    internal static Value[] Out(params Value[] v) => v;
}
