// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Parsing;
using MatRewrite.Core.Builtins;

namespace MatRewrite.Core.Runtime;

public delegate Value[] BuiltinFn(Interpreter it, Value[] args, int nargout);

/// <summary>A user-defined function (from a function file, script, or the command line).</summary>
public sealed class UserFunction
{
    public string Name = "";
    public List<string> Params = new();
    public List<string> Outputs = new();
    public List<Stmt> Body = new();
    public string? File;
    public bool IsScript;
    public Dictionary<string, UserFunction> Siblings = new(); // subfunctions in the same file
    public Dictionary<string, Value> Persistents = new();
    public UserFunction? Parent;                                // for nested functions
    public Dictionary<string, UserFunction> Nested = new();
    public int Line;

    public static UserFunction From(FunctionDef d, string? file, UserFunction? parent = null)
    {
        var f = new UserFunction { Name = d.Name, Params = d.Params, Outputs = d.Outputs, Body = d.Body, File = file, Parent = parent, Line = d.Line };
        foreach (var n in d.Nested) f.Nested[n.Name] = From(n, file, f);
        return f;
    }

    /// <summary>Share the file's subfunction table with nested functions too.</summary>
    public void SetSiblings(Dictionary<string, UserFunction> sibs)
    {
        Siblings = sibs;
        foreach (var n in Nested.Values) n.SetSiblings(sibs);
    }
}

public sealed class Scope
{
    public Dictionary<string, Value> Vars = new();
    public Dictionary<string, Dictionary<string, Value>> Redirect = new(); // global / persistent
    public UserFunction? Func;
    public int Nargin = -1, Nargout = -1;
    public Scope? ParentScope;   // enclosing function's live workspace (nested functions)
    public int Line;             // current line, for error stack traces
    public string FrameName = "";
}

enum Flow { Normal, Break, Continue, Return }

sealed class ControlException : Exception { public bool Quit; public int Code; }

/// <summary>Tree-walking interpreter for the Octave language.</summary>
public sealed partial class Interpreter
{
    public TextWriter Out;
    public readonly Scope Base = new();
    public Scope Current;
    public readonly Dictionary<string, Value> Globals = new();
    public readonly Dictionary<string, BuiltinFn> Builtins = new(StringComparer.Ordinal);
    public readonly Dictionary<string, UserFunction> CommandLineFunctions = new();
    public readonly List<string> SearchPath = new();
    public string LastError = "";
    public Random Rng = new();
    public System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    public double TicTime = double.NaN;

    readonly List<(Value v, int pos, int n)> endStack = new();
    readonly Dictionary<string, (DateTime mtime, UserFunction fn)> fileCache = new();
    readonly HashSet<string> missingCache = new();
    int depth;
    public const int MaxRecursion = 256;
    /// <summary>Active function/script frames (innermost last).</summary>
    public readonly List<Scope> CallStack = new();

    public Interpreter(TextWriter? output = null)
    {
        Out = output ?? Console.Out;
        Current = Base;
        BuiltinLibrary.RegisterAll(this);
        Ops.Warn = msg => Warning(msg);
    }

    public void Warning(string msg) { Out.Flush(); Out.WriteLine("warning: " + msg); }

    // ------------------------------------------------------------ public API
    /// <summary>Execute code in the base workspace (like typing at the prompt).</summary>
    public void Eval(string code)
    {
        missingCache.Clear();
        var unit = Parser.ParseProgram(code);
        foreach (var f in unit.Functions) CommandLineFunctions[f.Name] = UserFunction.From(f, null);
        var saveScope = Current;
        try { Current = Base; endStack.Clear(); depth = 0; ExecBlock(unit.Statements); }
        finally { Current = saveScope; Out.Flush(); }
    }

    /// <summary>Evaluate code in the current scope (used by eval()).</summary>
    public void EvalInCurrentScope(string code)
    {
        var unit = Parser.ParseProgram(code);
        foreach (var f in unit.Functions) CommandLineFunctions[f.Name] = UserFunction.From(f, null);
        var fl = ExecBlock(unit.Statements);
    }

    /// <summary>Run a script file in the base workspace.</summary>
    public void RunFile(string path)
    {
        path = Path.GetFullPath(path);
        missingCache.Clear();
        var fn = LoadFile(path, Path.GetFileNameWithoutExtension(path));
        var save = Current;
        try
        {
            Current = Base;
            if (fn.IsScript) ExecScript(fn);
            else CallUser(fn, Array.Empty<Value>(), 0);
        }
        finally { Current = save; Out.Flush(); }
    }

    /// <summary>Run a script file in the current scope (run/source).</summary>
    public void RunFileHere(string path)
    {
        if (!File.Exists(path) && File.Exists(path + ".m")) path += ".m";
        if (!File.Exists(path)) throw new OctaveException($"run: file SCRIPT must exist and be a valid Octave scriptfile");
        var full = Path.GetFullPath(path);
        var fn = LoadFile(full, Path.GetFileNameWithoutExtension(full));
        if (fn.IsScript) ExecScript(fn); else CallUser(fn, Array.Empty<Value>(), 0);
    }

    public Value? GetVar(string name) => LookupVar(name);
    public void SetVar(string name, Value v) => StoreVar(name, v);
    public IEnumerable<string> VariableNames => Current.Vars.Keys.Concat(Current.Redirect.Keys).Distinct().OrderBy(s => s, StringComparer.Ordinal);
    public void ClearVar(string name) { Current.Vars.Remove(name); Current.Redirect.Remove(name); }

    // ------------------------------------------------------------ variables
    Value? LookupVar(string name) => LookupIn(Current, name);

    static bool IsOwnName(Scope sc, string name) =>
        sc.Func != null && (sc.Func.Params.Contains(name) || sc.Func.Outputs.Contains(name));

    static Value? LookupIn(Scope sc, string name)
    {
        for (var s = sc; s != null; s = s.ParentScope)
        {
            if (s.Redirect.TryGetValue(name, out var store)) return store.TryGetValue(name, out var gv) ? gv : null;
            if (s.Vars.TryGetValue(name, out var v)) return v;
            if (IsOwnName(s, name)) return null;
        }
        return null;
    }

    void StoreVar(string name, Value v)
    {
        for (var s = Current; s != null; s = s.ParentScope)
        {
            if (s.Redirect.TryGetValue(name, out var store)) { store[name] = v; return; }
            if (s.Vars.ContainsKey(name) || IsOwnName(s, name) || s.ParentScope == null) { s.Vars[name] = v; return; }
        }
        Current.Vars[name] = v;
    }
    public bool IsVariable(string name) => LookupVar(name) != null;

    // ------------------------------------------------------------ statements
    Flow ExecBlock(List<Stmt> stmts)
    {
        foreach (var s in stmts)
        {
            var f = Exec(s);
            if (f != Flow.Normal) return f;
        }
        return Flow.Normal;
    }

    Flow Exec(Stmt s)
    {
        if (s.Line > 0) Current.Line = s.Line;
        switch (s)
        {
            case ExprStmt es: ExecExprStmt(es); return Flow.Normal;
            case AssignStmt a: ExecAssign(a); return Flow.Normal;
            case IfStmt ifs:
                foreach (var (cond, body) in ifs.Clauses)
                    if (Ops.IsTrue(Eval(cond))) return ExecBlock(body);
                return ifs.Else != null ? ExecBlock(ifs.Else) : Flow.Normal;
            case ForStmt f: return ExecFor(f);
            case WhileStmt w:
                while (Ops.IsTrue(Eval(w.Cond)))
                {
                    var fl = ExecBlock(w.Body);
                    if (fl == Flow.Break) break;
                    if (fl == Flow.Return) return fl;
                }
                return Flow.Normal;
            case DoUntilStmt d:
                do
                {
                    var fl = ExecBlock(d.Body);
                    if (fl == Flow.Break) break;
                    if (fl == Flow.Return) return fl;
                } while (!Ops.IsTrue(Eval(d.Cond)));
                return Flow.Normal;
            case SwitchStmt sw: return ExecSwitch(sw);
            case TryStmt t:
                try { return ExecBlock(t.Body); }
                catch (Exception ex) when (ex is not ControlException)
                {
                    var (msg, id) = ErrorInfo(ex);
                    LastError = msg;
                    if (t.CatchVar != null) StoreVar(t.CatchVar, MakeErrorStruct(msg, id, (ex as OctaveException)?.Stack));
                    return ExecBlock(t.CatchBody);
                }
            case UnwindStmt u:
                try { return ExecBlock(u.Body); }
                finally { ExecBlock(u.Cleanup); }
            case BreakStmt: return Flow.Break;
            case ContinueStmt: return Flow.Continue;
            case ReturnStmt: return Flow.Return;
            case GlobalStmt g:
                for (int k = 0; k < g.Names.Count; k++)
                {
                    var name = g.Names[k];
                    var store = g.Persistent ? (Current.Func?.Persistents ?? Globals) : Globals;
                    Current.Vars.Remove(name);
                    Current.Redirect[name] = store;
                    if (!store.ContainsKey(name))
                        store[name] = g.Inits[k] != null ? Eval(g.Inits[k]!) : MatrixValue.Empty();
                }
                return Flow.Normal;
            case NoopStmt: return Flow.Normal;
        }
        throw new OctaveException("unsupported statement");
    }

    public static (string msg, string id) ErrorInfo(Exception ex) => ex switch
    {
        OctaveException oe => (oe.Message, oe.Identifier),
        _ => (ex.Message, "Octave:internal")
    };

    public static StructValue MakeErrorStruct(string msg, string id, List<StackFrameInfo>? stack = null)
    {
        var s = new StructValue();
        s.Set("message", MatrixValue.Str(msg));
        s.Set("identifier", MatrixValue.Str(id));
        s.Set("stack", StackStruct(stack));
        return s;
    }

    public static StructValue StackStruct(List<StackFrameInfo>? stack)
    {
        int n = stack?.Count ?? 0;
        var st = new StructValue(Math.Max(n, 1), 1, new List<string> { "file", "name", "line", "column" });
        if (n == 0)
        {
            st = new StructValue();
            st.Set("file", MatrixValue.Str("")); st.Set("name", MatrixValue.Str("")); st.Set("line", MatrixValue.Scalar(0)); st.Set("column", MatrixValue.Scalar(0));
            return st;
        }
        for (int k = 0; k < n; k++)
        {
            var f = stack![k];
            st.Set("file", MatrixValue.Str(f.File), k); st.Set("name", MatrixValue.Str(f.Name), k);
            st.Set("line", MatrixValue.Scalar(f.Line), k); st.Set("column", MatrixValue.Scalar(f.Column), k);
        }
        return st;
    }

    /// <summary>Octave-style "called from" trace lines for an error.</summary>
    public static string FormatStack(Exception ex)
    {
        if (ex is not OctaveException oe || oe.Stack.Count == 0) return "";
        var sb = new System.Text.StringBuilder("error: called from\n");
        foreach (var f in oe.Stack) sb.Append($"    {f.Name}{(f.Line > 0 ? $" at line {f.Line}" : "")}\n");
        return sb.ToString();
    }

    static OctaveException AddFrame(Exception ex, Scope sc)
    {
        var oe = ex as OctaveException ?? new OctaveException(ex.Message, "Octave:internal");
        oe.Stack.Add(new StackFrameInfo(sc.FrameName, sc.Func?.File ?? "", sc.Line, 0));
        return oe;
    }

    void ExecExprStmt(ExprStmt es)
    {
        if (es.Expr is IdentExpr id && LookupVar(id.Name) is Value v)
        {
            if (es.Print) Out.Write(Display.Show(id.Name, v));
            return;
        }
        var vals = EvalMulti(es.Expr, 0);
        foreach (var r in vals)
        {
            StoreVar("ans", r);
            if (es.Print) Out.Write(Display.Show("ans", r));
        }
    }

    void ExecAssign(AssignStmt a)
    {
        if (a.Targets.Count == 1)
        {
            var vals = EvalMulti(a.Value, 1);
            if (vals.Count == 0) throw new OctaveException("value on right hand side of assignment is undefined");
            var name = AssignTo(a.Targets[0], vals[0]);
            if (a.Print) Out.Write(Display.Show(name, LookupVar(name)!));
            return;
        }
        int nout = a.Targets.Count;
        var results = EvalMulti(a.Value, nout);
        for (int k = 0; k < nout; k++)
        {
            if (a.Targets[k] is TildeExpr) continue;
            if (k >= results.Count)
            {
                if (k == 0) throw new OctaveException("value on right hand side of assignment is undefined");
                throw new OctaveException($"element number {k + 1} undefined in return list");
            }
            var name = AssignTo(a.Targets[k], results[k]);
            if (a.Print) Out.Write(Display.Show(name, LookupVar(name)!));
        }
    }

    Flow ExecFor(ForStmt f)
    {
        if (f.Range is RangeExpr re)
        {
            var (start, step, n) = RangeParams(re);
            for (long k = 0; k < n; k++)
            {
                AssignTo(f.Var, MatrixValue.Scalar(start + k * step));
                var fl = ExecBlock(f.Body);
                if (fl == Flow.Break) break;
                if (fl == Flow.Return) return fl;
            }
            return Flow.Normal;
        }
        var v = Eval(f.Range);
        int cols, rows;
        if (v is ArrayValue av) { rows = av.R; cols = av.Numel == 0 ? 0 : av.C; }
        else { rows = 1; cols = 1; }
        for (int j = 0; j < cols; j++)
        {
            Value item;
            if (v is ArrayValue arr)
            {
                var lin = Enumerable.Range(0, rows).Select(i => i + j * rows).ToArray();
                item = arr.Gather(lin, rows, 1);
            }
            else item = v;
            AssignTo(f.Var, item);
            var fl = ExecBlock(f.Body);
            if (fl == Flow.Break) break;
            if (fl == Flow.Return) return fl;
        }
        return Flow.Normal;
    }

    Flow ExecSwitch(SwitchStmt sw)
    {
        var subj = Eval(sw.Subject);
        foreach (var (label, body) in sw.Cases)
        {
            var lv = Eval(label);
            bool match = lv is CellValue cl ? cl.Data.Any(x => SwitchMatch(subj, x)) : SwitchMatch(subj, lv);
            if (match) return ExecBlock(body);
        }
        return sw.Otherwise != null ? ExecBlock(sw.Otherwise) : Flow.Normal;
    }

    static bool SwitchMatch(Value subj, Value label)
    {
        if (subj is MatrixValue s && label is MatrixValue l)
        {
            if (s.IsChar || l.IsChar)
                return s.IsChar && l.IsChar && s.AsString() == l.AsString();
            if (s.Numel == 0 || l.Numel == 0) return false;
            return l.Numel == 1 ? s.Re[0] == l.Re[0] && (s.Numel == 1) : false;
        }
        return false;
    }

    public (double start, double step, long n) RangeParams(RangeExpr re)
    {
        double a = ScalarOf(Eval(re.Start), "colon");
        double st = re.Step != null ? ScalarOf(Eval(re.Step), "colon") : 1;
        double b = ScalarOf(Eval(re.Stop), "colon");
        return (a, st, RangeCount(a, st, b));
    }

    public static long RangeCount(double a, double st, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(st) || double.IsNaN(b)) return 0;
        if (st == 0 || (st > 0 && a > b) || (st < 0 && a < b)) return 0;
        double n = Math.Floor((b - a) / st * (1 + 1e-15) + 1e-10) + 1;
        if (n > int.MaxValue) throw new OctaveException("out of memory or dimension too large for Octave's index type");
        return (long)n;
    }

    static double ScalarOf(Value v, string ctx)
    {
        if (v is MatrixValue m && m.Numel >= 1) return m.Re[0];
        if (v is MatrixValue m0 && m0.Numel == 0) return double.NaN;
        throw new OctaveException($"invalid types found in range");
    }

    // ------------------------------------------------------------ assignment
    sealed record Op(char Kind, List<Expr>? Args, string? Field, Expr? DynField);

    static Expr Flatten(Expr e, List<Op> ops)
    {
        var stack = new List<Op>();
        while (true)
        {
            switch (e)
            {
                case IndexExpr ix: stack.Add(new Op(ix.Brace ? '{' : '(', ix.Args, null, null)); e = ix.Target; continue;
                case FieldExpr fe: stack.Add(new Op('.', null, fe.Name, fe.DynName)); e = fe.Target; continue;
            }
            break;
        }
        stack.Reverse();
        ops.AddRange(stack);
        return e;
    }

    string FieldName(Op op)
    {
        if (op.Field != null) return op.Field;
        var v = Eval(op.DynField!);
        if (v is MatrixValue m && m.IsChar) return m.AsString();
        throw new OctaveException("dynamic structure field names must be strings");
    }

    /// <summary>Assign rhs into an lvalue expression; returns the root variable name.</summary>
    public string AssignTo(Expr target, Value rhs)
    {
        var ops = new List<Op>();
        var root = Flatten(target, ops);
        if (root is not IdentExpr id) throw new OctaveException("invalid assignment target");
        if (ops.Count == 0)
        {
            if (rhs is MatrixValue m && m.DoubleQuoted == false) { }
            StoreVar(id.Name, rhs);
            return id.Name;
        }
        var cur = LookupVar(id.Name);
        StoreVar(id.Name, AssignChain(cur, ops, 0, rhs));
        return id.Name;
    }

    Value AssignChain(Value? cur, List<Op> ops, int k, Value rhs)
    {
        if (k == ops.Count) return rhs;
        var op = ops[k];
        bool last = k == ops.Count - 1;
        switch (op.Kind)
        {
            case '.':
            {
                var name = FieldName(op);
                StructValue s = cur switch
                {
                    null => new StructValue(),
                    StructValue sv => sv.Copy(),
                    MatrixValue mv when mv.Numel == 0 => new StructValue(),
                    _ => throw new OctaveException($"invalid use of a N_-D array in indexed assignment; scalar cannot be indexed with .")
                };
                if (s.Numel == 0) s = (StructValue)s.Resized(1, 1);
                if (s.Numel != 1) throw new OctaveException("invalid number of output arguments for expression X.name (struct array)");
                Value? sub = s.HasField(name) ? s.Elems[0][name] : null;
                s.Set(name, AssignChain(sub, ops, k + 1, rhs));
                return s;
            }
            case '(':
            {
                if (cur is FunctionHandleValue) throw new OctaveException("invalid use of a function handle in indexed assignment");
                if (cur is MapValue map)
                {
                    var ka = EvalArgs(op.Args!);
                    if (ka.Length != 1) throw new OctaveException("only '()' indexing with exactly one key is supported by containers.Map");
                    Value? old = map.Has(ka[0]) ? map.Get(ka[0]) : null;
                    map.Set(ka[0], last ? rhs : AssignChain(old, ops, k + 1, rhs));
                    return map;
                }
                var curArr = cur as ArrayValue;
                var args = EvalIndexArgs(op.Args!, cur ?? MatrixValue.Empty());
                if (last)
                {
                    if (rhs is not ArrayValue ra)
                    {
                        if (curArr == null && args.All(a => a is MatrixValue m && m.Numel == 1 && m.Re[0] == 1)) return rhs;
                        throw new OctaveException($"operator = undefined for '{cur?.ClassName ?? "matrix"}' by '{rhs.ClassName}' operations");
                    }
                    return Indexing.Assign(curArr, args, ra);
                }
                Value? elem = null;
                if (curArr != null && curArr.Numel > 0)
                {
                    try { var e = Indexing.Read(curArr, args); if (e.Numel == 1) elem = e; } catch (OctaveException) { }
                }
                if (elem == null && ops[k + 1].Kind == '.') elem = new StructValue();
                var newElem = AssignChain(elem, ops, k + 1, rhs);
                if (newElem is not ArrayValue na) throw new OctaveException("invalid indexed assignment");
                if (curArr is MatrixValue cm && cm.Numel == 0) curArr = null;
                return Indexing.Assign(curArr, args, na);
            }
            case '{':
            {
                CellValue c = cur switch
                {
                    null => new CellValue(0, 0),
                    CellValue cv => cv,
                    MatrixValue mv when mv.Numel == 0 => new CellValue(0, 0),
                    _ => throw new OctaveException("matrix cannot be indexed with {")
                };
                var args = EvalIndexArgs(op.Args!, c);
                if (last) return Indexing.Assign(c, args, new CellValue(1, 1, new[] { rhs }));
                Value? elem = null;
                try { var e = (CellValue)Indexing.Read(c, args); if (e.Numel == 1) elem = e.Data[0]; } catch (OctaveException) { }
                var nv = AssignChain(elem, ops, k + 1, rhs);
                return Indexing.Assign(c, args, new CellValue(1, 1, new[] { nv }));
            }
        }
        throw new OctaveException("invalid assignment");
    }

    // ------------------------------------------------------------ expressions
    public Value Eval(Expr e)
    {
        switch (e)
        {
            case NumberExpr n: return n.Imag ? MatrixValue.Cplx(0, n.Value) : MatrixValue.Scalar(n.Value);
            case StringExpr s: return MatrixValue.Str(s.Value, s.DoubleQuoted);
            case BinaryExpr b: return EvalBinary(b);
            case UnaryExpr u: return Ops.Unary(u.Op, Eval(u.Operand));
            case ParenExpr p: return Eval(p.Inner);
        }
        var vals = EvalMulti(e, 1);
        if (vals.Count == 0)
        {
            if (e is IdentExpr id0) throw new OctaveException($"value on right hand side of assignment is undefined ('{id0.Name}' returned no value)");
            throw new OctaveException("indexing produces no results / value undefined");
        }
        return vals[0];
    }

    Value EvalBinary(BinaryExpr b)
    {
        if (b.Op == "&&" || b.Op == "||")
        {
            var l = Eval(b.Left);
            bool lv = LogicalScalar(l, b.Op);
            if (b.Op == "&&" && !lv) return MatrixValue.Bool(false);
            if (b.Op == "||" && lv) return MatrixValue.Bool(true);
            return MatrixValue.Bool(LogicalScalar(Eval(b.Right), b.Op));
        }
        return Ops.Binary(b.Op, Eval(b.Left), Eval(b.Right));
    }

    static bool LogicalScalar(Value v, string op)
    {
        if (v is MatrixValue m)
        {
            if (m.Numel == 0) throw new OctaveException($"invalid conversion from empty value to real scalar");
            return Ops.IsTrue(m);
        }
        throw new OctaveException($"binary operator '{op}' not implemented for '{v.ClassName}' operands");
    }

    public List<Value> EvalMulti(Expr e, int nargout)
    {
        switch (e)
        {
            case NumberExpr or StringExpr or BinaryExpr or UnaryExpr or ParenExpr:
                return new List<Value> { Eval(e) };
            case ColonExpr:
                if (endStack.Count > 0) return new List<Value> { MagicColon.Instance };
                return new List<Value> { MatrixValue.Str(":") };
            case EndExpr:
            {
                if (endStack.Count == 0) throw new OctaveException("'end': nonconformant arguments");
                var (v, pos, n) = endStack[^1];
                int val; { var vd = v.Dims; if (n == 1) val = v.Numel; else if (pos < n - 1) val = pos < vd.Length ? vd[pos] : 1; else val = NDArray.Prod(vd, pos); }
                return new List<Value> { MatrixValue.Scalar(val) };
            }
            case PostfixExpr pf:
            {
                var v = Eval(pf.Operand);
                if (v is ArrayValue av) return new List<Value> { Ops.Transpose(av, pf.Op == "'") };
                return new List<Value> { v };
            }
            case RangeExpr re:
            {
                var (a, st, n) = RangeParams(re);
                var m = new MatrixValue(1, (int)n);
                for (int k = 0; k < n; k++) m.Re[k] = a + k * st;
                return new List<Value> { m };
            }
            case MatrixExpr me: return new List<Value> { BuildMatrix(me.Rows, false) };
            case CellExpr ce: return new List<Value> { BuildMatrix(ce.Rows, true) };
            case AnonFuncExpr af: return new List<Value> { MakeAnon(af) };
            case FuncHandleExpr fh:
                return new List<Value> { new FunctionHandleValue { Name = fh.Name, Context = Current.Func } };
            case IdentExpr or IndexExpr or FieldExpr:
                return EvalChain(e, nargout);
        }
        throw new OctaveException($"cannot evaluate expression {e.GetType().Name}");
    }

    FunctionHandleValue MakeAnon(AnonFuncExpr af)
    {
        var captured = new Dictionary<string, Value>();
        foreach (var name in CollectIdents(af.Body))
        {
            if (af.Params.Contains(name)) continue;
            var v = LookupVar(name);
            if (v != null) captured[name] = v;
        }
        return new FunctionHandleValue { Params = af.Params, Body = af.Body, Captured = captured, Context = Current.Func };
    }

    static IEnumerable<string> CollectIdents(Expr e)
    {
        switch (e)
        {
            case IdentExpr i: yield return i.Name; break;
            case BinaryExpr b: foreach (var x in CollectIdents(b.Left).Concat(CollectIdents(b.Right))) yield return x; break;
            case UnaryExpr u: foreach (var x in CollectIdents(u.Operand)) yield return x; break;
            case PostfixExpr p: foreach (var x in CollectIdents(p.Operand)) yield return x; break;
            case ParenExpr pe: foreach (var x in CollectIdents(pe.Inner)) yield return x; break;
            case RangeExpr r:
                foreach (var x in CollectIdents(r.Start)) yield return x;
                if (r.Step != null) foreach (var x in CollectIdents(r.Step)) yield return x;
                foreach (var x in CollectIdents(r.Stop)) yield return x;
                break;
            case IndexExpr ix:
                foreach (var x in CollectIdents(ix.Target)) yield return x;
                foreach (var a in ix.Args) foreach (var x in CollectIdents(a)) yield return x;
                break;
            case FieldExpr f:
                foreach (var x in CollectIdents(f.Target)) yield return x;
                if (f.DynName != null) foreach (var x in CollectIdents(f.DynName)) yield return x;
                break;
            case MatrixExpr m: foreach (var row in m.Rows) foreach (var a in row) foreach (var x in CollectIdents(a)) yield return x; break;
            case CellExpr c: foreach (var row in c.Rows) foreach (var a in row) foreach (var x in CollectIdents(a)) yield return x; break;
            case AnonFuncExpr af: foreach (var x in CollectIdents(af.Body)) if (!af.Params.Contains(x)) yield return x; break;
        }
    }

    Value BuildMatrix(List<List<Expr>> rows, bool cell)
    {
        var rowVals = new List<Value>();
        foreach (var row in rows)
        {
            var items = new List<Value>();
            foreach (var ex in row)
            {
                foreach (var v in EvalArg(ex))
                    items.Add(cell ? (v is CellValue cv0 && false ? cv0 : new CellValue(1, 1, new[] { v })) : v);
            }
            rowVals.Add(Concat.Horz(items));
        }
        if (rowVals.Count == 0) return cell ? new CellValue(0, 0) : MatrixValue.Empty();
        return Concat.Vert(rowVals);
    }

    /// <summary>Evaluate an argument, expanding cs-lists like c{:} and s.f.</summary>
    public List<Value> EvalArg(Expr ex)
    {
        if (ex is IndexExpr { Brace: true } || ex is FieldExpr) return EvalMulti(ex, 1);
        if (ex is ColonExpr && endStack.Count == 0) return new List<Value> { MatrixValue.Str(":") };
        return new List<Value> { Eval(ex) };
    }

    Value[] EvalArgs(List<Expr> args)
    {
        var list = new List<Value>();
        foreach (var a in args) list.AddRange(EvalArg(a));
        return list.ToArray();
    }

    Value[] EvalIndexArgs(List<Expr> args, Value container)
    {
        var list = new List<Value>();
        for (int k = 0; k < args.Count; k++)
        {
            endStack.Add((container, k, args.Count));
            try
            {
                if (args[k] is ColonExpr) list.Add(MagicColon.Instance);
                else list.AddRange(EvalArg(args[k]));
            }
            finally { endStack.RemoveAt(endStack.Count - 1); }
        }
        return list.ToArray();
    }

    List<Value> EvalChain(Expr e, int nargout)
    {
        var ops = new List<Op>();
        var root = Flatten(e, ops);
        List<Value> cur;
        int start = 0;
        if (root is IdentExpr id)
        {
            var v = LookupVar(id.Name);
            if (v != null) cur = new List<Value> { v };
            else if (id.Name == "containers" && ops.Count > 0 && ops[0].Kind == '.' && ops[0].Field == "Map")
            {
                Value[] margs = Array.Empty<Value>();
                start = 1;
                if (ops.Count > 1 && ops[1].Kind == '(') { margs = EvalArgs(ops[1].Args!); start = 2; }
                cur = CallFunction("containers.Map", margs, 1).ToList();
                if (start == ops.Count) return cur;
            }
            else if (id.Name == "nargin" && Current.Nargin >= 0 && ops.Count == 0) return new List<Value> { MatrixValue.Scalar(Current.Nargin) };
            else if (id.Name == "nargout" && Current.Nargout >= 0 && ops.Count == 0) return new List<Value> { MatrixValue.Scalar(Current.Nargout) };
            else
            {
                Value[] args = Array.Empty<Value>();
                if (ops.Count > 0 && ops[0].Kind == '(') { args = EvalArgs(ops[0].Args!); start = 1; }
                int nout = start == ops.Count ? nargout : 1;
                cur = CallFunction(id.Name, args, nout).ToList();
                if (start == ops.Count) return cur;
                if (cur.Count == 0) throw new OctaveException($"indexing undefined value");
            }
        }
        else cur = new List<Value> { Eval(root) };

        for (int k = start; k < ops.Count; k++)
        {
            var op = ops[k];
            bool last = k == ops.Count - 1;
            if (cur.Count != 1)
                throw new OctaveException(cur.Count == 0 ? "indexing undefined value" : "a cs-list cannot be further indexed");
            var v = cur[0];
            switch (op.Kind)
            {
                case '(':
                    if (v is FunctionHandleValue fh)
                    {
                        cur = CallHandle(fh, EvalArgs(op.Args!), last ? nargout : 1).ToList();
                        break;
                    }
                    if (v is MapValue mv0)
                    {
                        var ka = EvalArgs(op.Args!);
                        if (ka.Length != 1) throw new OctaveException("only '()' indexing with exactly one key is supported by containers.Map");
                        cur = new List<Value> { mv0.Get(ka[0]) };
                        break;
                    }
                    if (v is not ArrayValue av) throw new OctaveException($"'{v.ClassName}' object cannot be indexed with (");
                    cur = new List<Value> { Indexing.Read(av, EvalIndexArgs(op.Args!, v)) };
                    break;
                case '{':
                {
                    if (v is not CellValue cv)
                        throw new OctaveException(v is FunctionHandleValue ? "'{' undefined for arguments of type 'function_handle'" : $"'{{' undefined for arguments of type '{v.ClassName}'; matrix cannot be indexed with {{");
                    var sub = (CellValue)Indexing.Read(cv, EvalIndexArgs(op.Args!, v));
                    cur = sub.Data.ToList();
                    break;
                }
                case '.':
                {
                    var name = FieldName(op);
                    if (v is MapValue mp)
                    {
                        Value[] margs = Array.Empty<Value>();
                        if (!last && ops[k + 1].Kind == '(' && name is "keys" or "values" or "isKey" or "remove" or "length")
                        { margs = EvalArgs(ops[k + 1].Args!); k++; }
                        cur = name switch
                        {
                            "Count" => new List<Value> { MatrixValue.Scalar(mp.Count) },
                            "KeyType" => new List<Value> { MatrixValue.Str(mp.KeyType) },
                            "ValueType" => new List<Value> { MatrixValue.Str(mp.ValueType) },
                            _ => CallFunction(name, new Value[] { mp }.Concat(margs).ToArray(), 1).ToList()
                        };
                        break;
                    }
                    if (v is not StructValue sv)
                        throw new OctaveException(v is MatrixValue mm && mm.IsScalar ? "scalar cannot be indexed with ." : v is MatrixValue ? "matrix cannot be indexed with ." : $"'{v.ClassName}' object cannot be indexed with .");
                    if (!sv.HasField(name)) throw new OctaveException($"invalid use of undefined value");
                    cur = sv.Elems.Select(el => el[name]).ToList();
                    break;
                }
            }
        }
        return cur;
    }

    // ------------------------------------------------------------ calls
    public Value[] CallFunction(string name, Value[] args, int nargout, UserFunction? context = null)
    {
        context ??= Current.Func;
        var uf = ResolveUser(name, context);
        if (uf != null)
        {
            if (uf.IsScript)
            {
                if (args.Length > 0) throw new OctaveException($"invalid use of script {uf.File} in index expression");
                ExecScript(uf);
                return Array.Empty<Value>();
            }
            return CallUser(uf, args, nargout);
        }
        if (Builtins.TryGetValue(name, out var bf)) return bf(this, args, nargout);
        throw new OctaveException($"'{name}' undefined", "Octave:undefined-function");
    }

    public bool FunctionExists(string name, out bool builtin)
    {
        builtin = false;
        if (ResolveUser(name, Current.Func) != null) return true;
        builtin = Builtins.ContainsKey(name);
        return builtin;
    }

    UserFunction? ResolveUser(string name, UserFunction? context)
    {
        for (var c = context; c != null; c = c.Parent)
        {
            if (c.Nested.TryGetValue(name, out var nf)) return nf;
        }
        if (context != null && context.Siblings.TryGetValue(name, out var sib)) return sib;
        if (CommandLineFunctions.TryGetValue(name, out var cl)) return cl;
        return FindOnPath(name);
    }

    public string? FindFile(string name)
    {
        foreach (var dir in new[] { Directory.GetCurrentDirectory() }.Concat(SearchPath))
        {
            var p = Path.Combine(dir, name + ".m");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    UserFunction? FindOnPath(string name)
    {
        if (missingCache.Contains(name)) return null;
        var p = FindFile(name);
        if (p == null) { missingCache.Add(name); return null; }
        return LoadFile(p, name);
    }

    public UserFunction LoadFile(string path, string name)
    {
        var mtime = File.GetLastWriteTimeUtc(path);
        if (fileCache.TryGetValue(path, out var c) && c.mtime == mtime) return c.fn;
        var unit = Parser.ParseProgram(File.ReadAllText(path));
        UserFunction fn;
        if (unit.IsFunctionFile)
        {
            var defs = unit.Functions.Select(d => UserFunction.From(d, path)).ToList();
            var sibs = defs.ToDictionary(d => d.Name, d => d);
            foreach (var d in defs) d.SetSiblings(sibs);
            fn = defs[0];
        }
        else
        {
            fn = new UserFunction { Name = name, Body = unit.Statements, File = path, IsScript = true };
            foreach (var d in unit.Functions) fn.Siblings[d.Name] = UserFunction.From(d, path);
        }
        fileCache[path] = (mtime, fn);
        return fn;
    }

    void ExecScript(UserFunction script)
    {
        foreach (var kv in script.Siblings) CommandLineFunctions[kv.Key] = kv.Value;
        var saveLine = Current.Line; var saveName = Current.FrameName;
        Current.FrameName = script.Name; Current.Line = 0;
        try { ExecBlock(script.Body); }
        catch (Exception ex) when (ex is not ControlException)
        {
            var oe = AddFrame(ex, Current);
            if (!ReferenceEquals(oe, ex)) throw oe;
            throw;
        }
        finally { Current.Line = saveLine; Current.FrameName = saveName; }
    }

    public Value[] CallUser(UserFunction f, Value[] args, int nargout)
    {
        bool hasVarargin = f.Params.Count > 0 && f.Params[^1] == "varargin";
        int fixedIn = hasVarargin ? f.Params.Count - 1 : f.Params.Count;
        if (!hasVarargin && args.Length > f.Params.Count)
            throw new OctaveException($"{f.Name}: function called with too many inputs", "Octave:invalid-fun-call");
        bool hasVarargout = f.Outputs.Count > 0 && f.Outputs[^1] == "varargout";
        if (!hasVarargout && nargout > Math.Max(1, f.Outputs.Count))
            throw new OctaveException($"{f.Name}: function called with too many outputs", "Octave:invalid-fun-call");
        if (depth >= MaxRecursion)
            throw new OctaveException("max_recursion_depth exceeded", "Octave:recursion-depth");

        var scope = new Scope { Func = f, Nargin = args.Length, Nargout = nargout, FrameName = f.Name, Line = f.Line };
        if (f.Parent != null)
        {
            for (int k = CallStack.Count - 1; k >= 0; k--)
                if (CallStack[k].Func == f.Parent) { scope.ParentScope = CallStack[k]; break; }
            if (scope.ParentScope == null && Current.Func == f.Parent) scope.ParentScope = Current;
        }
        for (int k = 0; k < Math.Min(fixedIn, args.Length); k++)
            if (f.Params[k] != "~") scope.Vars[f.Params[k]] = args[k];
        if (hasVarargin)
            scope.Vars["varargin"] = CellValue.RowOf(args.Skip(fixedIn));

        var save = Current; var saveEnd = endStack.ToList();
        Current = scope; depth++; CallStack.Add(scope);
        endStack.Clear();
        try { ExecBlock(f.Body); }
        catch (Exception ex) when (ex is not ControlException)
        {
            var oe = AddFrame(ex, scope);
            if (!ReferenceEquals(oe, ex)) throw oe;
            throw;
        }
        finally { Current = save; depth--; CallStack.RemoveAt(CallStack.Count - 1); endStack.Clear(); endStack.AddRange(saveEnd); }

        var outs = new List<Value>();
        int want = Math.Max(1, nargout);
        for (int k = 0; k < f.Outputs.Count && outs.Count < want; k++)
        {
            var on = f.Outputs[k];
            if (on == "varargout" && k == f.Outputs.Count - 1)
            {
                if (scope.Vars.TryGetValue("varargout", out var vo) && vo is CellValue vc)
                    outs.AddRange(vc.Data.Take(want - outs.Count));
                break;
            }
            Value? v = scope.Redirect.TryGetValue(on, out var st) ? (st.TryGetValue(on, out var gv) ? gv : null)
                     : scope.Vars.TryGetValue(on, out var lv) ? lv : null;
            if (v == null)
            {
                if (k < nargout || (k == 0 && nargout == 1 && false))
                    throw new OctaveException($"value on right hand side of assignment is undefined; '{on}' undefined in function '{f.Name}'");
                break;
            }
            outs.Add(v);
        }
        if (nargout > outs.Count && nargout > 1)
            throw new OctaveException($"{f.Name}: some elements undefined in return list");
        return outs.ToArray();
    }

    public Value[] CallHandle(FunctionHandleValue fh, Value[] args, int nargout)
    {
        if (!fh.IsAnonymous) return CallFunction(fh.Name!, args, nargout, fh.Context);
        var pars = fh.Params!;
        bool hasVarargin = pars.Count > 0 && pars[^1] == "varargin";
        int fixedIn = hasVarargin ? pars.Count - 1 : pars.Count;
        if (!hasVarargin && args.Length > pars.Count)
            throw new OctaveException($"@<anonymous>: function called with too many inputs");
        if (depth >= MaxRecursion) throw new OctaveException("max_recursion_depth exceeded");
        var scope = new Scope { Func = fh.Context, Nargin = args.Length, Nargout = nargout };
        foreach (var kv in fh.Captured!) scope.Vars[kv.Key] = kv.Value;
        for (int k = 0; k < Math.Min(fixedIn, args.Length); k++) if (pars[k] != "~") scope.Vars[pars[k]] = args[k];
        if (hasVarargin) scope.Vars["varargin"] = CellValue.RowOf(args.Skip(fixedIn));
        var save = Current; var saveEnd = endStack.ToList();
        Current = scope; depth++; endStack.Clear();
        try { return EvalMulti(fh.Body!, nargout).ToArray(); }
        finally { Current = save; depth--; endStack.Clear(); endStack.AddRange(saveEnd); }
    }

    /// <summary>Call something that may be a handle, or a function name string.</summary>
    public Value[] CallCallable(Value f, Value[] args, int nargout)
    {
        if (f is FunctionHandleValue fh) return CallHandle(fh, args, nargout);
        if (f is MatrixValue m && m.IsChar) return CallFunction(m.AsString(), args, nargout);
        throw new OctaveException("expected a function handle or function name");
    }

    public void Quit(int code) => throw new ControlException { Quit = true, Code = code };
    public static bool IsQuit(Exception ex, out int code)
    {
        if (ex is ControlException ce && ce.Quit) { code = ce.Code; return true; }
        code = 0; return false;
    }
}
