using System;
using System.Numerics;
namespace MatRewrite.Core.Runtime;

/// <summary>Exact saturating int64/uint64 arithmetic (MATLAB semantics: round-to-nearest for division, saturation).</summary>
static class Int64Ops
{
    /// <summary>Exact rational value num/2^sh of element k (doubles decomposed exactly).</summary>
    internal static bool GetQ(MatrixValue m, int k, out BigInteger num, out int sh)
    {
        sh = 0;
        if (m.Kind == NumKind.Int64) { num = m.I64(k); return true; }
        if (m.Kind == NumKind.UInt64) { num = m.U64(k); return true; }
        double d = m.Re[k]; num = 0;
        if (double.IsNaN(d) || double.IsInfinity(d)) return false;
        if (d == Math.Floor(d)) { num = new BigInteger(d); return true; }
        long bits = BitConverter.DoubleToInt64Bits(d); int exp = (int)((bits >> 52) & 0x7FF); long man = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0) exp = 1; else man |= 1L << 52;
        int e = exp - 1075; // d = man * 2^e, e < 0 here
        num = d < 0 ? -new BigInteger(man) : new BigInteger(man); sh = -e;
        return true;
    }
    internal static BigInteger RoundQ(BigInteger num, int sh) => sh <= 0 ? num << -sh : DivRound(num, BigInteger.One << sh);
    static bool Get(MatrixValue m, int k, NumKind kind, out BigInteger v)
    {
        if (m.Kind == NumKind.Int64) { v = m.I64(k); return true; }
        if (m.Kind == NumKind.UInt64) { v = m.U64(k); return true; }
        double d = m.Re[k];
        if (double.IsNaN(d) || double.IsInfinity(d) || d != Math.Floor(d)) { v = 0; return false; }
        v = new BigInteger(d); return true;
    }
    internal static long Sat(BigInteger x, NumKind k)
    {
        if (k == NumKind.UInt64) { if (x < 0) return 0; if (x > ulong.MaxValue) return unchecked((long)ulong.MaxValue); return unchecked((long)(ulong)x); }
        if (x > long.MaxValue) return long.MaxValue; if (x < long.MinValue) return long.MinValue; return (long)x;
    }
    internal static BigInteger DivRound(BigInteger a, BigInteger b)
    {
        var q = BigInteger.DivRem(a, b, out var r);
        if (BigInteger.Abs(r) * 2 >= BigInteger.Abs(b)) q += (a.Sign * b.Sign);
        return q;
    }
    internal static bool Bcast(MatrixValue a, MatrixValue b, out int r, out int c)
    {
        r = c = 0;
        if (a.ND != null || b.ND != null || a.IsComplex || b.IsComplex) return false;
        if (a.IsScalar) { r = b.R; c = b.C; return true; }
        if (b.IsScalar || (a.R == b.R && a.C == b.C)) { r = a.R; c = a.C; return true; }
        return false;
    }
    public static MatrixValue? TryArith(string op, MatrixValue a, MatrixValue b, NumKind k)
    {
        if (op is "*" or "/") { if (!(a.IsScalar || b.IsScalar)) return null; if (op == "/" && !b.IsScalar) return null; op = op == "*" ? ".*" : "./"; }
        if (op is not ("+" or "-" or ".*" or "./" or ".\\" or ".^" or "^")) return null;
        if (op == "^") { if (!(a.IsScalar && b.IsScalar)) return null; op = ".^"; }
        if (!Bcast(a, b, out int r, out int c)) return null;
        int n = r * c; var res = new long[n];
        for (int i = 0; i < n; i++)
        {
            if (!GetQ(a, a.IsScalar ? 0 : i, out var x, out int xs) || !GetQ(b, b.IsScalar ? 0 : i, out var y, out int ys)) return null;
            BigInteger z;
            switch (op)
            {
                case "+": case "-":
                    { int s2 = Math.Max(xs, ys); var X = x << (s2 - xs); var Y = y << (s2 - ys); z = RoundQ(op == "+" ? X + Y : X - Y, s2); break; }
                case ".*": z = RoundQ(x * y, xs + ys); break;
                case "./": case ".\\":
                    if (op == ".\\") { (x, y) = (y, x); (xs, ys) = (ys, xs); }
                    if (y.IsZero) { z = x.Sign > 0 ? new BigInteger(ulong.MaxValue) * 2 : x.Sign < 0 ? -new BigInteger(ulong.MaxValue) * 2 : 0; break; }
                    { var N = x << ys; var D = y << xs; if (D.Sign < 0) { N = -N; D = -D; } z = DivRound(N, D); break; }
                case ".^":
                    if (xs != 0 || ys != 0) return null;
                    if (y.Sign < 0 || y > 4096) return null;
                    z = BigInteger.Abs(x) > 1 && y > 128 ? (x.Sign < 0 && !y.IsEven ? -BigInteger.Pow(2, 130) : BigInteger.Pow(2, 130)) : BigInteger.Pow(x, (int)y); break;
                default: return null;
            }
            res[i] = Sat(z, k);
        }
        return MatrixValue.FromI64(r, c, res, k);
    }
    public static MatrixValue? TryCompare(string op, MatrixValue a, MatrixValue b)
    {
        if (op is not ("==" or "!=" or "<" or "<=" or ">" or ">=")) return null;
        if (!Bcast(a, b, out int r, out int c)) return null;
        var m = new MatrixValue(r, c, NumKind.Logical);
        for (int i = 0; i < r * c; i++)
        {
            if (!Get(a, a.IsScalar ? 0 : i, a.Kind, out var x) || !Get(b, b.IsScalar ? 0 : i, b.Kind, out var y)) return null;
            int s = x.CompareTo(y);
            m.Re[i] = (op switch { "==" => s == 0, "!=" => s != 0, "<" => s < 0, "<=" => s <= 0, ">" => s > 0, _ => s >= 0 }) ? 1 : 0;
        }
        return m;
    }
}
