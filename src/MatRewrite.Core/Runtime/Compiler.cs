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
                    {
                        var fr = fast(lm.Re[0], rm.Re[0]);
                        if (fr != null) return fr;
                    }
                    if (lv is ObjectValue || rv is ObjectValue || lv is ObjectArrayValue || rv is ObjectArrayValue) return it.ObjectBinary(op, lv, rv);
                    return Ops.Binary(op, lv, rv);
                };
            }
            case IndexExpr ix when !ix.Brace && ix.Target is IdentExpr tid && ix.Args.Count is 1 or 2 && ix.Args.All(SimpleArg):
                return CompileIndexRead(e, tid.Name, ix.Args);
            default:
                return it => it.EvalTree(e);
        }
    }

    /// <summary>Subscript expressions the compiler handles itself: no 'end', ':', cs-lists or nested indexing.</summary>
    static bool SimpleArg(Expr a) => a switch
    {
        NumberExpr n => !n.Imag,
        IdentExpr => true,
        ParenExpr p => SimpleArg(p.Inner),
        UnaryExpr u => SimpleArg(u.Operand),
        BinaryExpr b => SimpleArg(b.Left) && SimpleArg(b.Right),
        _ => false
    };

    static bool PureValue(Expr a) => a is NumberExpr { Imag: false } or BinaryExpr or UnaryExpr || a is ParenExpr p && PureValue(p.Inner);

    static Value[] EvalCompiledArgs(Interpreter it, Func<Interpreter, Value>[] fa, List<Expr> args)
    {
        var r = new Value[fa.Length];
        for (int k = 0; k < fa.Length; k++)
        {
            if (args[k] is IdentExpr aid && LookupIn(it.Current, aid.Name) is null)
            {
                var l = it.EvalArg(args[k]);
                if (l.Count != 1) return it.EvalArgs(args);
                r[k] = l[0];
            }
            else r[k] = fa[k](it);
        }
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ScalarIndex(Value v, int limit)
    {
        if (v is MatrixValue i && i.Kind == NumKind.Double && i.Im == null && i.R == 1 && i.C == 1 && i.ND == null)
        {
            double d = i.Re[0];
            if (d >= 1 && d <= limit && d == Math.Floor(d)) return (int)d - 1;
        }
        return -1;
    }

    static bool PlainDouble(MatrixValue m) => m.Kind == NumKind.Double && !m.HasIm && m.ND == null && !m.DoubleQuoted;

    static Func<Interpreter, Value> CompileIndexRead(Expr e, string name, List<Expr> args)
    {
        var fa = args.Select(Compile).ToArray();
        return it =>
        {
            var v = LookupRaw(it.Current, name);
            if (v == null)
            {
                if (name is "nargin" or "nargout" or "containers" or "table" or "timetable") return it.EvalTree(e);
                var av = EvalCompiledArgs(it, fa, args);
                var r = it.CallFunction(name, av, 1);
                if (r.Length == 0) throw new OctaveException("indexing produces no results / value undefined");
                return r[0];
            }
            if (v is not MatrixValue m || !PlainDouble(m)) return it.EvalTree(e);
            var a = EvalCompiledArgs(it, fa, args);
            if (a.Length == 1)
            {
                int k = ScalarIndex(a[0], m.Re.Length);
                if (k >= 0) return MatrixValue.Scalar(m.Re[k]);
            }
            else if (a.Length == 2)
            {
                int i = ScalarIndex(a[0], m.R), j = ScalarIndex(a[1], m.C);
                if (i >= 0 && j >= 0) return MatrixValue.Scalar(m.Re[i + j * m.R]);
            }
            return SimplifyObj(Indexing.Read(m, a));
        };
    }

    // ------------------------------------------------------------ statements
    static readonly ConditionalWeakTable<Stmt, Func<Interpreter, Flow>> stmtCache = new();

    static Func<Interpreter, Flow> CompiledStmt(Stmt s)
    {
        if (stmtCache.TryGetValue(s, out var f)) return f;
        f = CompileStmt(s);
        stmtCache.AddOrUpdate(s, f);
        return f;
    }

    static Func<Interpreter, Flow> CompileStmt(Stmt s)
    {
        switch (s)
        {
            case AssignStmt a when a.Targets.Count == 1 && !a.Print && a.OpAssign == null && PureValue(a.Value):
            {
                var val = Compile(a.Value);
                if (a.Targets[0] is IdentExpr tid)
                {
                    string name = tid.Name;
                    return it => { it.StoreVar(name, val(it)); return Flow.Normal; };
                }
                if (a.Targets[0] is IndexExpr { Brace: false, Target: IdentExpr vid } ix && ix.Args.Count is 1 or 2 && ix.Args.All(SimpleArg))
                {
                    string name = vid.Name; var target = a.Targets[0]; var args = ix.Args;
                    var fa = args.Select(Compile).ToArray();
                    return it =>
                    {
                        var rhs = val(it);
                        var cur = LookupRaw(it.Current, name);
                        if (cur is not MatrixValue m || !m.Exclusive || !PlainDouble(m) || rhs is not MatrixValue r || !IsRealDoubleScalar(r))
                        { it.AssignTo(target, rhs); return Flow.Normal; }
                        var av = EvalCompiledArgs(it, fa, args);
                        int lin = -1;
                        if (av.Length == 1) lin = ScalarIndex(av[0], m.Numel);
                        else if (av.Length == 2) { int i = ScalarIndex(av[0], m.R), j = ScalarIndex(av[1], m.C); if (i >= 0 && j >= 0) lin = i + j * m.R; }
                        if (lin >= 0) { m.RawRe[lin] = r.RawRe[0]; return Flow.Normal; }
                        if (av.Length == 1 && av[0] is MatrixValue ai && ai.Numel == 1 && !ai.HasIm && ai.Kind != NumKind.Logical && ai.RawRe[0] == m.Numel + 1 && m.TryAppend(r.RawRe[0])) return Flow.Normal;
                        // out of range / non-scalar subscripts: generic path with the already evaluated subscripts
                        it.AssignTo(new IndexExpr(vid, av.Select(x => (Expr)new ValueExpr(x)).ToList(), false) { Line = ix.Line }, rhs);
                        return Flow.Normal;
                    };
                }
                return it => it.ExecTree(s);
            }
            case IfStmt ifs:
            {
                var conds = ifs.Clauses.Select(c => Compile(c.Cond)).ToArray();
                return it =>
                {
                    for (int k = 0; k < conds.Length; k++)
                        if (Ops.IsTrue(conds[k](it))) return it.ExecBlock(ifs.Clauses[k].Body);
                    return ifs.Else != null ? it.ExecBlock(ifs.Else) : Flow.Normal;
                };
            }
            case WhileStmt w:
            {
                var cond = Compile(w.Cond);
                return it =>
                {
                    while (Ops.IsTrue(cond(it)))
                    {
                        it.CheckInterrupt();
                        var fl = it.ExecBlock(w.Body);
                        if (fl == Flow.Break) break;
                        if (fl == Flow.Return) return fl;
                    }
                    return Flow.Normal;
                };
            }
            case ForStmt fs when fs.Var is IdentExpr fv && fs.Range is RangeExpr re:
            {
                string name = fv.Name;
                return it =>
                {
                    var (start, step, n) = it.RangeParams(re);
                    for (long k = 0; k < n; k++)
                    {
                        it.StoreVar(name, MatrixValue.Scalar(start + k * step));
                        it.CheckInterrupt(); var fl = it.ExecBlock(fs.Body);
                        if (fl == Flow.Break) break;
                        if (fl == Flow.Return) return fl;
                    }
                    return Flow.Normal;
                };
            }
            default:
                return it => it.ExecTree(s);
        }
    }

    static readonly Dictionary<string, (bool ok, long t)> privateExists = new();
    /// <summary>File.Exists for private/ lookups, cached for one second (hot in every user function call).</summary>
    static bool PrivateExists(string path)
    {
        long now = Environment.TickCount64;
        lock (privateExists)
        {
            if (privateExists.TryGetValue(path, out var c) && now - c.t < 1000) return c.ok;
            bool ok = File.Exists(path);
            privateExists[path] = (ok, now);
            return ok;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsRealDoubleScalar(MatrixValue m) => m.Kind == NumKind.Double && !m.HasIm && m.R == 1 && m.C == 1 && m.ND == null;

    static Func<double, double, Value>? ScalarOp(string op) => op switch
    {
        "+" => (a, b) => MatrixValue.Scalar(a + b),
        "-" => (a, b) => MatrixValue.Scalar(a - b),
        "*" or ".*" => (a, b) => MatrixValue.Scalar(a * b),
        "/" or "./" => (a, b) => MatrixValue.Scalar(a / b),
        "^" or ".^" => (a, b) => a < 0 && b != Math.Floor(b) ? null! : MatrixValue.Scalar(Math.Pow(a, b)),
        "<" => (a, b) => MatrixValue.Bool(a < b),
        "<=" => (a, b) => MatrixValue.Bool(a <= b),
        ">" => (a, b) => MatrixValue.Bool(a > b),
        ">=" => (a, b) => MatrixValue.Bool(a >= b),
        "==" => (a, b) => MatrixValue.Bool(a == b),
        "!=" or "~=" => (a, b) => MatrixValue.Bool(a != b),
        _ => null
    };
}
