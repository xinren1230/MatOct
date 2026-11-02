using System;
using System.Numerics;
namespace MatRewrite.Core.Runtime;

/// <summary>Exact saturating int64/uint64 arithmetic (MATLAB semantics: round-to-nearest for division, saturation).</summary>
static class Int64Ops
{
    static bool Get(MatrixValue m, int k, NumKind kind, out BigInteger v)
    {
        if (m.Kind == NumKind.Int64) { v = m.I64(k); return true; }
        if (m.Kind == NumKind.UInt64) { v = m.U64(k); return true; }
        double d = m.Re[k];
        if (double.IsNaN(d) || double.IsInfinity(d) || d != Math.Floor(d)) { v = 0; return false; }
        v = new BigInteger(d); return true;
    }
    static long Sat(BigInteger x, NumKind k)
    {
        if (k == NumKind.UInt64) { if (x < 0) return 0; if (x > ulong.MaxValue) return unchecked((long)ulong.MaxValue); return unchecked((long)(ulong)x); }
        if (x > long.MaxValue) return long.MaxValue; if (x < long.MinValue) return long.MinValue; return (long)x;
    }
    static BigInteger DivRound(BigInteger a, BigInteger b)
    {
        var q = BigInteger.DivRem(a, b, out var r);
        if (BigInteger.Abs(r) * 2 >= BigInteger.Abs(b)) q += (a.Sign * b.Sign);
        return q;
    }
    static bool Bcast(MatrixValue a, MatrixValue b, out int r, out int c)
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
            if (!Get(a, a.IsScalar ? 0 : i, k, out var x) || !Get(b, b.IsScalar ? 0 : i, k, out var y)) return null;
            BigInteger z;
            switch (op)
            {
                case "+": z = x + y; break;
                case "-": z = x - y; break;
                case ".*": z = x * y; break;
                case "./": case ".\\":
                    if (op == ".\\") (x, y) = (y, x);
                    if (y.IsZero) { z = x.Sign > 0 ? new BigInteger(ulong.MaxValue) * 2 : x.Sign < 0 ? -new BigInteger(ulong.MaxValue) * 2 : 0; break; }
                    z = DivRound(x, y); break;
                case ".^":
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
