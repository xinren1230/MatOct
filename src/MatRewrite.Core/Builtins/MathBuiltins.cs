// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void Unary(string name, Func<double, double> fr, Func<Complex, Complex>? fc = null, Func<double, bool>? needC = null)
        => Reg1(name, a => { NArgs(a, 1, 1, name); return Ops.Map(M(a[0], name), fr, fc, needC); });

    static void Pred(string name, Func<double, double, bool> f)
        => Reg1(name, a =>
        {
            NArgs(a, 1, 1, name);
            var m = M(a[0], name);
            var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = (int[]?)m.ND?.Clone() };
            for (int k = 0; k < m.Numel; k++) r.Re[k] = f(m.Re[k], m.Im?[k] ?? 0) ? 1 : 0;
            return r;
        });

    static void Binary2(string name, Func<double, double, double> f, Func<Complex, Complex, Complex>? fc = null)
        => Reg1(name, a => { NArgs(a, 2, 2, name); return Ops.Elementwise(M(a[0], name), M(a[1], name), name, f, fc); });

    static Complex CTanh(Complex z)
    {
        double tx = Math.Tanh(z.Real), ty = Math.Tan(z.Imaginary);
        if (double.IsInfinity(ty)) return new Complex(tx == 0 ? 0 : 1 / tx, 0);
        return new Complex(tx, ty) / new Complex(1, tx * ty);
    }
    static Complex CAtanh(Complex z)
    {
        if (z.Imaginary == 0 && Math.Abs(z.Real) == 1) return new Complex(z.Real * double.PositiveInfinity, 0);
        var r = 0.5 * (Complex.Log(1 + z) - Complex.Log(1 - z));
        if (z.Imaginary == 0 && Math.Abs(z.Real) > 1) r = new Complex(r.Real, z.Real > 0 ? Math.PI / 2 : -Math.PI / 2);
        return r;
    }
    static double RoundHalfAway(double x) => Math.Round(x, MidpointRounding.AwayFromZero);

    internal static double Log1p(double x) { double u = 1 + x; if (u == 1) return x; if (double.IsInfinity(u)) return Math.Log(u); return Math.Log(u) * x / (u - 1); }
    internal static double Expm1(double x) { if (Math.Abs(x) < 1e-5) return x + x * x / 2 + x * x * x / 6; double u = Math.Exp(x); if (u == 1) return x; double um1 = u - 1; if (um1 == -1) return -1; if (double.IsInfinity(u)) return u; return um1 * x / Math.Log(u); }
    static void RegisterMath()
    {
        Unary("abs", Math.Abs);
        Reg1("abs", a => { NArgs(a, 1, 1, "abs"); var m = M(a[0], "abs"); if (!m.IsComplex) return Ops.Map(m, Math.Abs); var r = new MatrixValue(m.R, m.C) { ND = (int[]?)m.ND?.Clone() }; for (int k = 0; k < m.Numel; k++) r.Re[k] = m.At(k).Magnitude; return r; });
        Unary("sqrt", Math.Sqrt, Complex.Sqrt, x => x < 0);
        Unary("exp", Math.Exp, Complex.Exp);
        Unary("log", Math.Log, Complex.Log, x => x < 0);
        Unary("log2", Math.Log2, z => Complex.Log(z) / Math.Log(2), x => x < 0);
        Unary("log10", Math.Log10, Complex.Log10, x => x < 0);
        Unary("log1p", Log1p, z => Complex.Log(1 + z), x => x < -1);
        Unary("expm1", Expm1, z => Complex.Exp(z) - 1);
        Unary("sin", Math.Sin, Complex.Sin);
        Unary("cos", Math.Cos, Complex.Cos);
        Unary("tan", Math.Tan, Complex.Tan);
        Unary("asin", Math.Asin, Complex.Asin, x => Math.Abs(x) > 1);
        Unary("acos", Math.Acos, Complex.Acos, x => Math.Abs(x) > 1);
        Unary("atan", Math.Atan, Complex.Atan);
        Unary("sinh", Math.Sinh, Complex.Sinh);
        Unary("cosh", Math.Cosh, Complex.Cosh);
        Unary("tanh", Math.Tanh, CTanh);
        Unary("asinh", Math.Asinh, z => Complex.Log(z + Complex.Sqrt(z * z + 1)));
        Unary("acosh", Math.Acosh, z => Complex.Log(z + Complex.Sqrt(z + 1) * Complex.Sqrt(z - 1)), x => x < 1);
        Unary("atanh", Math.Atanh, CAtanh, x => Math.Abs(x) > 1);
        Unary("floor", Math.Floor, z => new Complex(Math.Floor(z.Real), Math.Floor(z.Imaginary)));
        Unary("ceil", Math.Ceiling, z => new Complex(Math.Ceiling(z.Real), Math.Ceiling(z.Imaginary)));
        Unary("round", RoundHalfAway, z => new Complex(RoundHalfAway(z.Real), RoundHalfAway(z.Imaginary)));
        Unary("fix", Math.Truncate, z => new Complex(Math.Truncate(z.Real), Math.Truncate(z.Imaginary)));
        Unary("sign", x => double.IsNaN(x) ? double.NaN : Math.Sign(x));
        Unary("gamma", x => MathNet.Numerics.SpecialFunctions.Gamma(x));
        Unary("gammaln", x => x > 0 || double.IsNaN(x) ? GammaLn(x) : MathNet.Numerics.SpecialFunctions.GammaLn(x));
        Unary("erf", MathNet.Numerics.SpecialFunctions.Erf);
        Unary("erfc", MathNet.Numerics.SpecialFunctions.Erfc);
        Unary("erfinv", x => x == 1 ? double.PositiveInfinity : x == -1 ? double.NegativeInfinity : x > 1 || x < -1 || double.IsNaN(x) ? double.NaN : MathNet.Numerics.SpecialFunctions.ErfInv(x));
        Unary("erfcinv", x => x == 0 ? double.PositiveInfinity : x == 2 ? double.NegativeInfinity : x < 0 || x > 2 || double.IsNaN(x) ? double.NaN : MathNet.Numerics.SpecialFunctions.ErfcInv(x));
        Unary("roundb", x => Math.Round(x, MidpointRounding.ToEven));
        Unary("factorial", x => x < 0 || x != Math.Floor(x) ? throw new OctaveException("factorial: all N must be real non-negative integers") : Math.Round(MathNet.Numerics.SpecialFunctions.Gamma(x + 1)));
        Reg1("real", a => { var m = M(a[0], "real"); return new MatrixValue(m.R, m.C, (double[])m.Re.Clone()) { ND = (int[]?)m.ND?.Clone() }; });
        Reg1("imag", a => { var m = M(a[0], "imag"); return new MatrixValue(m.R, m.C, m.Im == null ? new double[m.Numel] : (double[])m.Im.Clone()) { ND = (int[]?)m.ND?.Clone() }; });
        Reg1("conj", a => { var m = M(a[0], "conj").Copy(); m.Kind = NumKind.Double; if (m.Im != null) for (int k = 0; k < m.Numel; k++) m.Im[k] = -m.Im[k]; return m; });
        BuiltinFn angle = (it, a, n) => { var m = M(a[0], "angle"); var r = new MatrixValue(m.R, m.C) { ND = (int[]?)m.ND?.Clone() }; for (int k = 0; k < m.Numel; k++) r.Re[k] = Math.Atan2(m.Im?[k] ?? 0, m.Re[k]); return Out(r); };
        Reg("angle", angle); Reg("arg", angle);
        Reg1("complex", a => { var re = M(a[0], "complex"); var im = a.Length > 1 ? M(a[1], "complex") : Fill(re.R, re.C, 0);
            var r = Ops.Elementwise(re, im, "complex", (x, y) => x, null); r.Im = new double[r.Numel];
            var imB = Ops.Elementwise(re, im, "complex", (x, y) => y, null); Array.Copy(imB.Re, r.Im, r.Numel); return r; });

        Pred("isnan", (r, i) => double.IsNaN(r) || double.IsNaN(i));
        Pred("isinf", (r, i) => double.IsInfinity(r) || double.IsInfinity(i));
        Pred("isfinite", (r, i) => double.IsFinite(r) && double.IsFinite(i));

        Binary2("mod", (x, y) => y == 0 ? x : (x - Math.Floor(x / y) * y) is var r && Math.Abs(r - y) < Math.Abs(y) * 1e-15 ? 0 : x - Math.Floor(x / y) * y);
        Binary2("rem", (x, y) => y == 0 ? x : x - Math.Truncate(x / y) * y);
        Binary2("atan2", Math.Atan2);
        Binary2("hypot", (x, y) => Math.Sqrt(x * x + y * y));
        Binary2("power", Math.Pow, Ops.CPow);
        Binary2("idivide", (x, y) => Math.Truncate(x / y));
        Binary2("bitand", (x, y) => (double)((long)x & (long)y));
        Binary2("bitor", (x, y) => (double)((long)x | (long)y));
        Binary2("bitxor", (x, y) => (double)((long)x ^ (long)y));
        Binary2("bitshift", (x, y) => y >= 0 ? (double)((long)x << (int)y) : (double)((long)x >> (int)-y));
        Binary2("nchoosek", (n, k) => Math.Round(Math.Exp(MathNet.Numerics.SpecialFunctions.GammaLn(n + 1) - MathNet.Numerics.SpecialFunctions.GammaLn(k + 1) - MathNet.Numerics.SpecialFunctions.GammaLn(n - k + 1))));
        Reg1("gcd", a => a.Skip(1).Aggregate(M(a[0], "gcd"), (acc, v) => Ops.Elementwise(acc, M(v, "gcd"), "gcd", Gcd, null)));
        Reg1("lcm", a => a.Skip(1).Aggregate(M(a[0], "lcm"), (acc, v) => Ops.Elementwise(acc, M(v, "lcm"), "lcm", (x, y) => x == 0 || y == 0 ? 0 : Math.Abs(x * y) / Gcd(x, y), null)));
        Reg1("isprime", a =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to isprime");
            var m = M(a[0], "isprime");
            var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = (int[]?)m.ND?.Clone() };
            for (int k = 0; k < m.Numel; k++)
            {
                double re = m.Re[k], im = m.Im?[k] ?? 0;
                if (re != Math.Floor(re) || im != Math.Floor(im)) throw new OctaveException("isprime: X contains non-integer entries");
                bool v;
                if (m.IsComplex)
                {
                    double ar = Math.Abs(re), ai = Math.Abs(im);
                    if (ar == 0 || ai == 0) { double q = ar + ai; v = IsPrime(q) && q % 4 == 3; }
                    else v = IsPrime(ar * ar + ai * ai);
                }
                else v = IsPrime(Math.Abs(re));
                r.Re[k] = v ? 1 : 0;
            }
            return r;
        });
        Reg1("primes", a => { int n = I(a[0], "primes"); return MatrixValue.Row(Enumerable.Range(2, Math.Max(0, n - 1)).Where(k => IsPrime(k)).Select(k => (double)k)); });
        Reg1("factor", a => { long n = (long)D(a[0], "factor"); var f = new List<double>(); for (long p = 2; p * p <= n; p++) while (n % p == 0) { f.Add(p); n /= p; } if (n > 1 || f.Count == 0) f.Add(n); return MatrixValue.Row(f); });

        // operator-functions
        foreach (var (fn, op) in new[] { ("plus", "+"), ("minus", "-"), ("times", ".*"), ("rdivide", "./"), ("ldivide", ".\\"), ("mtimes", "*"), ("mrdivide", "/"), ("mldivide", "\\"), ("mpower", "^"),
                     ("eq", "=="), ("ne", "!="), ("lt", "<"), ("le", "<="), ("gt", ">"), ("ge", ">="), ("and", "&"), ("or", "|") })
        {
            string o = op;
            Reg1(fn, a => { if (a.Length < 2) throw new OctaveException($"Invalid call to {fn}"); Value r = a[0]; for (int k = 1; k < a.Length; k++) r = Ops.Binary(o, r, a[k]); return r; });
        }
        Reg1("not", a => Ops.Unary("!", a[0]));
        Reg1("uminus", a => Ops.Unary("-", a[0]));
        Reg1("uplus", a => a[0]);
        Reg1("xor", a => Ops.Elementwise(M(a[0], "xor"), M(a[1], "xor"), "xor", (x, y) => (x != 0) ^ (y != 0) ? 1 : 0, null, NumKind.Logical));
        Reg1("transpose", a => Ops.Transpose((ArrayValue)a[0], false));
        Reg1("ctranspose", a => Ops.Transpose((ArrayValue)a[0], true));

        // constants
        void Const(string name, double v) => Reg1(name, a => { return FillN(DimsN(a, name), v); });
        Const("pi", Math.PI); Const("e", Math.E); Const("Inf", double.PositiveInfinity); Const("inf", double.PositiveInfinity);
        Const("NaN", double.NaN); Const("nan", double.NaN); Const("NA", double.NaN);
        Reg1("eps", a =>
        {
            if (a.Length == 1 && a[0] is MatrixValue m && !m.IsChar) return Ops.Map(m, x => { x = Math.Abs(x); if (x == 0) return double.Epsilon; return Math.BitIncrement(x) - x; });
            return FillN(DimsN(a, "eps"), Math.Pow(2, -52));
        });
        Const("realmax", double.MaxValue); Const("realmin", 2.2250738585072014e-308);  Const("flintmax", 9007199254740992);
        Reg1("i", a => MatrixValue.Cplx(0, 1)); Reg1("j", a => MatrixValue.Cplx(0, 1));
        Reg1("I", a => MatrixValue.Cplx(0, 1)); Reg1("J", a => MatrixValue.Cplx(0, 1));
        Reg1("intmax", a => IntLimit(a, "intmax", true));
        Reg1("intmin", a => IntLimit(a, "intmin", false));
        Reg1("true", a => { return FillN(DimsN(a, "true"), 1, NumKind.Logical); });
        Reg1("false", a => { return FillN(DimsN(a, "false"), 0, NumKind.Logical); });
    }

    static double Gcd(double a, double b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) (a, b) = (b, a % b); return a; }
    static bool IsPrime(double x)
    {
        if (x < 2 || x != Math.Floor(x)) return false;
        if (x >= 18446744073709551615.0) return false;
        ulong n = (ulong)x;
        if (n < 4) return n >= 2;
        foreach (ulong sp in new ulong[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 }) { if (n == sp) return true; if (n % sp == 0) return false; }
        ulong d = n - 1; int sh = 0; while ((d & 1) == 0) { d >>= 1; sh++; }
        foreach (ulong b in new ulong[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            var y = System.Numerics.BigInteger.ModPow(b, d, n); if (y == 1 || y == n - 1) continue;
            bool comp = true;
            for (int t = 1; t < sh; t++) { y = y * y % n; if (y == n - 1) { comp = false; break; } }
            if (comp) return false;
        }
        return true;
    }

    static MatrixValue IntLimit(Value[] a, string fn, bool max)
    {
        var k = NumKind.Int32;
        if (a.Length > 0)
        {
            if (a[0] is MatrixValue m && m.IsInteger) k = m.Kind;
            else
            {
                var name = S(a[0], fn);
                k = NumTypes.FromName(name) is NumKind q && NumTypes.IsInt(q) ? q : throw new OctaveException($"{fn}: invalid class name '{name}'");
            }
        }
        if (k == NumKind.Int64) return MatrixValue.FromI64(1, 1, new[] { max ? long.MaxValue : long.MinValue }, k);
        if (k == NumKind.UInt64) return MatrixValue.FromI64(1, 1, new[] { max ? -1L : 0L }, k);
        var (lo, hi) = NumTypes.Range(k);
        return new MatrixValue(1, 1, new[] { max ? hi : lo }, null, k);
    }
}
