// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Sparse matrix basics. Storage is dense with an IsSparse attribute.</summary>
public static partial class BuiltinLibrary
{
    static MatrixValue Sp(MatrixValue m) { m.IsSparse = true; if (m.Kind != NumKind.Logical) m.Kind = NumKind.Double; return m; }

    static void RegisterSparse()
    {
        Reg1("sparse", a =>
        {
            if (a.Length == 1) return Sp(M(a[0], "sparse").Copy());
            if (a.Length == 2) return Sp(new MatrixValue((int)M(a[0], "sparse").Re[0], (int)M(a[1], "sparse").Re[0]));
            var I = M(a[0], "sparse"); var J = M(a[1], "sparse"); var V = M(a[2], "sparse");
            int n = Math.Max(I.Numel, Math.Max(J.Numel, V.Numel));
            double Get(MatrixValue x, int k) => x.Numel == 1 ? x.Re[0] : x.Re[k];
            int rows = a.Length >= 5 ? (int)M(a[3], "sparse").Re[0] : (n == 0 ? 0 : (int)Enumerable.Range(0, n).Max(k => Get(I, k)));
            int cols = a.Length >= 5 ? (int)M(a[4], "sparse").Re[0] : (n == 0 ? 0 : (int)Enumerable.Range(0, n).Max(k => Get(J, k)));
            bool cplx = V.Im != null;
            var r = new MatrixValue(rows, cols, NumKind.Double, cplx);
            for (int k = 0; k < n; k++)
            {
                int i = (int)Get(I, k) - 1, j = (int)Get(J, k) - 1;
                if (i < 0 || j < 0 || i >= rows || j >= cols) throw new OctaveException($"sparse: index ({i + 1},{j + 1}) out of bound ({rows}x{cols})");
                r.Re[j * rows + i] += Get(V, k);
                if (cplx) r.Im![j * rows + i] += V.Numel == 1 ? V.Im![0] : V.Im![k];
            }
            if (V.IsLogical) r.Kind = NumKind.Logical;
            return Sp(r);
        });
        Reg1("full", a => { if (a[0] is MatrixValue m) { var c = m.Copy(); c.IsSparse = false; return c; } return a[0]; });
        Reg1("issparse", a => MatrixValue.Bool(a[0] is MatrixValue m && m.IsSparse));
        Reg1("speye", a =>
        {
            int r = (int)M(a[0], "speye").Re[0], c = a.Length > 1 ? (int)M(a[1], "speye").Re[0] : (M(a[0], "speye").Numel > 1 ? (int)M(a[0], "speye").Re[1] : r);
            var m = new MatrixValue(r, c); for (int i = 0; i < Math.Min(r, c); i++) m.Re[i * r + i] = 1; return Sp(m);
        });
        Reg1("spones", a => { var m = M(a[0], "spones"); var r = new MatrixValue(m.R, m.C); for (int i = 0; i < m.Re.Length; i++) r.Re[i] = m.Re[i] != 0 || (m.Im != null && m.Im[i] != 0) ? 1 : 0; return Sp(r); });
        Reg1("spzeros", a => Sp(new MatrixValue((int)M(a[0], "spzeros").Re[0], a.Length > 1 ? (int)M(a[1], "spzeros").Re[0] : (int)M(a[0], "spzeros").Re[0])));
        Reg1("nzmax", a => { var m = M(a[0], "nzmax"); return Num(m.IsSparse ? Math.Max(1, m.Re.Count(x => x != 0)) : m.Numel); });
        Reg1("nonzeros", a => { var m = M(a[0], "nonzeros"); return MatrixValue.Col(m.Re.Where(x => x != 0)); });
    }
}
