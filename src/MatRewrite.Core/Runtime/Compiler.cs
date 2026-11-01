// MatRewrite - GPL-3.0-or-later
// Phase 6: expressions are compiled once into closures (Func<Interpreter, Value>) and cached per AST node.
// Hot paths (constants, variables, scalar double arithmetic/comparison, short-circuit logic) avoid the
// tree-walking switch and the generic operator dispatch; everything else falls back to EvalTree.
using System.Runtime.CompilerServices;
using MatRewrite.Core.Parsing;

namespace MatRewrite.Core.Runtime;

public sealed partial class Interpreter
{
    /// <summary>Set MATRW_NO_COMPILE=1 to use the plain tree walker (for A/B benchmarking).</summary>
    public static bool UseCompiler = Environment.GetEnvironmentVariable("MATRW_NO_COMPILE") != "1";

    static readonly ConditionalWeakTable<Expr, Func<Interpreter, Value>> compiledCache = new();

    static Func<Interpreter, Value> Compiled(Expr e)
    {
        if (compiledCache.TryGetValue(e, out var f)) return f;
        f = Compile(e);
        compiledCache.AddOrUpdate(e, f);
        return f;
    }

    static Func<Interpreter, Value> Compile(Expr e)
    {
        switch (e)
        {
            case NumberExpr n when !n.Imag:
            {
                double v = n.Value;
                return _ => MatrixValue.Scalar(v);
            }
            case ParenExpr p:
            {
                var inner = Compile(p.Inner);
                return inner;
            }
            case IdentExpr id:
            {
                string name = id.Name;
                return it =>
                {
                    var v = LookupIn(it.Current, name);   // clears Exclusive, like EvalChain
                    return v ?? it.EvalTree(e);
                };
            }
            case UnaryExpr u when u.Op is "-" or "!" or "~":
            {
                var o = Compile(u.Operand); string op = u.Op;
                bool neg = op == "-";
                return it =>
                {
                    var x = o(it);
                    if (x is MatrixValue m && IsRealDoubleScalar(m) && (neg || !double.IsNaN(m.Re[0])))
                        return neg ? MatrixValue.Scalar(-m.Re[0]) : MatrixValue.Bool(m.Re[0] == 0);
                    return Ops.Unary(op, x);
                };
            }
            case BinaryExpr b when b.Op is "&&" or "||":
            {
                var l = Compile(b.Left); var r = Compile(b.Right); string op = b.Op; bool isAnd = op == "&&";
                return it =>
                {
                    bool lv = LogicalScalar(l(it), op);
                    if (isAnd ? !lv : lv) return MatrixValue.Bool(lv);
                    return MatrixValue.Bool(LogicalScalar(r(it), op));
                };
            }
            case BinaryExpr b:
            {
                var l = Compile(b.Left); var r = Compile(b.Right); string op = b.Op;
                var fast = ScalarOp(op);
                return it =>
                {
                    var lv = l(it); var rv = r(it);
                    if (fast != null && lv is MatrixValue lm && rv is MatrixValue rm && IsRealDoubleScalar(lm) && IsRealDoubleScalar(rm))
                        return fast(lm.Re[0], rm.Re[0]);
                    if (lv is ObjectValue || rv is ObjectValue || lv is ObjectArrayValue || rv is ObjectArrayValue) return it.ObjectBinary(op, lv, rv);
                    return Ops.Binary(op, lv, rv);
                };
            }
            default:
                return it => it.EvalTree(e);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsRealDoubleScalar(MatrixValue m) => m.Kind == NumKind.Double && m.Im == null && m.R == 1 && m.C == 1 && m.ND == null;

    static Func<double, double, Value>? ScalarOp(string op) => op switch
    {
        "+" => (a, b) => MatrixValue.Scalar(a + b),
        "-" => (a, b) => MatrixValue.Scalar(a - b),
        "*" or ".*" => (a, b) => MatrixValue.Scalar(a * b),
        "/" or "./" => (a, b) => MatrixValue.Scalar(a / b),
        "<" => (a, b) => MatrixValue.Bool(a < b),
        "<=" => (a, b) => MatrixValue.Bool(a <= b),
        ">" => (a, b) => MatrixValue.Bool(a > b),
        ">=" => (a, b) => MatrixValue.Bool(a >= b),
        "==" => (a, b) => MatrixValue.Bool(a == b),
        "!=" or "~=" => (a, b) => MatrixValue.Bool(a != b),
        _ => null
    };
}
