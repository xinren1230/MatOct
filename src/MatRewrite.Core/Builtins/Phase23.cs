// MatRewrite - GPL-3.0-or-later
// 0.23: airy, BLAS/LAPACK version strings.
using System.Numerics;
using MatRewrite.Core.Runtime;
namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static readonly double SqPi = Math.Sqrt(Math.PI);

    static (Complex ai, Complex aip) AiryAsym(Complex z)
    {
        double ph = Math.Abs(z.Phase);
        if (ph < 2 * Math.PI / 3)
        {
            var zeta = 2.0 / 3 * Complex.Pow(z, 1.5); var z14 = Complex.Pow(z, 0.25);
            Complex su = 0, sv = 0, zk = 1; double u = 1, best = double.MaxValue;
            for (int k = 0; k < 40; k++)
            {
                if (k > 0) u *= (6.0 * k - 5) * (6.0 * k - 3) * (6.0 * k - 1) / ((2.0 * k - 1) * 216 * k);
                double v = k == 0 ? 1 : -(6.0 * k + 1) / (6.0 * k - 1) * u;
                var t = (k % 2 == 0 ? 1 : -1) * u / zk; double m = t.Magnitude;
                if (m > best) break; best = m;
                su += t; sv += (k % 2 == 0 ? 1 : -1) * v / zk; zk *= zeta;
                if (m < 1e-17 * su.Magnitude) break;
            }
            var e = Complex.Exp(-zeta);
            return (e / (2 * SqPi * z14) * su, -z14 * e / (2 * SqPi) * sv);
        }
        else
        {
            var w = -z; var xi = 2.0 / 3 * Complex.Pow(w, 1.5); var w14 = Complex.Pow(w, 0.25);
            Complex P = 0, Q = 0, R = 0, S = 0, xk = 1; double u = 1, best = double.MaxValue;
            for (int k = 0; k < 40; k++)
            {
                if (k > 0) u *= (6.0 * k - 5) * (6.0 * k - 3) * (6.0 * k - 1) / ((2.0 * k - 1) * 216 * k);
                double v = k == 0 ? 1 : -(6.0 * k + 1) / (6.0 * k - 1) * u;
                int sgn = (k / 2) % 2 == 0 ? 1 : -1;
                var t = sgn * u / xk; double m = t.Magnitude;
                if (m > best) break; best = m;
                if (k % 2 == 0) { P += t; R += sgn * v / xk; } else { Q += t; S += sgn * v / xk; }
                xk *= xi;
                if (m < 1e-17) break;
            }
            var a = xi - Math.PI / 4; var c = Complex.Cos(a); var s = Complex.Sin(a);
            return ((c * P + s * Q) / (SqPi * w14), w14 / SqPi * (s * R - c * S));
        }
    }

    static (Complex ai, Complex aip) AiryAi(Complex z)
    {
        double lim = z.Real > 0 ? 5 : 8;
        if (z.Magnitude > lim) return AiryAsym(z);
        const double c1 = 0.355028053887817239, c2 = 0.258819403792806798;
        var z3 = z * z * z;
        Complex f = 0, g = 0, fp = 0, gp = 0, t = 1, s = z, d = z * z / 2, e = 1;
        for (int k = 0; k < 200; k++)
        {
            f += t; g += s; gp += e; if (k > 0) fp += d;
            t *= z3 / ((3.0 * k + 2) * (3.0 * k + 3));
            s *= z3 / ((3.0 * k + 3) * (3.0 * k + 4));
            e *= z3 / ((3.0 * k + 3) * (3.0 * k + 1));
            if (k > 0) d *= z3 / (3.0 * k * (3.0 * k + 2));
            if (t.Magnitude + s.Magnitude + e.Magnitude + d.Magnitude < 1e-18 * (f.Magnitude + g.Magnitude + 1e-300)) break;
        }
        return (c1 * f - c2 * g, c1 * fp - c2 * gp);
    }

    static Complex Airy(int k, Complex z)
    {
        if (k <= 1) { var (a, ap) = AiryAi(z); return k == 0 ? a : ap; }
        double lim = z.Real > 0 ? 5 : 8;
        if (z.Magnitude <= lim || Math.Abs(z.Phase) >= Math.PI / 3)
        {
            var w1 = Complex.FromPolarCoordinates(1, 2 * Math.PI / 3); var w2 = Complex.Conjugate(w1);
            var (a1, a1p) = AiryAi(z * w1); var (a2, a2p) = AiryAi(z * w2);
            if (z.Magnitude <= lim)
            {
                // series directly is better conditioned here
                const double c1 = 0.355028053887817239, c2 = 0.258819403792806798;
                var z3 = z * z * z; Complex f = 0, g = 0, fp = 0, gp = 0, t = 1, s = z, d = z * z / 2, e = 1;
                for (int i = 0; i < 200; i++)
                {
                    f += t; g += s; gp += e; if (i > 0) fp += d;
                    t *= z3 / ((3.0 * i + 2) * (3.0 * i + 3)); s *= z3 / ((3.0 * i + 3) * (3.0 * i + 4));
                    e *= z3 / ((3.0 * i + 3) * (3.0 * i + 1)); if (i > 0) d *= z3 / (3.0 * i * (3.0 * i + 2));
                    if (t.Magnitude + s.Magnitude + e.Magnitude + d.Magnitude < 1e-18 * (f.Magnitude + g.Magnitude + 1e-300)) break;
                }
                double r3 = Math.Sqrt(3);
                return k == 2 ? r3 * (c1 * f + c2 * g) : r3 * (c1 * fp + c2 * gp);
            }
            var p6 = Complex.FromPolarCoordinates(1, Math.PI / 6); var p56 = Complex.FromPolarCoordinates(1, 5 * Math.PI / 6);
            return k == 2 ? p6 * a1 + Complex.Conjugate(p6) * a2 : p56 * a1p + Complex.Conjugate(p56) * a2p;
        }
        var zeta = 2.0 / 3 * Complex.Pow(z, 1.5); var z14 = Complex.Pow(z, 0.25);
        Complex su = 0, sv = 0, zk = 1; double u = 1, best = double.MaxValue;
        for (int j = 0; j < 40; j++)
        {
            if (j > 0) u *= (6.0 * j - 5) * (6.0 * j - 3) * (6.0 * j - 1) / ((2.0 * j - 1) * 216 * j);
            double v = j == 0 ? 1 : -(6.0 * j + 1) / (6.0 * j - 1) * u;
            var tt = u / zk; if (tt.Magnitude > best) break; best = tt.Magnitude;
            su += tt; sv += v / zk; zk *= zeta;
        }
        var ex = Complex.Exp(zeta);
        return k == 2 ? ex / (SqPi * z14) * su : z14 * ex / SqPi * sv;
    }

    static void RegisterPhase23()
    {
        Reg1("__blas_version__", a => Str("OpenBLAS-compatible managed BLAS (MatOct)"));
        Reg1("__lapack_version__", a => Str("LAPACK 3.12 (MatOct managed)"));
        Reg1("bitunpack", a =>
        {
            if (a.Length != 1 || a[0] is not MatrixValue m || m.IsComplex) throw new OctaveException("bitunpack: invalid input class");
            var bytes = new List<byte>();
            for (int i = 0; i < m.Numel; i++)
            {
                double v = m.Re[i];
                bytes.AddRange(m.Kind switch
                {
                    NumKind.Double => BitConverter.GetBytes(v),
                    NumKind.Single => BitConverter.GetBytes((float)v),
                    NumKind.Logical or NumKind.Int8 or NumKind.UInt8 => new[] { unchecked((byte)(long)v) },
                    NumKind.Char => new[] { (byte)(int)v },
                    NumKind.Int16 => BitConverter.GetBytes((short)v), NumKind.UInt16 => BitConverter.GetBytes((ushort)v),
                    NumKind.Int32 => BitConverter.GetBytes((int)v), NumKind.UInt32 => BitConverter.GetBytes((uint)v),
                    NumKind.Int64 => BitConverter.GetBytes((long)v), _ => BitConverter.GetBytes((ulong)v),
                });
            }
            var r = new double[bytes.Count * 8];
            for (int i = 0; i < bytes.Count; i++) for (int b = 0; b < 8; b++) r[i * 8 + b] = (bytes[i] >> b) & 1;
            return new MatrixValue(r.Length, 1, r, null, NumKind.Logical);
        });
        Reg1("bitpack", a =>
        {
            if (a.Length != 2 || a[0] is not MatrixValue m || m.Kind != NumKind.Logical) throw new OctaveException("bitpack: X must be a logical array");
            var cls = S(a[1], "bitpack");
            int sz = cls switch { "double" or "int64" or "uint64" => 8, "single" or "int32" or "uint32" => 4, "int16" or "uint16" => 2, "char" or "logical" or "int8" or "uint8" => 1, _ => throw new OctaveException($"bitpack: invalid data type to pack") };
            if (m.Numel % (sz * 8) != 0) throw new OctaveException("bitpack: incorrect number of bits to make up output value");
            int n = m.Numel / (sz * 8); var bytes = new byte[n * sz];
            for (int i = 0; i < bytes.Length; i++) for (int b = 0; b < 8; b++) if (m.Re[i * 8 + b] != 0) bytes[i] |= (byte)(1 << b);
            var o = new double[n];
            for (int i = 0; i < n; i++)
                o[i] = cls switch
                {
                    "double" => BitConverter.ToDouble(bytes, i * 8), "single" => BitConverter.ToSingle(bytes, i * 4),
                    "int64" => BitConverter.ToInt64(bytes, i * 8), "uint64" => BitConverter.ToUInt64(bytes, i * 8),
                    "int32" => BitConverter.ToInt32(bytes, i * 4), "uint32" => BitConverter.ToUInt32(bytes, i * 4),
                    "int16" => BitConverter.ToInt16(bytes, i * 2), "uint16" => BitConverter.ToUInt16(bytes, i * 2),
                    "int8" => (sbyte)bytes[i], _ => bytes[i],
                };
            var k = NumTypes.FromName(cls) ?? NumKind.Double;
            return new MatrixValue(n, 1, o, null, k);
        });
        Reg("airy", (it, a, n) =>
        {
            if (a.Length < 1 || a.Length > 3) throw new OctaveException("Invalid call to airy");
            int k = 0; MatrixValue zm;
            if (a.Length == 1) zm = M(a[0], "airy");
            else { k = (int)D(a[0], "airy"); zm = M(a[1], "airy"); if (k < 0 || k > 3) throw new OctaveException("airy: K must be an integer value"); }
            bool scaled = a.Length == 3 && ((MatrixValue)a[2]).Numel > 0 && ((MatrixValue)a[2]).Re[0] != 0;
            var r = new MatrixValue(zm.R, zm.C, new double[zm.Numel], new double[zm.Numel]) { ND = (int[]?)zm.ND?.Clone() };
            for (int i = 0; i < zm.Numel; i++)
            {
                var z = new Complex(zm.Re[i], zm.Im?[i] ?? 0);
                var v = Airy(k, z);
                if (zm.Im == null && zm.Re[i] >= 0) v = new Complex(v.Real, 0);
                if (zm.Im == null && zm.Re[i] < 0) v = new Complex(v.Real, 0);
                if (scaled)
                {
                    var zeta = 2.0 / 3 * Complex.Pow(z, 1.5);
                    v *= k <= 1 ? Complex.Exp(zeta) : Complex.Exp(-Math.Abs(zeta.Real));
                }
                r.Re[i] = v.Real; r.Im![i] = v.Imaginary;
            }
            if (r.Im!.All(x => x == 0)) r.Im = null;
            var ierr = new MatrixValue(zm.R, zm.C, new double[zm.Numel], null);
            return n >= 2 ? new Value[] { r, ierr } : new Value[] { r };
        });
    }
}
