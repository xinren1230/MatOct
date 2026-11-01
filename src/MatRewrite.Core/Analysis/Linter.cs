// MatRewrite - GPL-3.0-or-later
using System.Linq;
using System.Text.RegularExpressions;
using MatRewrite.Core.Parsing;

namespace MatRewrite.Core.Analysis;

public enum LintSeverity { Error, Warning }
public sealed record LintMessage(int Line, LintSeverity Severity, string Message)
{
    public override string ToString() => $"{Line}: {(Severity == LintSeverity.Error ? "error" : "warning")}: {Message}";
}

/// <summary>
/// Small static checker for the editor: parse errors, unused variables and outputs that are never set
/// inside functions, unreachable statements, and a few suspicious constructs.
/// </summary>
public static class Linter
{
    public static List<LintMessage> Check(string source)
    {
        var res = new List<LintMessage>();
        ProgramUnit unit;
        try { unit = Parser.ParseProgram(source); }
        catch (Exception ex)
        {
            var m = Regex.Match(ex.Message, @"line (\d+)");
            res.Add(new LintMessage(m.Success ? int.Parse(m.Groups[1].Value) : 1, LintSeverity.Error, ex.Message));
            return res;
        }
        var funcs = new List<FunctionDef>(unit.Functions);
        if (unit.Class != null) funcs.AddRange(unit.Class.Methods.Select(md => md.Func));
        for (int i = 0; i < funcs.Count; i++) funcs.AddRange(funcs[i].Nested);
        Unreachable(unit.Statements, res);
        foreach (var f in funcs) CheckFunction(f, res);
        Suspicious(unit.Statements, res);
        foreach (var f in funcs) Suspicious(f.Body, res);
        res.Sort((a, b) => a.Line.CompareTo(b.Line));
        return res;
    }

    static void CheckFunction(FunctionDef f, List<LintMessage> res)
    {
        Unreachable(f.Body, res);
        var defs = new Dictionary<string, int>();   // first assignment line
        var uses = new HashSet<string>();
        bool dynamic = false;                         // eval/evalin/assignin/exist/load make analysis unsound
        WalkStmts(f.Body, defs, uses, ref dynamic);
        if (dynamic || f.Nested.Count > 0) return;
        foreach (var o in f.Outputs)
            if (o != "varargout" && !defs.ContainsKey(o))
                res.Add(new LintMessage(f.Line, LintSeverity.Warning, $"output '{o}' of function '{f.Name}' may never be set"));
        foreach (var (name, line) in defs)
            if (!uses.Contains(name) && !f.Outputs.Contains(name) && name != "ans" && !name.StartsWith("unused"))
                res.Add(new LintMessage(line, LintSeverity.Warning, $"variable '{name}' is assigned but never used"));
    }

    static void WalkStmts(List<Stmt> body, Dictionary<string, int> defs, HashSet<string> uses, ref bool dyn)
    {
        foreach (var s in body)
        {
            switch (s)
            {
                case ExprStmt e: Uses(e.Expr, uses, ref dyn); break;
                case AssignStmt a:
                    Uses(a.Value, uses, ref dyn);
                    foreach (var t in a.Targets)
                    {
                        if (t is IdentExpr id) { if (a.OpAssign != null) uses.Add(id.Name); defs.TryAdd(id.Name, a.Line); }
                        else if (t is not TildeExpr)
                        {
                            Uses(t, uses, ref dyn);   // x(i) = ... reads x as well
                            var b = Base(t); if (b != null) defs.TryAdd(b, a.Line);
                        }
                    }
                    break;
                case IfStmt i:
                    foreach (var (c, b) in i.Clauses) { Uses(c, uses, ref dyn); WalkStmts(b, defs, uses, ref dyn); }
                    if (i.Else != null) WalkStmts(i.Else, defs, uses, ref dyn);
                    break;
                case ForStmt fo:
                    Uses(fo.Range, uses, ref dyn);
                    if (fo.Var is IdentExpr fv) { defs.TryAdd(fv.Name, fo.Line); uses.Add(fv.Name); } else Uses(fo.Var, uses, ref dyn);
                    WalkStmts(fo.Body, defs, uses, ref dyn); break;
                case WhileStmt w: Uses(w.Cond, uses, ref dyn); WalkStmts(w.Body, defs, uses, ref dyn); break;
                case DoUntilStmt d: WalkStmts(d.Body, defs, uses, ref dyn); Uses(d.Cond, uses, ref dyn); break;
                case SwitchStmt sw:
                    Uses(sw.Subject, uses, ref dyn);
                    foreach (var (l, b) in sw.Cases) { Uses(l, uses, ref dyn); WalkStmts(b, defs, uses, ref dyn); }
                    if (sw.Otherwise != null) WalkStmts(sw.Otherwise, defs, uses, ref dyn);
                    break;
                case TryStmt tr:
                    WalkStmts(tr.Body, defs, uses, ref dyn);
                    if (tr.CatchVar != null) uses.Add(tr.CatchVar);
                    WalkStmts(tr.CatchBody, defs, uses, ref dyn); break;
                case UnwindStmt u: WalkStmts(u.Body, defs, uses, ref dyn); WalkStmts(u.Cleanup, defs, uses, ref dyn); break;
                case GlobalStmt g: foreach (var n in g.Names) uses.Add(n); break;   // globals/persistents escape
            }
        }
    }

    static string? Base(Expr e) => e switch
    {
        IdentExpr i => i.Name,
        IndexExpr ix => Base(ix.Target),
        FieldExpr fe => Base(fe.Target),
        _ => null
    };

    static readonly HashSet<string> Dynamic = new() { "eval", "evalin", "assignin", "exist", "load", "who", "whos", "keyboard", "save", "clear", "inputname" };

    static void Uses(Expr? e, HashSet<string> uses, ref bool dyn)
    {
        switch (e)
        {
            case null: return;
            case IdentExpr i: uses.Add(i.Name); if (Dynamic.Contains(i.Name)) dyn = true; return;
            case BinaryExpr b: Uses(b.Left, uses, ref dyn); Uses(b.Right, uses, ref dyn); return;
            case UnaryExpr u: Uses(u.Operand, uses, ref dyn); return;
            case PostfixExpr p: Uses(p.Operand, uses, ref dyn); return;
            case RangeExpr r: Uses(r.Start, uses, ref dyn); Uses(r.Step, uses, ref dyn); Uses(r.Stop, uses, ref dyn); return;
            case IndexExpr ix: Uses(ix.Target, uses, ref dyn); foreach (var a in ix.Args) Uses(a, uses, ref dyn); return;
            case FieldExpr fe: Uses(fe.Target, uses, ref dyn); Uses(fe.DynName, uses, ref dyn); return;
            case MatrixExpr m: foreach (var row in m.Rows) foreach (var x in row) Uses(x, uses, ref dyn); return;
            case CellExpr c: foreach (var row in c.Rows) foreach (var x in row) Uses(x, uses, ref dyn); return;
            case AnonFuncExpr af: Uses(af.Body, uses, ref dyn); return;
            case FuncHandleExpr fh: uses.Add(fh.Name); return;
            case ParenExpr pe: Uses(pe.Inner, uses, ref dyn); return;
            case AssignExpr ae: Uses(ae.Target, uses, ref dyn); Uses(ae.Value, uses, ref dyn); return;
        }
    }

    static void Unreachable(List<Stmt> body, List<LintMessage> res)
    {
        for (int i = 0; i < body.Count; i++)
        {
            var s = body[i];
            foreach (var child in Children(s)) Unreachable(child, res);
            if (s is ReturnStmt or BreakStmt or ContinueStmt)
            {
                var next = body.Skip(i + 1).FirstOrDefault(x => x is not NoopStmt && x.Line > 0);
                if (next != null) res.Add(new LintMessage(next.Line, LintSeverity.Warning, "this statement can never be reached"));
                return;
            }
        }
    }

    static IEnumerable<List<Stmt>> Children(Stmt s)
    {
        switch (s)
        {
            case IfStmt i: foreach (var c in i.Clauses) yield return c.Body; if (i.Else != null) yield return i.Else; break;
            case ForStmt f: yield return f.Body; break;
            case WhileStmt w: yield return w.Body; break;
            case DoUntilStmt d: yield return d.Body; break;
            case SwitchStmt sw: foreach (var c in sw.Cases) yield return c.Body; if (sw.Otherwise != null) yield return sw.Otherwise; break;
            case TryStmt t: yield return t.Body; yield return t.CatchBody; break;
            case UnwindStmt u: yield return u.Body; yield return u.Cleanup; break;
        }
    }

    static void Suspicious(List<Stmt> body, List<LintMessage> res)
    {
        foreach (var s in body)
        {
            foreach (var c in Children(s)) Suspicious(c, res);
            if (s is IfStmt i)
                foreach (var (c, _) in i.Clauses)
                    if (c is NumberExpr or StringExpr) res.Add(new LintMessage(s.Line, LintSeverity.Warning, "condition is a constant"));
            if (s is WhileStmt w && w.Cond is NumberExpr { Value: 0 }) res.Add(new LintMessage(s.Line, LintSeverity.Warning, "loop body never executes (condition is 0)"));
            if (s is AssignStmt a && a.Targets.Count == 1 && a.Targets[0] is IdentExpr t && a.Value is IdentExpr v && t.Name == v.Name)
                res.Add(new LintMessage(s.Line, LintSeverity.Warning, $"self-assignment '{t.Name} = {t.Name}' has no effect"));
        }
    }
}
