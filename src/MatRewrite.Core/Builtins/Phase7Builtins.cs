// MatRewrite - GPL-3.0-or-later
// Phase 7: high-frequency Octave library functions implemented natively in C#
// (error messages and edge cases follow the corresponding Octave m-files).
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    // ------------------------------------------------------------ helpers
    static Value Fc(Interpreter it, string f, params Value[] a)
    {
        var r = it.CallFunction(f, a, 1);
        if (r.Length == 0) throw new OctaveException($"{f}: value undefined");
        return r[0];
    }
    static Value[] Fn(Interpreter it, string f, int n, params Value[] a) => it.CallFunction(f, a, n);
    static Value Bx(string op, Value x, Value y) => Ops.Binary(op, x, y);
    static string Sx(Value v) => v is MatrixValue m ? m.AsString() : throw new OctaveException("argument must be a string");
    static Value Dn(double d) => MatrixValue.Scalar(d);
    static bool IsNum(Value v) => v is MatrixValue m ? !m.IsChar && m.Kind != NumKind.Logical : v is Sparse.SparseValue;
    static bool IsNumL(Value v) => v is MatrixValue m ? !m.IsChar : v is Sparse.SparseValue;
    static bool IsCplxV(Value v) => v is MatrixValue m && m.Im != null;
    static bool Tr(Value v) => Ops.IsTrue(v);
    static bool IsScal(Value v) => v is ArrayValue av && av.Numel == 1 || v is not ArrayValue && v is not Sparse.SparseValue;
    static bool IsCellStr(Value v) => v is CellValue c && c.Data.All(x => x is MatrixValue m && (m.IsChar || m.Numel == 0 && m.Kind == NumKind.Char));
    static int[] SizeOf(Value v) => v is ArrayValue av ? av.Dims.ToArray() : v is Sparse.SparseValue sp ? new[] { sp.R, sp.C } : new[] { 1, 1 };
    static int FirstNonSingleton(Value v) { var d = SizeOf(v); for (int k = 0; k < d.Length; k++) if (d[k] != 1) return k + 1; return 1; }
    static int FirstGt1(Value v) { var d = SizeOf(v); for (int k = 0; k < d.Length; k++) if (d[k] > 1) return k + 1; return 1; }
    static double[] Re(Value v, string fn) => M(v, fn).Re;
    static MatrixValue Copy(MatrixValue m) => new(m.R, m.C, m.Re.ToArray(), m.Im?.ToArray(), m.Kind) { ND = m.ND?.ToArray() };
    static Value TooMany(string fn) => throw new OctaveException($"{fn}: function called with too many inputs", "Octave:invalid-fun-call");
    static Value Usage(string fn) => throw new OctaveException($"Invalid call to {fn}", "Octave:invalid-fun-call");

    static void RegisterPhase7()
    {
        RegisterPhase7Elfun();
        RegisterPhase7General();
        RegisterPhase7Stats();
        RegisterPhase7Strings();
        RegisterPhase7Matrices();
        RegisterPhase7Sets();
        RegisterPhase7Mov();
        //RegisterPhase7Linalg();
        //RegisterPhase7Poly();
        RegisterPhase8();
    }

    // ------------------------------------------------------------ elfun
    static void RegisterPhase7Elfun()
    {
        void E(string name, Func<Interpreter, Value, Value> f) =>
            Reg(name, (it, a, n) => { if (a.Length != 1) Usage(name); return new[] { f(it, a[0]) }; });
        Value Inv(Value x) => Bx("./", Dn(1), x);
        Value ToDeg(Value r) => Bx("./", Bx(".*", r, Dn(180)), Dn(Math.PI));
        E("acot", (it, x) => Fc(it, "atan", Inv(x)));
        E("acoth", (it, x) => Fc(it, "atanh", Inv(x)));
        E("acsc", (it, x) => Fc(it, "asin", Inv(x)));
        E("acsch", (it, x) => Fc(it, "asinh", Inv(x)));
        E("asec", (it, x) => Fc(it, "acos", Inv(x)));
        E("asech", (it, x) => Fc(it, "acosh", Inv(x)));
        E("acosd", (it, x) => ToDeg(Fc(it, "acos", x)));
        E("asind", (it, x) => ToDeg(Fc(it, "asin", x)));
        E("acscd", (it, x) => ToDeg(Fc(it, "acsc", x)));
        E("asecd", (it, x) => ToDeg(Fc(it, "asec", x)));
        E("atand", (it, x) => Bx(".*", Bx("./", Dn(180), Dn(Math.PI)), Fc(it, "atan", x)));
        E("acotd", (it, x) => Fc(it, "atand", Inv(x)));
        E("cot", (it, x) => Inv(Fc(it, "tan", x)));
        E("coth", (it, x) => Inv(Fc(it, "tanh", x)));
        E("csc", (it, x) => Inv(Fc(it, "sin", x)));
        E("csch", (it, x) => Inv(Fc(it, "sinh", x)));
        E("sec", (it, x) => Inv(Fc(it, "cos", x)));
        E("sech", (it, x) => Inv(Fc(it, "cosh", x)));
        E("cotd", (it, x) => Inv(Fc(it, "tand", x)));
        E("cscd", (it, x) => Inv(Fc(it, "sind", x)));
        E("secd", (it, x) => Inv(Fc(it, "cosd", x)));
        Reg("atan2d", (it, a, n) => { if (a.Length != 2) Usage("atan2d"); return new[] { Bx(".*", Bx("./", Dn(180), Dn(Math.PI)), Fc(it, "atan2", a[0], a[1])) }; });
        Value Sind(Interpreter it, Value x, string fn)
        {
            if (!IsNum(x)) throw new OctaveException($"{fn}: X must be numeric");
            bool cx = IsCplxV(x);
            var xi = cx ? Fc(it, "imag", x) : null;
            var xr = Bx("-", Fc(it, "mod", Bx("-", Fc(it, "real", x), Dn(180)), Dn(360)), Dn(180));
            var y = (MatrixValue)Fc(it, "sin", Bx("*", Bx("/", cx ? Fc(it, "complex", xr, xi!) : xr, Dn(180)), Dn(Math.PI)));
            y = Copy(y);
            var xm = (MatrixValue)xr;
            for (int k = 0; k < xm.Numel; k++) if (xm.Re[k] == -180) y.Re[k] = 0;
            return y;
        }
        E("sind", (it, x) => Sind(it, x, "sind"));
        E("cosd", (it, x) => { if (!IsNum(x)) throw new OctaveException("cosd: X must be numeric"); return Sind(it, Bx("+", x, Dn(90)), "cosd"); });
        E("tand", (it, x) =>
        {
            var i0 = M(Bx("/", x, Dn(180)), "tand"); var i90 = M(Bx("/", Bx("-", x, Dn(90)), Dn(180)), "tand");
            var y = Copy(M(Fc(it, "tan", Bx(".*", i0, Dn(Math.PI))), "tand"));
            for (int k = 0; k < y.Numel; k++)
            {
                if (i0.Re[k] == Math.Truncate(i0.Re[k]) && double.IsFinite(i0.Re[k])) { y.Re[k] = 0; if (y.Im != null) y.Im[k] = 0; }
                if (i90.Re[k] == Math.Truncate(i90.Re[k]) && double.IsFinite(i90.Re[k])) { y.Re[k] = double.PositiveInfinity; if (y.Im != null) y.Im[k] = 0; }
            }
            return y.Im != null ? y.Narrow() : y;
        });
        Value Pi(Interpreter it, Value x, double off)
        {
            var r = Copy(M(Bx("-", Fc(it, "mod", Bx("-", x, Dn(off)), Dn(2)), Dn(1)), "sinpi"));
            for (int k = 0; k < r.Numel; k++) if (r.Re[k] == -1) r.Re[k] = 0;
            return Fc(it, "sin", Bx("*", r, Dn(Math.PI)));
        }
        E("sinpi", (it, x) => Pi(it, x, 1));
        E("cospi", (it, x) => Pi(it, x, 0.5));
        Reg1("deg2rad", a =>
        {
            if (a.Length != 1) Usage("deg2rad");
            if (a[0] is not MatrixValue m || m.IsChar || m.Kind == NumKind.Logical || m.IsInteger) throw new OctaveException("deg2rad: DEG must be a floating point class (double or single)");
            return Bx("*", m, Dn(Math.PI / 180));
        });
        Reg1("rad2deg", a =>
        {
            if (a.Length != 1) Usage("rad2deg");
            if (a[0] is not MatrixValue m || m.IsChar || m.Kind == NumKind.Logical || m.IsInteger) throw new OctaveException("rad2deg: RAD must be a floating point class (double or single)");
            return Bx("*", m, Dn(180 / Math.PI));
        });
        Reg("nthroot", (it, a, n) =>
        {
            if (a.Length != 2) Usage("nthroot");
            if (IsCplxV(a[0])) throw new OctaveException("nthroot: X must not contain complex values");
            if (IsCplxV(a[1]) || !IsScal(a[1]) || D(a[1], "nthroot") == 0) throw new OctaveException("nthroot: N must be a real nonzero scalar");
            return new[] { NthRoot(it, a[0], D(a[1], "nthroot")) };
        });
    }

    static Value NthRoot(Interpreter it, Value x, double n)
    {
        if (n == 3) return Fc(it, "cbrt", x);
        if (n == -3) return Bx("./", Dn(1), Fc(it, "cbrt", x));
        if (n < 0) return Bx("./", Dn(1), NthRoot(it, x, -n));
        bool integer = n == Math.Truncate(n);
        Value y;
        if (integer && n % 2 == 1) y = Bx(".*", Bx(".^", Fc(it, "abs", x), Dn(1 / n)), Fc(it, "sign", x));
        else if (M(x, "nthroot").Re.Any(v => v < 0)) throw new OctaveException("nthroot: N must be an odd integer if X contains negative values");
        else y = Bx(".^", x, Dn(1 / n));
        if (integer && n > 0 && double.IsFinite(n))
        {
            var ym = M(y, "nthroot");
            if (!(ym.Numel == 1 && ym.Re[0] == 0))
            {
                var y2 = M(Bx("/", Bx("+", Bx("*", Dn(n - 1), y), Bx("./", x, Bx(".^", y, Dn(n - 1)))), Dn(n)), "nthroot");
                var r = Copy(y2); var xm = M(x, "nthroot");
                for (int k = 0; k < r.Numel; k++) if (!double.IsFinite(r.Re[k])) r.Re[k] = xm.Numel == 1 ? xm.Re[0] : xm.Re[k];
                return r;
            }
        }
        return y;
    }
}
