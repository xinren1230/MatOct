// MatRewrite - GPL-3.0-or-later
using System.Globalization;
namespace MatRewrite.Core.Parsing;

/// <summary>Turns an expression back into Octave source (used for anonymous function display).</summary>
public static class Unparser
{
    [ThreadStatic] static int _mat;
    static string OutMat(Func<string> f) { int o = _mat; _mat = 0; try { return f(); } finally { _mat = o; } }
    static string InMat(Func<string> f) { _mat++; try { return f(); } finally { _mat--; } }
    public static string Print(Expr e) => e switch
    {
        NumberExpr n => n.Text,
        StringExpr s => s.DoubleQuoted ? "\"" + s.Value.Replace("\"", "\\\"") + "\"" : "'" + s.Value.Replace("'", "''") + "'",
        IdentExpr i => i.Name,
        ColonExpr => ":",
        EndExpr => "end",
        TildeExpr => "~",
        BinaryExpr b => $"{Print(b.Left)} {b.Op} {Print(b.Right)}",
        UnaryExpr u => u.Op + Print(u.Operand),
        PostfixExpr p => Print(p.Operand) + p.Op,
        RangeExpr r => r.Step == null ? $"{Print(r.Start)}:{Print(r.Stop)}" : $"{Print(r.Start)}:{Print(r.Step)}:{Print(r.Stop)}",
        IndexExpr ix => Print(ix.Target) + (ix.Brace ? "{" : (_mat > 0 ? "(" : " (")) + string.Join(", ", ix.Args.Select(Print)) + (ix.Brace ? "}" : ")"),
        FieldExpr f => Print(f.Target) + "." + (f.Name ?? "(" + Print(f.DynName!) + ")"),
        MatrixExpr m => InMat(() => "[" + string.Join("; ", m.Rows.Select(r => string.Join(", ", r.Select(Print)))) + "]"),
        CellExpr c => InMat(() => "{" + string.Join("; ", c.Rows.Select(r => string.Join(", ", r.Select(Print)))) + "}"),
        AnonFuncExpr a => "@(" + string.Join(", ", a.Params) + ") " + OutMat(() => Print(a.Body)),
        FuncHandleExpr h => "@" + h.Name,
        ParenExpr p => "(" + Print(p.Inner) + ")",
        AssignExpr a => a.PostOld ? Print(a.Target) + "++" : Print(a.Target) + " = " + Print(a.Value),
        _ => "?"
    };
}
