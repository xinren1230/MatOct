// MatRewrite - GPL-3.0-or-later
namespace MatRewrite.Core.Parsing;

public abstract record Node { public int Line { get; init; } }

// ---------- expressions ----------
public abstract record Expr : Node;
public sealed record NumberExpr(double Value, bool Imag, string Text) : Expr;
public sealed record StringExpr(string Value, bool DoubleQuoted) : Expr;
public sealed record IdentExpr(string Name) : Expr;
public sealed record ColonExpr : Expr;          // magic colon inside an index
public sealed record EndExpr : Expr;            // 'end' inside an index
public sealed record TildeExpr : Expr;          // ~ placeholder in [~, i] = ...
public sealed record BinaryExpr(string Op, Expr Left, Expr Right) : Expr;
public sealed record UnaryExpr(string Op, Expr Operand) : Expr;
public sealed record PostfixExpr(string Op, Expr Operand) : Expr;
public sealed record RangeExpr(Expr Start, Expr? Step, Expr Stop) : Expr;
public sealed record IndexExpr(Expr Target, List<Expr> Args, bool Brace) : Expr;
public sealed record FieldExpr(Expr Target, string? Name, Expr? DynName) : Expr;
public sealed record MatrixExpr(List<List<Expr>> Rows) : Expr;
public sealed record CellExpr(List<List<Expr>> Rows) : Expr;
public sealed record AnonFuncExpr(List<string> Params, Expr Body) : Expr;
public sealed record FuncHandleExpr(string Name) : Expr;
public sealed record ParenExpr(Expr Inner) : Expr;

// ---------- statements ----------
public abstract record Stmt : Node;
public sealed record ExprStmt(Expr Expr, bool Print) : Stmt;
public sealed record AssignStmt(List<Expr> Targets, Expr Value, bool Print, string? OpAssign = null) : Stmt;
public sealed record IfStmt(List<(Expr Cond, List<Stmt> Body)> Clauses, List<Stmt>? Else) : Stmt;
public sealed record ForStmt(Expr Var, Expr Range, List<Stmt> Body) : Stmt;
public sealed record WhileStmt(Expr Cond, List<Stmt> Body) : Stmt;
public sealed record DoUntilStmt(List<Stmt> Body, Expr Cond) : Stmt;
public sealed record SwitchStmt(Expr Subject, List<(Expr Label, List<Stmt> Body)> Cases, List<Stmt>? Otherwise) : Stmt;
public sealed record TryStmt(List<Stmt> Body, string? CatchVar, List<Stmt> CatchBody) : Stmt;
public sealed record UnwindStmt(List<Stmt> Body, List<Stmt> Cleanup) : Stmt;
public sealed record BreakStmt : Stmt;
public sealed record ContinueStmt : Stmt;
public sealed record ReturnStmt : Stmt;
public sealed record GlobalStmt(List<string> Names, List<Expr?> Inits, bool Persistent) : Stmt;
public sealed record NoopStmt : Stmt;

public sealed record FunctionDef(string Name, List<string> Params, List<string> Outputs, List<Stmt> Body) : Node;

public sealed class ProgramUnit
{
    public List<Stmt> Statements = new();
    public List<FunctionDef> Functions = new();
    public bool IsFunctionFile;
}
