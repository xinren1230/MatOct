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
    internal static (int r, int c) Dims(Value[] a, string fn, int start = 0)
    {
        var args = a.Skip(start).Where(v => !IsString(v)).ToArray();
        if (args.Length == 0) return (1, 1);
        if (args.Length == 1)
        {
            var m = M(args[0], fn);
            if (m.Numel == 1) { int n = Math.Max(0, (int)m.Re[0]); return (n, n); }
            if (m.Numel >= 2)
            {
                for (int k = 2; k < m.Numel; k++) if (m.Re[k] != 1) throw new OctaveException($"{fn}: N-d arrays are not supported yet");
                return (Math.Max(0, (int)m.Re[0]), Math.Max(0, (int)m.Re[1]));
            }
            return (0, 0);
        }
        for (int k = 2; k < args.Length; k++) if (D(args[k], fn) != 1) throw new OctaveException($"{fn}: N-d arrays are not supported yet");
        return (Math.Max(0, I(args[0], fn)), Math.Max(0, I(args[1], fn)));
    }

    internal static MatrixValue Fill(int r, int c, double v, NumKind k = NumKind.Double)
    {
        var m = new MatrixValue(r, c, k);
        if (v != 0) Array.Fill(m.Re, v);
        return m;
    }

    /// <summary>Default reduction dimension (1-based): first non-singleton.</summary>
    internal static int DefDim(ArrayValue m) => m.R != 1 ? 1 : 2;

    internal static int DimArg(Value[] a, int idx, ArrayValue m, string fn)
    {
        if (a.Length > idx && !(a[idx] is MatrixValue e && e.Numel == 0)) { int d = I(a[idx], fn); if (d < 1) throw new OctaveException($"{fn}: DIM must be a valid dimension"); return d; }
        return DefDim(m);
    }

    /// <summary>Apply f over each vector along dim; f returns one value.</summary>
    internal static MatrixValue Reduce(MatrixValue m, int dim, Func<double[], double> f, NumKind kind = NumKind.Double)
    {
        if (m.R == 0 && m.C == 0) return new MatrixValue(1, 1, new[] { f(Array.Empty<double>()) }, null, kind);
        if (dim == 1)
        {
            var r = new MatrixValue(1, m.C, kind);
            var buf = new double[m.R];
            for (int j = 0; j < m.C; j++) { Array.Copy(m.Re, j * m.R, buf, 0, m.R); r.Re[j] = f(buf); }
            return r;
        }
        if (dim == 2)
        {
            var r = new MatrixValue(m.R, 1, kind);
            var buf = new double[m.C];
            for (int i = 0; i < m.R; i++) { for (int j = 0; j < m.C; j++) buf[j] = m.Re[i + j * m.R]; r.Re[i] = f(buf); }
            return r;
        }
        return Ops.Map(m, x => f(new[] { x }), null, null, kind);
    }

    internal static MatrixValue ReduceC(MatrixValue m, int dim, Func<Complex[], Complex> f)
    {
        Complex[] Vec(int start, int count, int stride) { var v = new Complex[count]; for (int k = 0; k < count; k++) v[k] = m.At(start + k * stride); return v; }
        MatrixValue r;
        if (m.R == 0 && m.C == 0) return MatrixValue.Cplx(f(Array.Empty<Complex>()));
        if (dim == 1) { r = new MatrixValue(1, m.C, NumKind.Double, true); for (int j = 0; j < m.C; j++) { var z = f(Vec(j * m.R, m.R, 1)); r.Re[j] = z.Real; r.Im![j] = z.Imaginary; } }
        else if (dim == 2) { r = new MatrixValue(m.R, 1, NumKind.Double, true); for (int i = 0; i < m.R; i++) { var z = f(Vec(i, m.C, m.R)); r.Re[i] = z.Real; r.Im![i] = z.Imaginary; } }
        else return m;
        return r.Narrow();
    }

    /// <summary>Apply f to each vector along dim, f returns the same-length vector.</summary>
    internal static MatrixValue AlongDim(MatrixValue m, int dim, Func<double[], double[]> f, NumKind kind = NumKind.Double)
    {
        var r = new MatrixValue(m.R, m.C, kind);
        if (dim == 1)
        {
            var buf = new double[m.R];
            for (int j = 0; j < m.C; j++) { Array.Copy(m.Re, j * m.R, buf, 0, m.R); var o = f(buf); Array.Copy(o, 0, r.Re, j * m.R, m.R); }
        }
        else if (dim == 2)
        {
            var buf = new double[m.C];
            for (int i = 0; i < m.R; i++) { for (int j = 0; j < m.C; j++) buf[j] = m.Re[i + j * m.R]; var o = f(buf); for (int j = 0; j < m.C; j++) r.Re[i + j * m.R] = o[j]; }
        }
        else Array.Copy(m.Re, r.Re, m.Numel);
        return r;
    }

    internal static Value[] Out(params Value[] v) => v;
}
