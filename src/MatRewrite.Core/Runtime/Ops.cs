// MatRewrite - GPL-3.0-or-later
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace MatRewrite.Core.Runtime;

/// <summary>Arithmetic, comparison and logical operators with Octave broadcasting rules.</summary>
public static class Ops
{
    public static MatrixValue ToMatrix(Value v, string op)
    {
        if (v is MatrixValue m) return m;
        if (v is Sparse.SparseValue sp) return sp.ToDense();
        if (v is CellValue) throw new OctaveException($"binary operator '{op}' not implemented for 'cell' by 'double' operations");
        if (v is StructValue) throw new OctaveException($"binary operator '{op}' not implemented for 'struct' operations");
        if (v is FunctionHandleValue) throw new OctaveException($"binary operator '{op}' not implemented for 'function handle' operations");
        throw new OctaveException($"invalid operand for '{op}'");
    }

    static (int r, int c) BroadcastDims(MatrixValue a, MatrixValue b, string op)
    {
        int r = a.R == b.R ? a.R : a.R == 1 ? b.R : b.R == 1 ? a.R : -1;
        int c = a.C == b.C ? a.C : a.C == 1 ? b.C : b.C == 1 ? a.C : -1;
        if (r < 0 || c < 0)
            throw new OctaveException($"operator {op}: nonconformant arguments (op1 is {a.R}x{a.C}, op2 is {b.R}x{b.C})");
        return (r, c);
    }

    static Complex CMulMixed(Complex x, Complex y)
        => y.Imaginary == 0 ? new Complex(x.Real * y.Real, x.Imaginary * y.Real)
         : x.Imaginary == 0 ? new Complex(x.Real * y.Real, x.Real * y.Imaginary) : x * y;

    public static MatrixValue Elementwise(MatrixValue a, MatrixValue b, string op,
        Func<double, double, double> fr, Func<Complex, Complex, Complex>? fc, NumKind resultKind = NumKind.Double,
        Func<double, double, bool>? needComplex = null)
    {
        if (a.ND != null || b.ND != null) return ElementwiseND(a, b, op, fr, fc, resultKind, needComplex);
        var (r, c) = BroadcastDims(a, b, op);
        bool cplx = (a.IsComplex || b.IsComplex) && fc != null;
        int n = r * c;
        bool aS = a.R == r && a.C == c, bS = b.R == r && b.C == c;
        if (!cplx && needComplex != null && fc != null)
        {
            for (int k = 0; k < n && !cplx; k++)
            {
                int i = k % Math.Max(r, 1), j = k / Math.Max(r, 1);
                double x = a.Re[aS ? k : (a.R == 1 ? 0 : i) + (a.C == 1 ? 0 : j) * a.R];
                double y = b.Re[bS ? k : (b.R == 1 ? 0 : i) + (b.C == 1 ? 0 : j) * b.R];
                if (needComplex(x, y)) cplx = true;
            }
        }
        var res = new MatrixValue(r, c, resultKind, cplx);
        for (int j = 0; j < c; j++)
        {
            int ja = a.C == 1 ? 0 : j, jb = b.C == 1 ? 0 : j;
            for (int i = 0; i < r; i++)
            {
                int ka = (a.R == 1 ? 0 : i) + ja * a.R;
                int kb = (b.R == 1 ? 0 : i) + jb * b.R;
                int k = i + j * r;
                if (cplx)
                {
                    var z = fc!(a.At(ka), b.At(kb));
                    res.Re[k] = z.Real; res.Im![k] = z.Imaginary;
                }
                else res.Re[k] = fr(a.Re[ka], b.Re[kb]);
            }
        }
        return res.Narrow();
    }

    static MatrixValue ElementwiseND(MatrixValue a, MatrixValue b, string op,
        Func<double, double, double> fr, Func<Complex, Complex, Complex>? fc, NumKind resultKind, Func<double, double, bool>? needComplex)
    {
        int n = Math.Max(a.NDims, b.NDims);
        var da = NDArray.Pad(a.Dims, n); var db = NDArray.Pad(b.Dims, n);
        var d = new int[n];
        for (int k = 0; k < n; k++)
        {
            if (da[k] == db[k] || db[k] == 1) d[k] = da[k];
            else if (da[k] == 1) d[k] = db[k];
            else throw new OctaveException($"operator {op}: nonconformant arguments (op1 is {string.Join("x", da)}, op2 is {string.Join("x", db)})");
        }
        int total = NDArray.Prod(d);
        var ia = new int[total]; var ib = new int[total];
        if (total > 0)
        {
            var sub = new int[n]; var sa = new int[n]; var sb = new int[n]; int t = 0;
            do
            {
                for (int k = 0; k < n; k++) { sa[k] = da[k] == 1 ? 0 : sub[k]; sb[k] = db[k] == 1 ? 0 : sub[k]; }
                ia[t] = NDArray.Linear(sa, da); ib[t] = NDArray.Linear(sb, db); t++;
            } while (NDArray.Next(sub, d));
        }
        bool cplx = (a.IsComplex || b.IsComplex) && fc != null;
        if (!cplx && needComplex != null && fc != null)
            for (int k = 0; k < total && !cplx; k++) if (needComplex(a.Re[ia[k]], b.Re[ib[k]])) cplx = true;
        var res = new MatrixValue(1, total, resultKind, cplx);
        for (int k = 0; k < total; k++)
        {
            if (cplx) { var z = fc!(a.At(ia[k]), b.At(ib[k])); res.Re[k] = z.Real; res.Im![k] = z.Imaginary; }
            else res.Re[k] = fr(a.Re[ia[k]], b.Re[ib[k]]);
        }
        res.SetDims(d);
        return res.Narrow();
    }

    public static MatrixValue Map(MatrixValue a, Func<double, double> fr, Func<Complex, Complex>? fc = null,
        Func<double, bool>? needComplex = null, NumKind kind = NumKind.Double)
    {
        bool cplx = a.IsComplex || (needComplex != null && fc != null && a.Re.Any(needComplex));
        var res = new MatrixValue(a.R, a.C, kind, cplx && fc != null) { ND = a.ND };
        for (int k = 0; k < a.Numel; k++)
        {
            if (cplx && fc != null)
            {
                var z = fc(a.At(k)); res.Re[k] = z.Real; res.Im![k] = z.Imaginary;
            }
            else res.Re[k] = fr(a.Re[k]);
        }
        return res.Narrow();
    }

    static double B(bool b) => b ? 1 : 0;
    static bool IsInt(double x) => x == Math.Floor(x);

    static string OpClass(MatrixValue m) =>
        m.IsInteger || m.IsSingle ? m.ClassName + (m.IsScalar ? " scalar" : " matrix")
        : m.IsChar ? "char matrix" : m.IsLogical ? "bool matrix" : m.IsComplex ? "complex matrix" : m.IsScalar ? "double" : "matrix";

    /// <summary>Result class of an arithmetic operation (Octave rules: int op double -&gt; int, single wins over double).</summary>
    public static NumKind ArithKind(MatrixValue a, MatrixValue b, string op)
    {
        bool ai = a.IsInteger, bi = b.IsInteger;
        if (ai && bi && a.Kind != b.Kind)
            throw new OctaveException($"binary operator '{op}' not implemented for '{OpClass(a)}' by '{OpClass(b)}' operations");
        if ((ai && b.IsComplex) || (bi && a.IsComplex))
            throw new OctaveException($"binary operator '{op}' not implemented for '{OpClass(a)}' by '{OpClass(b)}' operations");
        if (ai) return a.Kind;
        if (bi) return b.Kind;
        if (a.IsSingle || b.IsSingle) return NumKind.Single;
        return NumKind.Double;
    }

    public static Value Binary(string op, Value av, Value bv)
    {
        if (av is Sparse.SparseValue || bv is Sparse.SparseValue)
            return Sparse.SparseOps.Binary(op, av, bv, BinaryCore);
        var res = BinaryCore(op, av, bv);
        return res;
    }

    static Value BinaryCore(string op, Value av, Value bv)
    {
        var a = ToMatrix(av, op); var b = ToMatrix(bv, op);
        if (op is "+" or "-" or ".*" or "./" or ".\\" or ".^" or "*" or "/" or "\\" or "^")
        {
            var k = ArithKind(a, b, op);
            if (k != NumKind.Double)
            {
                if (NumTypes.IsInt(k) && op is "*" or "/" or "\\" or "^" && !(a.IsScalar || b.IsScalar))
                    throw new OctaveException($"binary operator '{op}' not implemented for '{OpClass(a)}' by '{OpClass(b)}' operations");
                var r = (MatrixValue)BinaryDouble(op, a, b);
                return r.CastInPlace(k);
            }
        }
        return BinaryDouble(op, a, b);
    }


    static Value BinaryDouble(string op, MatrixValue a, MatrixValue b)
    {
        switch (op)
        {
            case "+": return Elementwise(a, b, op, (x, y) => x + y, (x, y) => x + y);
            case "-": return Elementwise(a, b, op, (x, y) => x - y, (x, y) => x - y);
            case ".*": return Elementwise(a, b, op, (x, y) => x * y, CMulMixed);
            case "./": return Elementwise(a, b, op, (x, y) => x / y, (x, y) => y.Imaginary == 0 ? new Complex(x.Real / y.Real, x.Imaginary / y.Real) : CDiv(x, y));
            case ".\\": return Elementwise(b, a, op, (x, y) => x / y, CDiv);
            case ".^": return Power(a, b);
            case "*": if ((a.IsComplex || b.IsComplex) && (a.IsScalar || b.IsScalar)) return BinaryDouble(".*", a, b); return MTimes(a, b);
            case "/": if ((a.IsComplex || b.IsComplex) && b.IsScalar) return BinaryDouble("./", a, b); return MRDivide(a, b);
            case "\\": return MLDivide(a, b);
            case "^": return MPower(a, b);
            case "==": return Elementwise(a, b, op, (x, y) => B(x == y), (x, y) => x == y ? 1 : 0, NumKind.Logical);
            case "!=": return Elementwise(a, b, op, (x, y) => B(x != y), (x, y) => x != y ? 1 : 0, NumKind.Logical);
            case "<": return Elementwise(a, b, op, (x, y) => B(x < y), (x, y) => x.Real < y.Real ? 1 : 0, NumKind.Logical);
            case "<=": return Elementwise(a, b, op, (x, y) => B(x <= y), (x, y) => x.Real <= y.Real ? 1 : 0, NumKind.Logical);
            case ">": return Elementwise(a, b, op, (x, y) => B(x > y), (x, y) => x.Real > y.Real ? 1 : 0, NumKind.Logical);
            case ">=": return Elementwise(a, b, op, (x, y) => B(x >= y), (x, y) => x.Real >= y.Real ? 1 : 0, NumKind.Logical);
            case "&": return Elementwise(a, b, op, (x, y) => B(x != 0 && y != 0), (x, y) => x != 0 && y != 0 ? 1 : 0, NumKind.Logical);
            case "|": return Elementwise(a, b, op, (x, y) => B(x != 0 || y != 0), (x, y) => x != 0 || y != 0 ? 1 : 0, NumKind.Logical);
        }
        throw new OctaveException($"unknown operator '{op}'");
    }

    static Complex CDiv(Complex x, Complex y)
    {
        if (y == Complex.Zero)
        {
            if (x == Complex.Zero) return new Complex(double.NaN, 0);
            return new Complex(x.Real == 0 ? 0 : x.Real / 0.0, x.Imaginary == 0 ? 0 : x.Imaginary / 0.0);
        }
        return x / y;
    }

    public static MatrixValue Power(MatrixValue a, MatrixValue b) =>
        Elementwise(a, b, ".^", Math.Pow, CPow, NumKind.Double, (x, y) => x < 0 && !IsInt(y));

    public static Complex CPow(Complex x, Complex y)
    {
        if (y.Imaginary == 0 && IsInt(y.Real) && Math.Abs(y.Real) <= 1024)
        {
            int n = (int)y.Real;
            Complex r = Complex.One, bse = x;
            int m = Math.Abs(n);
            while (m > 0) { if ((m & 1) != 0) r *= bse; bse *= bse; m >>= 1; }
            return n < 0 ? Complex.One / r : r;
        }
        if (x == Complex.Zero) return y.Real > 0 ? Complex.Zero : new Complex(double.PositiveInfinity, 0);
        return Complex.Pow(x, y);
    }

    public static Value Unary(string op, Value v)
    {
        if (v is Sparse.SparseValue sv) return Sparse.SparseOps.Unary(op, sv, Unary);
        var a = ToMatrix(v, op);
        switch (op)
        {
            case "-":
            {
                var r = Map(a, x => -x, z => -z);
                return a.IsInteger || a.IsSingle ? r.CastInPlace(a.Kind) : r;
            }
            case "+": return a.IsChar || a.IsLogical ? a.AsDouble() : a;
            case "!":
                if (a.IsComplex) throw new OctaveException("logical operation on complex value");
                return Map(a, x => { if (double.IsNaN(x)) throw new OctaveException("logical: NaN can't be converted to logical value"); return x == 0 ? 1 : 0; }, null, null, NumKind.Logical);
        }
        throw new OctaveException($"unknown unary operator '{op}'");
    }

    public static ArrayValue Transpose(ArrayValue a, bool conj)
    {
        if (a.ND != null) throw new OctaveException("transpose not defined for N-D objects");
        var lin = new int[a.Numel];
        for (int i = 0; i < a.R; i++)
            for (int j = 0; j < a.C; j++)
                lin[j + i * a.C] = i + j * a.R;
        var t = a.Gather(lin, a.C, a.R);
        if (conj && t is MatrixValue m && m.Im != null)
            for (int k = 0; k < m.Im.Length; k++) m.Im[k] = -m.Im[k];
        return t;
    }

    // ---------- linear algebra via MathNet ----------
    public static Matrix<double> ToMathNet(MatrixValue a) => Matrix<double>.Build.Dense(a.R, a.C, (double[])a.Re.Clone());
    public static Matrix<Complex> ToMathNetC(MatrixValue a)
    {
        var arr = new Complex[a.Numel];
        for (int k = 0; k < arr.Length; k++) arr[k] = a.At(k);
        return Matrix<Complex>.Build.Dense(a.R, a.C, arr);
    }
    public static MatrixValue FromMathNet(Matrix<double> m) => new(m.RowCount, m.ColumnCount, m.ToColumnMajorArray());
    public static MatrixValue FromMathNet(Matrix<Complex> m)
    {
        var arr = m.ToColumnMajorArray();
        return new MatrixValue(m.RowCount, m.ColumnCount, arr.Select(z => z.Real).ToArray(), arr.Select(z => z.Imaginary).ToArray()).Narrow();
    }

    public static MatrixValue MTimes(MatrixValue a, MatrixValue b)
    {
        if (a.IsScalar || b.IsScalar) return Elementwise(a, b, "*", (x, y) => x * y, (x, y) => x * y);
        if (a.C != b.R) throw new OctaveException($"operator *: nonconformant arguments (op1 is {a.R}x{a.C}, op2 is {b.R}x{b.C})");
        if (a.IsComplex || b.IsComplex) return FromMathNet(ToMathNetC(a) * ToMathNetC(b));
        if (a.R * a.C * b.C < 4096)
        {
            var r = new MatrixValue(a.R, b.C);
            for (int j = 0; j < b.C; j++)
                for (int k = 0; k < a.C; k++)
                {
                    double bkj = b.Re[k + j * b.R];
                    if (bkj == 0) continue;
                    for (int i = 0; i < a.R; i++) r.Re[i + j * a.R] += a.Re[i + k * a.R] * bkj;
                }
            return r;
        }
        return FromMathNet(ToMathNet(a) * ToMathNet(b));
    }

    public static Action<string>? Warn;

    public static MatrixValue MLDivide(MatrixValue a, MatrixValue b)
    {
        if (a.IsScalar) return Elementwise(b, a, "\\", (x, y) => x / y, CDiv);
        if (a.R != b.R) throw new OctaveException($"operator \\: nonconformant arguments (op1 is {a.R}x{a.C}, op2 is {b.R}x{b.C})");
        if (a.IsComplex || b.IsComplex)
        {
            var A = ToMathNetC(a); var Bm = ToMathNetC(b);
            return FromMathNet(a.R == a.C ? A.LU().Solve(Bm) : A.QR().Solve(Bm));
        }
        var Ar = ToMathNet(a); var Br = ToMathNet(b);
        if (a.R == a.C)
        {
            var lu = Ar.LU();
            double rc = RCond(Ar);
            if (rc < 1e-16 || double.IsNaN(rc)) Warn?.Invoke("matrix singular to machine precision");
            return FromMathNet(lu.Solve(Br));
        }
        if (a.R > a.C) return FromMathNet(Ar.QR().Solve(Br));
        return FromMathNet(Ar.Svd().Solve(Br));
    }

    static double RCond(Matrix<double> A)
    {
        try
        {
            var s = A.Svd(false).S;
            double mx = s.Maximum(), mn = s.Minimum();
            return mx == 0 ? 0 : mn / mx;
        }
        catch { return 0; }
    }

    public static MatrixValue MRDivide(MatrixValue a, MatrixValue b)
    {
        if (b.IsScalar) return Elementwise(a, b, "/", (x, y) => x / y, CDiv);
        // a / b = (b' \ a')'
        var r = MLDivide((MatrixValue)Transpose(b, false), (MatrixValue)Transpose(a, false));
        return (MatrixValue)Transpose(r, false);
    }

    public static MatrixValue MPower(MatrixValue a, MatrixValue b)
    {
        if (a.IsScalar && b.IsScalar) return Power(a, b);
        if (!b.IsScalar) throw new OctaveException("operator ^: exponent must be a scalar for matrix power");
        if (a.R != a.C) throw new OctaveException("for x^y, only square matrix arguments are permitted and one argument must be scalar.  Use .^ for elementwise power.");
        double p = b.Re[0];
        if (b.IsComplex || !IsInt(p)) throw new OctaveException("matrix power with non-integer exponent is not implemented yet");
        int n = (int)Math.Abs(p);
        var result = Identity(a.R);
        var bse = a;
        while (n > 0)
        {
            if ((n & 1) != 0) result = MTimes(result, bse);
            bse = MTimes(bse, bse);
            n >>= 1;
        }
        if (p < 0) result = Inv(result);
        return result;
    }

    public static MatrixValue Identity(int n, int m = -1)
    {
        if (m < 0) m = n;
        var r = new MatrixValue(n, m);
        for (int i = 0; i < Math.Min(n, m); i++) r.Re[i + i * n] = 1;
        return r;
    }

    public static MatrixValue Inv(MatrixValue a)
    {
        if (a.R != a.C) throw new OctaveException("inverse: argument must be a square matrix");
        if (a.Numel == 0) return a;
        if (a.IsComplex) return FromMathNet(ToMathNetC(a).Inverse());
        var A = ToMathNet(a);
        double rc = RCond(A);
        if (rc < 1e-16 || double.IsNaN(rc))
        {
            Warn?.Invoke("matrix singular to machine precision");
            var r = new MatrixValue(a.R, a.C);
            Array.Fill(r.Re, double.PositiveInfinity);
            return r;
        }
        return FromMathNet(A.Inverse());
    }

    public static bool IsTrue(Value v)
    {
        if (v is Sparse.SparseValue sv) v = sv.ToDense();
        if (v is MatrixValue m)
        {
            if (m.Numel == 0) return false;
            for (int k = 0; k < m.Numel; k++)
            {
                if (double.IsNaN(m.Re[k])) throw new OctaveException("if: conditional expression cannot contain NaN");
                if (m.Re[k] == 0 && (m.Im == null || m.Im[k] == 0)) return false;
            }
            return true;
        }
        if (v is CellValue) throw new OctaveException("wrong type argument 'cell array'");
        if (v is StructValue) throw new OctaveException("wrong type argument 'struct'");
        return true;
    }
}
