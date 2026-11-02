// MatRewrite - GPL-3.0-or-later
// gammainc / betainc ported from Octave scripts/specfun (+ the __gammainc__/__betainc__ continued fractions).
using System.Numerics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    [System.Runtime.InteropServices.DllImport("libm.so.6", EntryPoint = "lgamma")] static extern double LgammaLinux(double x);
    [System.Runtime.InteropServices.DllImport("ucrtbase.dll", EntryPoint = "lgamma")] static extern double LgammaWin(double x);
    static int _lgMode = -1; // -1 unknown, 0 managed, 1 linux, 2 windows

    /// <summary>log|Gamma(x)|: uses the C runtime lgamma (as Octave does) when available, else a Stirling-series fallback.</summary>
    static double GammaLn(double x)
    {
        if (_lgMode == -1)
        {
            try { LgammaLinux(2.5); _lgMode = 1; }
            catch { try { LgammaWin(2.5); _lgMode = 2; } catch { _lgMode = 0; } }
        }
        if (_lgMode == 1) return LgammaLinux(x);
        if (_lgMode == 2) return LgammaWin(x);
        return GammaLnManaged(x);
    }

    static double GammaLnManaged(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x <= 0)
        {
            if (x == Math.Floor(x)) return double.PositiveInfinity;
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - GammaLnManaged(1 - x);
        }
        if (x == Math.Floor(x) && x <= 30)
        {
            double f = 1; for (int k = 2; k < x; k++) f *= k; return Math.Log(f);
        }
        double prod = 1, z = x;
        while (z < 20) { prod *= z; z += 1; }
        double z2 = 1 / (z * z);
        double ser = (1.0 / 12 + z2 * (-1.0 / 360 + z2 * (1.0 / 1260 + z2 * (-1.0 / 1680 + z2 * (1.0 / 1188 + z2 * (-691.0 / 360360 + z2 * (1.0 / 156))))))) / z;
        return (z - 0.5) * Math.Log(z) - z + 0.91893853320467274178 + ser - Math.Log(prod);
    }

    static double CfGammaInc(double x, double a)
    {
        const double tiny = 7.888609052210118e-31; // 2^-100 as in Octave (matters when the CF has not converged)
        double eps = 2.220446049250313e-16;
        double y = tiny, Cj = y, Dj = 0, bj = x - a + 1, Deltaj = 0, alpha = 1; int j = 1;
        while (Math.Abs((Deltaj - 1) / y) > eps && j < 200)
        {
            Dj = bj + alpha * Dj; if (Dj == 0) Dj = tiny;
            Cj = bj + alpha / Cj; if (Cj == 0) Cj = tiny;
            Dj = 1 / Dj; Deltaj = Cj * Dj; y *= Deltaj;
            alpha = -j * (j - a); bj += 2; j++;
        }
        return y * a;
    }

    static double CfBetaInc(double x, double a, double b)
    {
        const double tiny = 1e-300; double eps = 2.220446049250313e-16;
        double f = tiny, C = f, Dd = 0, alpha = 1, beta = a - (a * (a + b)) / (a + 1) * x, x2 = x * x, Delta = 0; int j = 1;
        while (Math.Abs(Delta - 1) > eps && j < 200)
        {
            Dd = beta + alpha * Dd; if (Dd == 0) Dd = tiny;
            C = beta + alpha / C; if (C == 0) C = tiny;
            Dd = 1 / Dd; Delta = C * Dd; f *= Delta;
            alpha = ((a + j - 1) * (a + b + j - 1) * (b - j) * j) / ((a + 2 * j - 1) * (a + 2 * j - 1)) * x2;
            beta = a + 2 * j + ((j * (b - j)) / (a + 2 * j - 1) - ((a + j) * (a + b + j)) / (a + 2 * j + 1)) * x;
            j++;
        }
        return f;
    }

    static Complex GiD(double x, double a)
    {
        if (x == 0) return 0;
        const double th = 10;
        double LnGa(double q) => Math.Log(2 * Math.PI * q) / 2 + 1 / (12 * q) - 1 / (360 * Math.Pow(q, 3)) + 1 / (1260 * Math.Pow(q, 5))
            - 1 / (1680 * Math.Pow(q, 7)) + 1 / (1188 * Math.Pow(q, 9)) - 691 / (87360 * Math.Pow(q, 11)) + 1 / (156 * Math.Pow(q, 13))
            - 3617 / (122400 * Math.Pow(q, 15)) + 43867 / (244188 * Math.Pow(q, 17)) - 174611 / (125400 * Math.Pow(q, 19));
        if (x > 0 && a > th && a >= x) { double lns = Log1p((a - x) / x); return Math.Exp((a - x) - a * lns - LnGa(a)); }
        if (x > 0 && a > th && a < x) { double lns = -Log1p((x - a) / a); return Math.Exp((a - x) - a * lns - LnGa(a)); }
        Complex y = Complex.Exp(a * Complex.Log(x) - x - GammaLn(a + 1));
        if (x < 0 && a == Math.Floor(a)) y = y.Real;
        return y;
    }

    static Complex GammaIncOne(double x, double a, string tail)
    {
        if (double.IsPositiveInfinity(x) && double.IsPositiveInfinity(a)) return double.NaN;
        if (x == 0 && a == 0) return tail is "upper" or "scaledupper" ? 0 : 1;
        if (x == 0) return tail == "lower" ? 0 : tail is "upper" or "scaledlower" ? 1 : double.PositiveInfinity;
        if (double.IsPositiveInfinity(x)) return tail == "lower" ? 1 : tail is "upper" or "scaledupper" ? 0 : double.PositiveInfinity;
        if (double.IsPositiveInfinity(a)) return tail == "lower" ? 0 : tail is "upper" or "scaledlower" ? 1 : double.PositiveInfinity;
        if (a == 0) return tail == "lower" ? 1 : tail == "scaledlower" ? Math.Exp(x) : 0;
        if (a == 1)
        {
            return tail switch
            {
                "lower" => Math.Abs(x) < 0.5 ? -Expm1(-x) : 1 - Math.Exp(-x),
                "upper" => Math.Exp(-x),
                "scaledlower" => Math.Abs(x) < 0.5 ? Expm1(x) / x : (Math.Exp(x) - 1) / x,
                _ => 1 / x,
            };
        }
        if (a == Math.Floor(a) && a > 1 && a <= 18 && x <= 36 && Math.Abs(x) >= .1)
        {
            double y = 1, t = 1; for (int i = 1; a > i; i++) { t *= x / i; y += t; }
            return tail switch
            {
                "lower" => 1 - Math.Exp(-x) * y,
                "upper" => y * Math.Exp(-x),
                "scaledlower" => (1 - Math.Exp(-x) * y) / GiD(x, a),
                _ => y * Math.Exp(-x) / GiD(x, a),
            };
        }
        bool small = Math.Abs(a) > 0 && Math.Abs(a) < 2 && x < 0;
        double a0 = a; if (small) a += 2;
        Complex r;
        bool flagS = ((x + 0.25 < a) || (x < 0)) && (x > -20) || Math.Abs(x) < 1;
        if (flagS)
        {
            Complex y, term;
            bool sc = tail is "scaledlower" or "scaledupper";
            if (sc) { y = 1; term = x / (a + 1); }
            else { y = GiD(x, a); term = y * x / (a + 1); }
            int n = 1; double eps = 2.220446049250313e-16;
            while (Complex.Abs(term) > Complex.Abs(y) * eps) { n++; y += term; term *= x / (a + n); }
            if (tail == "upper") y = 1 - y; else if (tail == "scaledupper") y = 1 / GiD(x, a) - y;
            r = y;
        }
        else
        {
            Complex y = CfGammaInc(x, a);
            if (tail == "lower") y = 1 - y * GiD(x, a);
            else if (tail == "upper") y *= GiD(x, a);
            else if (tail == "scaledlower") y = 1 / GiD(x, a) - y;
            r = y;
        }
        if (small)
        {
            double aa = a; // a+2
            if (tail == "lower") r += GiD(x, aa - 1) + GiD(x, aa - 2);
            else if (tail == "upper") r -= GiD(x, aa - 1) + GiD(x, aa - 2);
            else if (tail == "scaledlower") r = r * (x * x) / (aa * (aa - 1)) + (x / (aa - 1)) + 1;
            else r = r * (x * x) / (aa * (aa - 1)) - (x / (aa - 1)) - 1;
        }
        return r;
    }

    static (MatrixValue[] args, int[] dims) CommonSize(Interpreter it, string fn, string msg, params Value[] vs)
    {
        var res = Fn(it, "common_size", vs.Length + 1, vs);
        if (D(res[0], fn) > 0) throw new OctaveException(msg);
        var ms = res.Skip(1).Select(v => (MatrixValue)v).ToArray();
        return (ms, ms[0].Dims.ToArray());
    }

    static Value ClassConst(Interpreter it, Value[] a, string fn, double dv, double sv)
    {
        bool single = false; var rest = a.ToList();
        if (rest.Count > 0 && rest[^1] is MatrixValue c && c.IsChar)
        {
            var cl = c.AsString();
            if (cl == "single") single = true; else if (cl != "double") throw new OctaveException($"{fn}: CLASS must be \"double\" or \"single\"");
            rest.RemoveAt(rest.Count - 1);
        }
        Value r = rest.Count == 0 ? Dn(single ? sv : dv) : Bx("*", Fc(it, "ones", rest.ToArray()), Dn(single ? sv : dv));
        return single ? Fc(it, "single", r) : r;
    }

    static void RegisterPhase8Special()
    {
        Reg("eps", (it, a, n) =>
        {
            if (a.Length == 1 && a[0] is MatrixValue m && !m.IsChar)
            {
                if (!(m.Kind == NumKind.Double || m.Kind == NumKind.Single)) throw new OctaveException("eps: X must be of a floating point type");
                var r = new double[m.Numel];
                for (int k = 0; k < r.Length; k++)
                {
                    double x = Math.Abs(m.Re[k]);
                    if (m.IsSingle)
                    {
                        float f = (float)x;
                        r[k] = float.IsNaN(f) || float.IsInfinity(f) ? double.NaN : f < 1.17549435e-38f ? Math.Pow(2, -149) : (double)MathF.BitIncrement(f) - f;
                    }
                    else r[k] = double.IsNaN(x) || double.IsInfinity(x) ? double.NaN : x < 2.2250738585072014e-308 ? double.Epsilon : Math.BitIncrement(x) - x;
                }
                var mv = new MatrixValue(1, r.Length, r, null); mv.SetDims(m.Dims.ToArray());
                if (m.IsSingle) mv.CastInPlace(NumKind.Single);
                return new Value[] { mv };
            }
            return new[] { ClassConst(it, a, "eps", Math.Pow(2, -52), Math.Pow(2, -23)) };
        });
        Reg("realmax", (it, a, n) => new[] { ClassConst(it, a, "realmax", double.MaxValue, float.MaxValue) });
        Reg("realmin", (it, a, n) => new[] { ClassConst(it, a, "realmin", 2.2250738585072014e-308, 1.17549435e-38) });
        Reg("flintmax", (it, a, n) =>
        {
            if (a.Length == 1 && !(a[0] is MatrixValue fc && fc.IsChar))
            {
                var cls = Fc(it, "class", a[0]) is MatrixValue cv ? cv.AsString() : "double";
                if (cls == "single") return new[] { Fc(it, "single", Dn(16777216)) };
                if (cls == "double") return new[] { Dn(9007199254740992) };
                throw new OctaveException($"flintmax: not defined for class '{cls}'");
            }
            return new[] { ClassConst(it, a, "flintmax", 9007199254740992, 16777216) };
        });
        Reg("__gammainc__", (it, a, n) =>
        {
            var x = M(a[0], "__gammainc__"); var aa = M(a[1], "__gammainc__");
            var r = new double[x.Numel]; for (int k = 0; k < r.Length; k++) r[k] = CfGammaInc(x.Re[k], aa.Re[aa.Numel == 1 ? 0 : k]);
            var m = new MatrixValue(1, r.Length, r, null); m.SetDims(x.Dims.ToArray()); return new Value[] { m };
        });
        Reg("__betainc__", (it, a, n) =>
        {
            var x = M(a[0], "__betainc__"); var aa = M(a[1], "__betainc__"); var bb = M(a[2], "__betainc__");
            var r = new double[x.Numel]; for (int k = 0; k < r.Length; k++) r[k] = CfBetaInc(x.Re[k], aa.Re[k], bb.Re[k]);
            var m = new MatrixValue(1, r.Length, r, null); m.SetDims(x.Dims.ToArray()); return new Value[] { m };
        });
        Reg("gammainc", (it, a, n) =>
        {
            if (a.Length < 2) Usage("gammainc");
            if (a.Length > 3) TooMany("gammainc");
            var (ms, dims) = CommonSize(it, "gammainc", "gammainc: X and A must be of common size or scalars", a[0], a[1]);
            if (ms[0].Im != null || ms[1].Im != null) throw new OctaveException("gammainc: all inputs must be real");
            if (ms[1].Re.Any(v => v < 0)) throw new OctaveException("gammainc: A must be non-negative");
            string tail = "lower";
            if (a.Length == 3)
            {
                tail = a[2] is MatrixValue tm && tm.IsChar ? tm.AsString().ToLowerInvariant() : "";
                if (tail is not ("lower" or "upper" or "scaledlower" or "scaledupper")) throw new OctaveException("gammainc: invalid value for TAIL");
            }
            bool single = ms[0].IsSingle || ms[1].IsSingle;
            int N = ms[0].Numel; var re = new double[N]; var im = new double[N]; bool cx = false;
            for (int k = 0; k < N; k++)
            {
                double xv = ms[0].Re[k], av = ms[1].Re[k];
                if (single) { xv = (float)xv; av = (float)av; }
                var y = GammaIncOne(xv, av, tail);
                re[k] = y.Real; im[k] = y.Imaginary; if (y.Imaginary != 0) cx = true;
            }
            var r = new MatrixValue(1, N, re, cx ? im : null); r.SetDims(dims);
            if (single) r.CastInPlace(NumKind.Single);
            return new Value[] { r };
        });
        Reg("betainc", (it, a, n) =>
        {
            if (a.Length < 3) Usage("betainc");
            if (a.Length > 4) TooMany("betainc");
            var (ms, dims) = CommonSize(it, "betainc", "betainc: X, A, and B must be of common size or scalars", a[0], a[1], a[2]);
            if (ms.Any(m => m.Im != null)) throw new OctaveException("betainc: all inputs must be real");
            if (ms.Any(m => m.IsChar || m.IsLogical)) throw new OctaveException("betainc: all inputs must be numeric");
            string tail = a.Length == 4 && a[3] is MatrixValue tm && tm.IsChar ? tm.AsString().ToLowerInvariant() : a.Length == 4 ? "?" : "lower";
            if (tail is not ("lower" or "upper")) throw new OctaveException("betainc: invalid value for TAIL");
            bool lower = tail == "lower";
            if (ms[0].Re.Any(v => v < 0 || v > 1)) throw new OctaveException("betainc: X must be in the range [0, 1]");
            if (ms[1].Re.Any(v => v <= 0)) throw new OctaveException("betainc: A must be strictly positive");
            if (ms[2].Re.Any(v => v <= 0)) throw new OctaveException("betainc: B must be strictly positive");
            if (ms[0].IsInteger) return new Value[] { ms[0] };
            bool single = ms.Any(m => m.IsSingle);
            int N = ms[0].Numel; var I = new double[N];
            for (int k = 0; k < N; k++)
            {
                double x = ms[0].Re[k], A = ms[1].Re[k], B = ms[2].Re[k];
                if (single) { x = (float)x; A = (float)A; B = (float)B; }
                bool xt = x == 0 || x == 1;
                if ((A == 1 && B == 1) || xt) { I[k] = lower ? x : 1 - x; continue; }
                if (A == 1) { I[k] = lower ? -Expm1(Log1p(-x) * B) : Math.Exp(Log1p(-x) * B); continue; }
                if (B == 1) { I[k] = lower ? Math.Pow(x, A) : -Expm1(Math.Log(x) * A); continue; }
                bool f;
                if (lower) { f = x > A / (A + B); if (f) { x = 1 - x; (A, B) = (B, A); } }
                else { f = x < A / (A + B); if (!f) { x = 1 - x; (A, B) = (B, A); } }
                double ff = single ? (float)CfBetaInc(x, A, B) : CfBetaInc(x, A, B);
                double y = Math.Exp(A * Math.Log(x) + B * Log1p(-x) + (GammaLn(A + B) - GammaLn(A) - GammaLn(B)) + Math.Log(ff));
                I[k] = f ? 1 - y : y;
            }
            var r = new MatrixValue(1, N, I, null); r.SetDims(dims);
            if (single) r.CastInPlace(NumKind.Single);
            return new Value[] { r };
        });
    }
}
