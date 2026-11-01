// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;
using CSparse;
using CSparse.Double.Factorization;
using CSparse.Factorization;

namespace MatRewrite.Core.Sparse;

/// <summary>Arithmetic on sparse matrices. Fast CSC paths for the common operators; everything
/// else goes through the dense kernels and is re-sparsified following Octave's result rules.</summary>
public static class SparseOps
{
    public static MatrixValue Dense(Value v) => v is SparseValue s ? s.ToDense() : (MatrixValue)v;
    static bool IsScalarNum(Value v) => v is MatrixValue m && m.Numel == 1;

    public static Value Binary(string op, Value av, Value bv, Func<string, Value, Value, Value> dense)
    {
        var sa = av as SparseValue; var sb = bv as SparseValue;
        bool cplx = (sa?.IsComplex ?? false) || (sb?.IsComplex ?? false) || (av is MatrixValue ma && ma.IsComplex) || (bv is MatrixValue mb && mb.IsComplex);
        if (!cplx)
        {
            if (sa != null && sb != null && SameSize(sa, sb))
            {
                switch (op)
                {
                    case "+": return Combine(sa, sb, 1, 1, false);
                    case "-": return Combine(sa, sb, 1, -1, false);
                    case ".*": return Combine(sa, sb, 1, 1, true);
                }
            }
            if ((op is "*" or ".*") && sa != null && IsScalarNum(bv) && double.IsFinite(((MatrixValue)bv).Re[0])) return Scale(sa, ((MatrixValue)bv).Re[0]);
            if ((op is "*" or ".*") && sb != null && IsScalarNum(av) && double.IsFinite(((MatrixValue)av).Re[0])) return Scale(sb, ((MatrixValue)av).Re[0]);
            if ((op is "/" or "./") && sa != null && IsScalarNum(bv) && ((MatrixValue)bv).Re[0] != 0 && double.IsFinite(((MatrixValue)bv).Re[0])) return Scale(sa, 1 / ((MatrixValue)bv).Re[0]);
            if (op == "*" && sa != null && sb != null && sa.C == sb.R) return MulSS(sa, sb);
            if (op == "*" && sa != null && bv is MatrixValue db && db.ND == null && sa.C == db.R) return MulSD(sa, db);
            if (op == "\\" && sa != null && sa.R == (bv is SparseValue x ? x.R : ((MatrixValue)bv).R) && !IsScalarNum(av))
                return Solve(sa, bv);
        }
        var res = dense(op, Dense(av), Dense(bv));
        if (res is not MatrixValue rm || rm.ND != null) return res;
        bool both = sa != null && sb != null;
        bool keep = op switch
        {
            "+" or "-" => both,
            ".*" or "&" => true,
            "*" => both || IsScalarNum(av) || IsScalarNum(bv),
            "./" or "/" => sa != null && IsScalarNum(bv),
            "\\" => both,
            "==" or "~=" or "!=" or "<" or "<=" or ">" or ">=" or "|" => true,
            ".^" => sa != null && IsScalarNum(bv) && ((MatrixValue)bv).Re[0] > 0,
            _ => both,
        };
        if (!keep) return res;
        return SparseValue.FromDense(rm);
    }

    static bool SameSize(SparseValue a, SparseValue b) => a.R == b.R && a.C == b.C;

    static SparseValue Scale(SparseValue a, double k)
        => new SparseValue(a.R, a.C, (int[])a.Cp.Clone(), a.Ri[..a.Nnz], a.Re.Take(a.Nnz).Select(x => x * k).ToArray()).Prune();

    /// <summary>alpha*A + beta*B, or A.*B when mult.</summary>
    static SparseValue Combine(SparseValue a, SparseValue b, double alpha, double beta, bool mult)
    {
        var cp = new int[a.C + 1];
        var ri = new List<int>(a.Nnz + b.Nnz); var re = new List<double>(a.Nnz + b.Nnz);
        for (int j = 0; j < a.C; j++)
        {
            int p = a.Cp[j], pe = a.Cp[j + 1], q = b.Cp[j], qe = b.Cp[j + 1];
            while (p < pe || q < qe)
            {
                int ia = p < pe ? a.Ri[p] : int.MaxValue, ib = q < qe ? b.Ri[q] : int.MaxValue;
                double v; int i;
                if (ia == ib) { i = ia; v = mult ? a.Re[p] * b.Re[q] : alpha * a.Re[p] + beta * b.Re[q]; p++; q++; }
                else if (ia < ib) { i = ia; v = mult ? a.Re[p] * 0 : alpha * a.Re[p]; p++; }
                else { i = ib; v = mult ? 0 * b.Re[q] : beta * b.Re[q]; q++; }
                if (v != 0) { ri.Add(i); re.Add(v); }
            }
            cp[j + 1] = ri.Count;
        }
        return new SparseValue(a.R, a.C, cp, ri.ToArray(), re.ToArray());
    }

    static SparseValue MulSS(SparseValue a, SparseValue b)
    {
        var cp = new int[b.C + 1];
        var ri = new List<int>(); var re = new List<double>();
        var acc = new double[a.R]; var mark = new int[a.R]; Array.Fill(mark, -1);
        var rows = new List<int>();
        for (int j = 0; j < b.C; j++)
        {
            rows.Clear();
            for (int q = b.Cp[j]; q < b.Cp[j + 1]; q++)
            {
                int k = b.Ri[q]; double bv = b.Re[q];
                for (int p = a.Cp[k]; p < a.Cp[k + 1]; p++)
                {
                    int i = a.Ri[p];
                    if (mark[i] != j) { mark[i] = j; acc[i] = 0; rows.Add(i); }
                    acc[i] += a.Re[p] * bv;
                }
            }
            rows.Sort();
            foreach (var i in rows) if (acc[i] != 0) { ri.Add(i); re.Add(acc[i]); }
            cp[j + 1] = ri.Count;
        }
        return new SparseValue(a.R, b.C, cp, ri.ToArray(), re.ToArray());
    }

    static MatrixValue MulSD(SparseValue a, MatrixValue b)
    {
        var r = new MatrixValue(a.R, b.C);
        for (int c = 0; c < b.C; c++)
            for (int k = 0; k < a.C; k++)
            {
                double bv = b.Re[k + c * b.R];
                if (bv == 0) continue;
                for (int p = a.Cp[k]; p < a.Cp[k + 1]; p++) r.Re[a.Ri[p] + c * a.R] += a.Re[p] * bv;
            }
        return r;
    }

    public static Value Solve(SparseValue A, Value bv)
    {
        bool bSparse = bv is SparseValue;
        var B = Dense(bv);
        if (B.IsComplex) return Ops.Binary("\\", A.ToDense(), B);
        var cs = A.ToCSparse();
        var X = new MatrixValue(A.C, B.C);
        var x = new double[A.C];
        try
        {
            if (A.R == A.C)
            {
                ISolver<double> solver;
                if (IsSymmetricPosDiag(A))
                {
                    try { solver = SparseCholesky.Create(cs, ColumnOrdering.MinimumDegreeAtPlusA); }
                    catch (Exception) { solver = SparseLU.Create(cs, ColumnOrdering.MinimumDegreeAtPlusA, 1.0); }
                }
                else solver = SparseLU.Create(cs, ColumnOrdering.MinimumDegreeAtPlusA, 1.0);
                SolveCols(solver, B, X, A.C);
            }
            else
            {
                if (A.R < A.C) return Ops.Binary("\\", A.ToDense(), B);
                var qr = SparseQR.Create(cs, ColumnOrdering.MinimumDegreeAtA);
                SolveCols(qr, B, X, A.C);
            }
        }
        catch (Exception e) when (e is not OctaveException)
        {
            Warn("warning: matrix singular to machine precision");
            var d = (MatrixValue)Ops.Binary("\\", A.ToDense(), B);
            return bSparse ? SparseValue.FromDense(d) : d;
        }
        if (X.Re.Any(v => double.IsNaN(v) || double.IsInfinity(v))) Warn("warning: matrix singular to machine precision");
        return bSparse ? SparseValue.FromDense(X) : X;
    }

    public static Action<string>? WarningSink;
    static void Warn(string s) => WarningSink?.Invoke(s);

    static void SolveCols(ISolver<double> solver, MatrixValue B, MatrixValue X, int n)
    {
        var b = new double[B.R]; var x = new double[n];
        for (int c = 0; c < B.C; c++)
        {
            Array.Copy(B.Re, c * B.R, b, 0, B.R);
            Array.Clear(x);
            solver.Solve(b, x);
            Array.Copy(x, 0, X.Re, c * n, n);
        }
    }

    static bool IsSymmetricPosDiag(SparseValue A)
    {
        for (int j = 0; j < A.C; j++)
        {
            if (!(A.Get(j, j).Real > 0)) return false;
            for (int p = A.Cp[j]; p < A.Cp[j + 1]; p++)
                if (A.Get(j, A.Ri[p]).Real != A.Re[p]) return false;
        }
        return true;
    }

    public static Value Unary(string op, SparseValue s, Func<string, Value, Value> dense)
    {
        switch (op)
        {
            case "-":
                return new SparseValue(s.R, s.C, (int[])s.Cp.Clone(), (int[])s.Ri.Clone(), s.Re.Select(x => -x).ToArray(), s.Im?.Select(x => -x).ToArray());
            case "+": return s.Logical ? new SparseValue(s.R, s.C, s.Cp, s.Ri, s.Re, s.Im) : s;
        }
        var r = dense(op, s.ToDense());
        return r is MatrixValue m && m.ND == null ? SparseValue.FromDense(m) : r;
    }
}
