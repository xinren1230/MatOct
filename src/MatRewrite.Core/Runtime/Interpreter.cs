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
    public List<Expr?>? Defaults;
    public List<string> Outputs = new();
    public List<Stmt> Body = new();
    public string? File;
    public bool IsScript;
    public Dictionary<string, UserFunction> Siblings = new(); // subfunctions in the same file
    public Dictionary<string, Value> Persistents = new();
    public UserFunction? Parent;                                // for nested functions
    public Dictionary<string, UserFunction> Nested = new();
    public int Line;
    public ClassInfo? Class;        // set on the constructor stub of a classdef file
    public ClassInfo? OwnerClass;   // set on class methods

    public static UserFunction From(FunctionDef d, string? file, UserFunction? parent = null)
    {
        var f = new UserFunction { Defaults = d.Defaults, Name = d.Name, Params = d.Params, Outputs = d.Outputs, Body = d.Body, File = file, Parent = parent, Line = d.Line };
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
    /// <summary>Folders that restoredefaultpath puts back (after userpath). Hosts may add toolbox folders.</summary>
    public readonly List<string> DefaultPath = new();
    /// <summary>Raised after addpath/rmpath/path/cd change what resolves.</summary>
    public event Action? PathChanged;
    /// <summary>Set by the IDE so pathtool opens the Set Path dialog.</summary>
    public Action? PathToolRequested;
    public void NotifyPathChanged() { missingCache.Clear(); PathChanged?.Invoke(); }
    /// <summary>Forget negative lookups so new files on the path are found (MATLAB rehash).</summary>
    public void Rehash() { missingCache.Clear(); fileCache.Clear(); }
    public string LastError = "";
    public string LastErrorId = "";
    public UserFunction? FindUserFunction(string name) => ResolveUser(name, Current.Func);
    public Random Rng = new MtRandom();
    public Random RngN = new MtRandom();
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
        InputReader = DefaultReader;
        ObjectArrayValue.DefaultFactory = ci => NewObject(ci);
    }

    public void Warning(string msg) => Warning(msg, "");
    public string LastWarning = "", LastWarningId = "";
    public readonly HashSet<string> DisabledWarnings = new(StringComparer.Ordinal);
    public void Warning(string msg, string id)
    {
        if (DisabledWarnings.Count > 0 && (DisabledWarnings.Contains("all") ? !EnabledWarnings.Contains(id ?? "") : (id != null && id != "" && DisabledWarnings.Contains(id)))) return;
        LastWarning = msg; LastWarningId = id ?? "";
        Out.Flush(); Out.WriteLine("warning: " + msg);
    }
    public readonly HashSet<string> EnabledWarnings = new(StringComparer.Ordinal);

    // ------------------------------------------------------------ public API
    /// <summary>Execute code in the base workspace (like typing at the prompt).</summary>
    public void Eval(string code)
    {
        missingCache.Clear();
        var unit = Parser.ParseProgram(code);
        foreach (var f in unit.Functions) CommandLineFunctions[f.Name] = UserFunction.From(f, null);
        var saveScope = Current;
        if (!IsPaused) { fileStack.Clear(); interruptRequested = false; step = StepMode.None; }
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
        if (!IsPaused) { fileStack.Clear(); interruptRequested = false; step = StepMode.None; }
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
        var r = LookupRaw(sc, name);
        if (r is ArrayValue xm) xm.Exclusive = false;
        return r;
    }

    static Value? LookupRaw(Scope sc, string name)
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
        BeforeStatement(s);
        return UseCompiler ? CompiledStmt(s)(this) : ExecTree(s);
    }

    internal Flow ExecTree(Stmt s)
    {
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
                    CheckInterrupt();
                    var fl = ExecBlock(w.Body);
                    if (fl == Flow.Break) break;
                    if (fl == Flow.Return) return fl;
                }
                return Flow.Normal;
            case DoUntilStmt d:
                do
                {
                    CheckInterrupt();
                    var fl = ExecBlock(d.Body);
                    if (fl == Flow.Break) break;
                    if (fl == Flow.Return) return fl;
                } while (!Ops.IsTrue(Eval(d.Cond)));
                return Flow.Normal;
            case SwitchStmt sw: return ExecSwitch(sw);
            case TryStmt t:
                try { return ExecBlock(t.Body); }
                catch (Exception ex) when (ex is not ControlException && ex is not InterruptException)
                {
                    var (msg, id) = ErrorInfo(ex);
                    LastError = msg; LastErrorId = id ?? "";
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
        var oe = ex as OctaveException ?? new OctaveException(ex.Message, "Octave:internal") { Inner = ex };
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
        if (a.Targets.Count == 1 && !(a.Targets[0] is FieldExpr { Target: IdentExpr } && ExpandCsListTargets(a.Targets).Count != 1) && !(a.Targets[0] is IndexExpr { Brace: true, Target: IdentExpr, Args.Count: 1 } bt && bt.Args[0] is not (NumberExpr or EndExpr) && ExpandCsListTargets(a.Targets).Count != 1))
        {
            var vals = EvalMulti(a.Value, 1);
            if (vals.Count == 0) throw new OctaveException("value on right hand side of assignment is undefined");
            var name = AssignTo(a.Targets[0], vals[0]);
            if (a.Print) Out.Write(Display.Show(name, LookupVar(name)!));
            return;
        }
        var targets = ExpandCsListTargets(a.Targets);
        int nout = targets.Count;
        var results = EvalMulti(a.Value, nout);
        for (int k = 0; k < nout; k++)
        {
            if (targets[k] is TildeExpr) continue;
            if (k >= results.Count)
            {
                if (k == 0) throw new OctaveException("value on right hand side of assignment is undefined");
                throw new OctaveException($"element number {k + 1} undefined in return list");
            }
            var name = AssignTo(targets[k], results[k]);
            if (a.Print) Out.Write(Display.Show(name, LookupVar(name)!));
        }
    }

    /// <summary>[c{:}] = f() / [c{1:3}] = f(): one output per addressed cell element.</summary>
    List<Expr> ExpandCsListTargets(List<Expr> targets)
    {
        if (!targets.Any(t => t is IndexExpr { Brace: true, Target: IdentExpr, Args.Count: 1 } or FieldExpr { Target: IdentExpr })) return targets;
        var r = new List<Expr>();
        foreach (var t in targets)
        {
            // [s.name] = ... on a struct array: one output per element
            if (t is FieldExpr { Target: IdentExpr fid } fe && LookupRaw(Current, fid.Name) is StructValue fsv && fsv.Numel != 1)
            {
                for (int q = 1; q <= fsv.Numel; q++)
                    r.Add(new FieldExpr(new IndexExpr(fid, new List<Expr> { new NumberExpr(q, false, q.ToString()) }, false) { Line = fe.Line }, fe.Name, fe.DynName) { Line = fe.Line });
                continue;
            }
            if (t is IndexExpr { Brace: true, Target: IdentExpr id, Args.Count: 1 } ix)
            {
                var cur = LookupRaw(Current, id.Name);   // raw: must not clear the in-place ownership flag
                double[]? idx = null;
                if (ix.Args[0] is ColonExpr) { if (cur is ArrayValue av) idx = Enumerable.Range(1, av.Numel).Select(i => (double)i).ToArray(); }
                else if (ix.Args[0] is not (NumberExpr or EndExpr))
                {
                    var iv = EvalIndexArgs(ix.Args, cur ?? new CellValue(0, 0))[0] as MatrixValue;
                    if (iv != null && !iv.IsChar)
                        idx = iv.Kind == NumKind.Logical ? Enumerable.Range(0, iv.Numel).Where(i => iv.Re[i] != 0).Select(i => (double)(i + 1)).ToArray() : iv.Re.ToArray();
                }
                if (idx != null && idx.Length != 1)
                {
                    foreach (var d in idx) r.Add(new IndexExpr(ix.Target, new List<Expr> { new NumberExpr(d, false, d.ToString(System.Globalization.CultureInfo.InvariantCulture)) }, true) { Line = ix.Line });
                    continue;
                }
            }
            r.Add(t);
        }
        return r;
    }

    Flow ExecFor(ForStmt f)
    {
        if (f.Var is MatrixExpr mvx && mvx.Rows.Count == 1 && mvx.Rows[0].Count == 2)
        {
            if (Eval(f.Range) is not StructValue fs) throw new OctaveException("in statement 'for [X, Y] = VAL', VAL must be a structure");
            foreach (var key in fs.Fields.ToList())
            {
                Value val = fs.Numel == 1 ? fs.Get(key) : new CellValue(fs.R, fs.C, fs.Elems.Select(e => e[key]).ToArray());
                AssignTo(mvx.Rows[0][0], val);
                AssignTo(mvx.Rows[0][1], MatrixValue.Str(key));
                CheckInterrupt(); var fl = ExecBlock(f.Body);
                if (fl == Flow.Break) break;
                if (fl == Flow.Return) return fl;
            }
            return Flow.Normal;
        }
        if (f.Range is RangeExpr re)
        {
            var (start, step, n) = RangeParams(re);
            for (long k = 0; k < n; k++)
            {
                AssignTo(f.Var, MatrixValue.Scalar(start + k * step));
                CheckInterrupt(); var fl = ExecBlock(f.Body);
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
            CheckInterrupt(); var fl = ExecBlock(f.Body);
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
        var cur = LookupRaw(Current, id.Name);
        if (cur is MatrixValue cm && cm.Exclusive && ops.Count == 1 && TryFastScalarAssign(cm, ops[0], rhs)) return id.Name;
        if (cur is CellValue cc && cc.Exclusive && ops.Count == 1 && TryFastCellAssign(cc, ops[0], rhs)) return id.Name;
        if (cur is ArrayValue cm2) cm2.Exclusive = false;
        var res = AssignChain(cur, ops, 0, rhs);
        bool fresh = res is MatrixValue rm0 ? !(rhs is MatrixValue rhm && ReferenceEquals(rhm.Re, rm0.Re)) && !(cur is MatrixValue cm3 && ReferenceEquals(cm3.Re, rm0.Re))
                   : res is CellValue rc0 && !(rhs is CellValue rhc && ReferenceEquals(rhc.Data, rc0.Data)) && !(cur is CellValue cc3 && ReferenceEquals(cc3.Data, rc0.Data));
        if (fresh && res is ArrayValue rm && !ReferenceEquals(res, rhs) && !ReferenceEquals(res, cur)
            && !Current.Redirect.ContainsKey(id.Name) && Current.Vars.ContainsKey(id.Name))
        {
            StoreVar(id.Name, res);
            rm.Exclusive = ReferenceEquals(Current.Vars.GetValueOrDefault(id.Name), res);
        }
        else StoreVar(id.Name, res);
        return id.Name;
    }

    /// <summary>In-place c{i} = x (in range, or appending to a row/empty cell) for an exclusively owned cell.</summary>
    bool TryFastCellAssign(CellValue c, Op op, Value rhs)
    {
        if (op.Kind != '{' || op.Args == null || op.Args.Count != 1 || c.ND != null) return false;
        if (op.Args[0] is not (NumberExpr or IdentExpr or BinaryExpr or ValueExpr)) return false;
        if (rhs is ObjectValue) return false;
        var args = EvalIndexArgs(op.Args, c);
        if (args[0] is not MatrixValue i || i.Numel != 1 || i.Kind == NumKind.Logical || i.Im != null) return false;
        double d = i.Re[0]; if (d < 1 || d != Math.Floor(d)) return false;
        int n = c.Numel;
        if (d <= n) { c.RawData[(int)d - 1] = rhs; return true; }
        if (d == n + 1 && (c.R == 1 || n == 0))
        {
            return c.TryAppend(rhs);
        }
        return false;
    }

    /// <summary>In-place v(i) = s / v(i,j) = s for an exclusively owned real double matrix (hot-loop fast path).</summary>
    bool TryFastScalarAssign(MatrixValue m, Op op, Value rhs)
    {
        if (op.Kind != '(' || op.Args == null || op.Args.Count is < 1 or > 2) return false;
        if (m.Kind != NumKind.Double || m.HasIm || m.ND != null || m.DoubleQuoted) return false;
        if (rhs is not MatrixValue r || r.Numel != 1 || r.Kind != NumKind.Double || r.HasIm) return false;
        foreach (var a in op.Args) if (a is not (NumberExpr or IdentExpr or BinaryExpr or ValueExpr)) return false;
        // evaluate subscripts with the matrix as 'end' context
        var args = EvalIndexArgs(op.Args, m);
        int lin;
        if (args.Length == 1)
        {
            if (args[0] is not MatrixValue i || i.Numel != 1 || i.Kind == NumKind.Logical || i.Im != null) return false;
            double d = i.Re[0];
            if (d == m.Numel + 1 && d == Math.Floor(d)) return m.TryAppend(r.RawRe[0]);
            if (d < 1 || d > m.Numel || d != Math.Floor(d)) return false;
            lin = (int)d - 1;
        }
        else
        {
            if (args[0] is not MatrixValue i || args[1] is not MatrixValue j || i.Numel != 1 || j.Numel != 1
                || i.Kind == NumKind.Logical || j.Kind == NumKind.Logical || i.Im != null || j.Im != null) return false;
            double di = i.Re[0], dj = j.Re[0];
            if (di < 1 || dj < 1 || di > m.R || dj > m.C || di != Math.Floor(di) || dj != Math.Floor(dj)) return false;
            lin = (int)di - 1 + ((int)dj - 1) * m.R;
        }
        m.RawRe[lin] = r.RawRe[0];
        return true;
    }

    /// <summary>Argument variable names of the last table()/timetable() call (for default VariableNames).</summary>
    public string[]? CallArgNames;

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
                if (cur is StructValue { Tag: not null } tas && Tagged.DotAssign(tas, name, old => AssignChain(old, ops, k + 1, rhs)) is Value tar) return tar;
                if (cur is ObjectValue ob)
                    return WriteProp(ob, name, old => AssignChain(old, ops, k + 1, rhs), !last);
                if (cur is ObjectArrayValue oarr)
                {
                    if (oarr.Numel != 1) throw new OctaveException("invalid use of a N-D object array in indexed assignment");
                    return WriteProp(oarr.At(0), name, old => AssignChain(old, ops, k + 1, rhs), !last);
                }
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
                if (cur is StructValue { Tag: not null } tpa)
                {
                    var targs = EvalIndexArgs(op.Args!, tpa);
                    var trhs = last ? rhs : AssignChain(Tagged.Paren(tpa, targs), ops, k + 1, rhs);
                    return Tagged.ParenAssign(this, tpa, targs, trhs, false);
                }
                if (cur is MapValue map)
                {
                    var ka = EvalArgs(op.Args!);
                    if (ka.Length != 1) throw new OctaveException("only '()' indexing with exactly one key is supported by containers.Map");
                    Value? old = map.Has(ka[0]) ? map.Get(ka[0]) : null;
                    map.Set(ka[0], last ? rhs : AssignChain(old, ops, k + 1, rhs));
                    return map;
                }
                if (cur is Sparse.SparseValue spc)
                {
                    var sargs = EvalIndexArgs(op.Args!, spc);
                    Value srhs = rhs;
                    if (!last)
                    {
                        Value? old = null;
                        try { old = Sparse.SparseIndex.Read(spc, sargs); } catch (OctaveException) { }
                        srhs = AssignChain(old, ops, k + 1, rhs);
                    }
                    if (srhs is Sparse.SparseValue sr2) srhs = sr2.ToDense();
                    if (srhs is not ArrayValue sra) throw new OctaveException($"operator = undefined for 'sparse matrix' by '{srhs.ClassName}' operations");
                    return Sparse.SparseIndex.Assign(spc, sargs, sra);
                }
                if (rhs is Sparse.SparseValue rsp && last) rhs = rsp.ToDense();
                if (last && rhs is ObjectValue robj) rhs = ObjectArrayValue.Of(robj);
                if (cur is ObjectValue cobj) cur = ObjectArrayValue.Of(cobj);
                var curArr = cur as ArrayValue;
                var args = EvalIndexArgs(op.Args!, cur ?? MatrixValue.Empty());
                if (last)
                {
                    if (rhs is not ArrayValue ra)
                    {
                        if (curArr == null && args.All(a => a is MatrixValue m && m.Numel == 1 && m.Re[0] == 1)) return rhs;
                        throw new OctaveException($"operator = undefined for '{cur?.ClassName ?? "matrix"}' by '{rhs.ClassName}' operations");
                    }
                    return SimplifyObj(Indexing.Assign(curArr, args, ra));
                }
                Value? elem = null;
                if (curArr != null && curArr.Numel > 0)
                {
                    try { var e = Indexing.Read(curArr, args); if (e.Numel == 1) elem = e is ObjectArrayValue eo ? eo.At(0) : e; } catch (OctaveException) { }
                }
                if (elem == null && ops[k + 1].Kind == '.') elem = new StructValue();
                var newElem = AssignChain(elem, ops, k + 1, rhs);
                if (newElem is ObjectValue neo) newElem = ObjectArrayValue.Of(neo);
                if (newElem is not ArrayValue na) throw new OctaveException("invalid indexed assignment");
                if (curArr is MatrixValue cm && cm.Numel == 0) curArr = null;
                return SimplifyObj(Indexing.Assign(curArr, args, na));
            }
            case '{':
            {
                if (cur is StructValue { Tag: not null } tba)
                {
                    var targs = EvalIndexArgs(op.Args!, tba);
                    var trhs = last ? rhs : AssignChain(Tagged.Brace(tba, targs), ops, k + 1, rhs);
                    return Tagged.ParenAssign(this, tba, targs, trhs, true);
                }
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
    public Value Eval(Expr e) => UseCompiler ? Compiled(e)(this) : EvalTree(e);

    internal Value EvalTree(Expr e)
    {
        switch (e)
        {
            case ValueExpr ve: return ve.Value;
            case NumberExpr n: return n.Imag ? MatrixValue.Cplx(0, n.Value) : MatrixValue.Scalar(n.Value);
            case StringExpr s: return MatrixValue.Str(s.Value, s.DoubleQuoted);
            case BinaryExpr b: return EvalBinary(b);
            case UnaryExpr u: return Ops.Unary(u.Op, Eval(u.Operand));
            case ParenExpr p: return Eval(p.Inner);
            case MultiAssignExpr me:
            {
                Exec(new AssignStmt(me.Targets, me.Value, false));
                var first = me.Targets.FirstOrDefault(t => t is not TildeExpr);
                return first == null ? MatrixValue.Empty() : Eval(first);
            }
            case AssignExpr ae:
            {
                var old = ae.PostOld ? Eval(ae.Target) : null;
                var v = Eval(ae.Value);
                AssignTo(ae.Target, v);
                return old ?? v;
            }
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
        var lv2 = Eval(b.Left); var rv2 = Eval(b.Right);
        if (lv2 is ObjectValue || rv2 is ObjectValue || lv2 is ObjectArrayValue || rv2 is ObjectArrayValue) return ObjectBinary(b.Op, lv2, rv2);
        return Ops.Binary(b.Op, lv2, rv2);
    }

    static readonly Dictionary<string, string> OpMethods = new()
    {
        ["+"] = "plus", ["-"] = "minus", [".*"] = "times", ["*"] = "mtimes", ["./"] = "rdivide", ["/"] = "mrdivide",
        [".^"] = "power", ["^"] = "mpower", ["=="] = "eq", ["!="] = "ne", ["~="] = "ne", ["<"] = "lt", ["<="] = "le",
        [">"] = "gt", [">="] = "ge", ["&"] = "and", ["|"] = "or", ["\\"] = "mldivide"
    };

    Value ObjectBinary(string op, Value l, Value r)
    {
        if (OpMethods.TryGetValue(op, out var mname))
        {
            foreach (var o in new[] { l, r })
                if (o is ObjectValue ov && ov.Cls.FindMethod(mname, out _) is UserFunction mf)
                {
                    var res = CallUser(mf, new[] { l, r }, 1);
                    if (res.Length == 0) throw new OctaveException($"{mname}: method returned no value");
                    return res[0];
                }
            if (mname is "eq" or "ne" && l is ObjectValue ea && ea.EnumName != null)
            {
                bool eq = r switch
                {
                    ObjectValue eb => eb.Cls == ea.Cls && eb.EnumName == ea.EnumName,
                    MatrixValue es when es.IsChar => es.AsString() == ea.EnumName,
                    _ => false
                };
                return MatrixValue.Bool(eq == (mname == "eq"));
            }
            if (mname is "eq" or "ne" && r is ObjectValue ra2 && ra2.EnumName != null) return ObjectBinary(op, r, l);
            if (mname is "eq" or "ne" && l is ObjectValue a && r is ObjectValue b2 && a.Cls.IsHandle)
                return MatrixValue.Bool(ReferenceEquals(a, b2) == (mname == "eq"));
        }
        if ((op == "==" || op == "!=" || op == "~=") && l is FunctionHandleValue fl && r is FunctionHandleValue fr)
        {
            bool same = ReferenceEquals(fl, fr) || fl.Name != null && fl.Name == fr.Name && fl.Body == null && fr.Body == null;
            return MatrixValue.Bool(same == (op == "=="));
        }
        throw new OctaveException($"binary operator '{op}' not implemented for '{l.ClassName}' by '{r.ClassName}' operations");
    }

    static Value SimplifyObj(Value v) => v is ObjectArrayValue oa ? oa.Simplify() : v;

    public static bool EmptyLogical = Environment.GetEnvironmentVariable("MATRW_EMPTY_TRUE") == "1";
    static bool LogicalScalar(Value v, string op)
    {
        if (v is MatrixValue m)
        {
            if (m.Numel == 0) return EmptyLogical;
            return Ops.IsTrue(m);
        }
        if (v is MatRewrite.Core.Sparse.SparseValue sv) return LogicalScalar(sv.ToDense(), op);
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
                if (v is Sparse.SparseValue spt) return new List<Value> { spt.Transpose(pf.Op == "'") };
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
            case AssignExpr: case MultiAssignExpr: return new List<Value> { Eval(e) };
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
            case AssignExpr ae: foreach (var x in CollectIdents(ae.Target).Concat(CollectIdents(ae.Value))) yield return x; break;
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
            var v = ops.Count > 0 && ops[0].Kind is '(' or '{' ? LookupRaw(Current, id.Name) : LookupVar(id.Name);
            if (v is ArrayValue vx && vx.Exclusive && !(ops.Count == 1 && ops[0].Args != null && ops[0].Args!.All(a => a is NumberExpr or IdentExpr or BinaryExpr))) vx.Exclusive = false;
            if (v != null) cur = new List<Value> { v };
            else if (id.Name == "containers" && ops.Count > 0 && ops[0].Kind == '.' && ops[0].Field == "Map")
            {
                Value[] margs = Array.Empty<Value>();
                start = 1;
                if (ops.Count > 1 && ops[1].Kind == '(') { margs = EvalArgs(ops[1].Args!); start = 2; }
                cur = CallFunction("containers.Map", margs, 1).ToList();
                if (start == ops.Count) return cur;
            }
            else if (ops.Count > 0 && ops[0].Kind == '.' && ops[0].Field != null && ResolveUser(id.Name, Current.Func) == null && !Builtins.ContainsKey(id.Name)
                     && FindPackageFunction(id.Name, ops, out int pk) is UserFunction pkf)
            {
                // package function: pkg.sub.fn(args)
                start = pk + 1;
                Value[] pargs = Array.Empty<Value>();
                if (start < ops.Count && ops[start].Kind == '(') { pargs = EvalArgs(ops[start].Args!); start++; }
                cur = (pkf.Class != null ? Construct(pkf.Class, pargs, 1) : CallUser(pkf, pargs, start == ops.Count ? nargout : 1)).ToList();
                if (start == ops.Count) return cur;
            }
            else if (ops.Count > 0 && ops[0].Kind == '.' && ops[0].Field != null && !Builtins.ContainsKey(id.Name) && FindClassQuiet(id.Name) is ClassInfo sci
                     && (sci.Static.Contains(ops[0].Field!) || sci.Chain().Any(c => c.Constants.ContainsKey(ops[0].Field!) || c.Enums.Any(en => en.Name == ops[0].Field) || c.Static.Contains(ops[0].Field!))))
            {
                var mname = ops[0].Field!;
                start = 1;
                if (sci.Chain().Any(c => c.Enums.Any(en => en.Name == mname))) cur = new List<Value> { EnumMember(sci, mname) };
                else if (sci.FindMethod(mname, out var smOwner) is UserFunction smf && smOwner!.Static.Contains(mname))
                {
                    if (sci.MethodAccess.TryGetValue(mname, out var sacc) && !CanAccess(smOwner, sacc))
                        throw new OctaveException($"{sci.Name}: method '{mname}' has {sacc} access and cannot be called from this context");
                    Value[] sargs = Array.Empty<Value>();
                    if (ops.Count > 1 && ops[1].Kind == '(') { sargs = EvalArgs(ops[1].Args!); start = 2; }
                    cur = CallUser(smf, sargs, start == ops.Count ? nargout : 1).ToList();
                }
                else if (sci.Static.Contains(mname))
                {
                    Value[] sargs = Array.Empty<Value>();
                    if (ops.Count > 1 && ops[1].Kind == '(') { sargs = EvalArgs(ops[1].Args!); start = 2; }
                    cur = CallUser(sci.Methods[mname], sargs, start == ops.Count ? nargout : 1).ToList();
                }
                else cur = new List<Value> { sci.Chain().First(c => c.Constants.ContainsKey(mname)).Constants[mname] };
                if (start == ops.Count) return cur;
            }
            else if (id.Name == "nargin" && Current.Nargin >= 0 && ops.Count == 0) return new List<Value> { MatrixValue.Scalar(Current.Nargin) };
            else if (id.Name == "nargout" && Current.Nargout >= 0 && ops.Count == 0) return new List<Value> { MatrixValue.Scalar(Current.Nargout) };
            else
            {
                Value[] args = Array.Empty<Value>();
                if (ops.Count > 0 && ops[0].Kind == '(') { args = EvalArgs(ops[0].Args!); start = 1; if (id.Name is "table" or "timetable") CallArgNames = ops[0].Args!.Select(e => e is IdentExpr ie ? ie.Name : "").ToArray(); }
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
                    if (v is ObjectValue && op.Args!.Count == 1 && EvalArgs(op.Args) is var oa && oa.Length == 1 && oa[0] is MatrixValue om && om.Numel == 1 && om.Re[0] == 1)
                    { cur = new List<Value> { v }; break; }
                    if (v is Sparse.SparseValue spv) { cur = new List<Value> { Sparse.SparseIndex.Read(spv, EvalIndexArgs(op.Args!, v)) }; break; }
                    if (v is StructValue { Tag: not null } tsv) { cur = new List<Value> { Tagged.Paren(tsv, EvalIndexArgs(op.Args!, v)) }; break; }
                    if (v is not ArrayValue av) throw new OctaveException($"'{v.ClassName}' object cannot be indexed with (");
                    cur = new List<Value> { SimplifyObj(Indexing.Read(av, EvalIndexArgs(op.Args!, v))) };
                    break;
                case '{':
                {
                    if (v is MapValue) throw new OctaveException("containers.Map: only '()' indexing is supported by containers.Map");
                    if (v is StructValue { Tag: not null } tbv) { cur = new List<Value> { Tagged.Brace(tbv, EvalIndexArgs(op.Args!, v)) }; break; }
                    if (v is not CellValue cv)
                        throw new OctaveException(v is FunctionHandleValue ? "'{' undefined for arguments of type 'function_handle'" : $"'{{' undefined for arguments of type '{v.ClassName}'; matrix cannot be indexed with {{");
                    var sub = (CellValue)Indexing.Read(cv, EvalIndexArgs(op.Args!, v));
                    cur = sub.Data.ToList();
                    break;
                }
                case '.':
                {
                    var name = FieldName(op);
                    if (v is StructValue { Tag: not null } tdv && Tagged.Dot(tdv, name, out var tdr)) { cur = new List<Value> { tdr }; break; }
                    if (v is MapValue mp)
                    {
                        Value[] margs = Array.Empty<Value>();
                        if (!last && ops[k + 1].Kind == '(' && name is "keys" or "values" or "isKey" or "remove" or "length")
                        { margs = EvalArgs(ops[k + 1].Args!); k++; }
                        cur = name switch
                        {
                            "Count" => new List<Value> { MatrixValue.Scalar(mp.Count).CastInPlace(NumKind.UInt64) },
                            "KeyType" => new List<Value> { MatrixValue.Str(mp.KeyType) },
                            "ValueType" => new List<Value> { MatrixValue.Str(mp.ValueType) },
                            "keys" or "values" or "isKey" or "remove" or "length" => CallFunction(name, new Value[] { mp }.Concat(margs).ToArray(), 1).ToList(),
                            _ => throw new OctaveException($"containers.Map: unknown property '{name}'")
                        };
                        break;
                    }
                    if (v is ObjectArrayValue oav)
                    {
                        var am = oav.Cls.FindMethod(name, out _);
                        if (am != null && oav.Cls.FindProp(name, out _) == null)
                        {
                            Value[] margs = Array.Empty<Value>();
                            if (!last && ops[k + 1].Kind == '(') { margs = EvalArgs(ops[k + 1].Args!); k++; }
                            cur = CallUser(am, new Value[] { oav }.Concat(margs).ToArray(), k == ops.Count - 1 ? nargout : 1).ToList();
                            break;
                        }
                        cur = Enumerable.Range(0, oav.Numel).Select(i => ReadProp(oav.At(i), name)).ToList();
                        break;
                    }
                    if (v is ObjectValue ov)
                    {
                        var meth = ov.Cls.FindMethod(name, out _);
                        if (meth != null && !ov.Props.ContainsKey(name) && ov.Cls.FindProp(name, out _) == null)
                        {
                            if (ov.Cls.MethodAccess.TryGetValue(name, out var macc) && !CanAccess(meth.OwnerClass ?? ov.Cls, macc))
                                throw new OctaveException($"{ov.Cls.Name}: method '{name}' has {macc} access and cannot be called from this context");
                            Value[] margs = Array.Empty<Value>();
                            if (!last && ops[k + 1].Kind == '(') { margs = EvalArgs(ops[k + 1].Args!); k++; }
                            bool isLast = k == ops.Count - 1;
                            var all = ov.Cls.Static.Contains(name) ? margs : new Value[] { ov }.Concat(margs).ToArray();
                            cur = CallUser(meth, all, isLast ? nargout : 1).ToList();
                            break;
                        }
                        cur = new List<Value> { ReadProp(ov, name) };
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
        if (name.Contains('@')) return CallSuper(name, args, nargout);
        if (args.Length > 0 && TryDispatchMethod(name, args, nargout, out var mres)) return mres;
        var uf = ResolveUser(name, context);
        if (uf != null && uf.Class != null) return Construct(uf.Class, args, nargout);
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
        if (Builtins.TryGetValue(name, out var bf))
        {
            try
            {
                Value[] r = (!args.Any(a => a is Sparse.SparseValue) || Sparse.SparseBridge.Aware.Contains(name))
                    ? bf(this, args, nargout)
                    : Sparse.SparseBridge.Call(name, args, a => bf(this, a, nargout));
                if (nargout > 1 && r.Length < nargout)
                {
                    // the builtin can't supply every requested output: fall back to the library m-file
                    var mf = FindOnPath(name, true);
                    if (mf != null && !mf.IsScript && mf.Class == null) return CallUser(mf, args, nargout);
                }
                return r;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                // a builtin indexed past its argument list: report it the way Octave's print_usage does
                throw new OctaveException($"Invalid call to {name}", "Octave:invalid-fun-call");
            }
            catch (InvalidCastException)
            {
                throw new OctaveException($"{name}: wrong type argument", "Octave:invalid-input-type");
            }
        }
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
        // private/ functions are visible to functions in the parent directory (and to each other)
        if (context?.File is { } cf)
        {
            var d = Path.GetDirectoryName(cf);
            if (d != null)
            {
                var pd = Path.GetFileName(d) == "private" ? d : Path.Combine(d, "private");
                var pf = Path.Combine(pd, name + ".m");
                if (PrivateExists(pf)) return LoadFile(pf, name);
            }
        }
        if (PreferBuiltins && Builtins.ContainsKey(name) && !MFileFirst.Contains(name)) return null;
        return FindOnPath(name);
    }

    /// <summary>When set, built-in functions take precedence over same-named m-files on the search path
    /// (used by the compatibility runner when Octave's own m-file library is on the path).</summary>
    public bool PreferBuiltins;

    /// <summary>Names for which Octave's m-file implementation (with its full input validation) is preferred
    /// over our builtin when the m-file library is on the path. Tuned against the %! test suite.</summary>
    public static readonly HashSet<string> MFileFirst = LoadMFileFirst();
    static HashSet<string> LoadMFileFirst()
    {
        var set = new HashSet<string>(MFileFirstDefaults.Names);
        var env = Environment.GetEnvironmentVariable("MATRW_MFILE_FIRST");
        if (env != null)
        {
            if (env.StartsWith("=")) { set.Clear(); env = env[1..]; }
            foreach (var n in env.Split(',', StringSplitOptions.RemoveEmptyEntries)) { var t = n.Trim(); if (t.StartsWith("-")) set.Remove(t[1..]); else set.Add(t); }
        }
        return set;
    }

    public string? FindFile(string name)
    {
        foreach (var dir in new[] { Directory.GetCurrentDirectory() }.Concat(SearchPath))
        {
            var p = Path.Combine(dir, name + ".m");
            if (File.Exists(p)) return p;
            var pf = Path.Combine(dir, "@" + name, name + ".m");
            if (File.Exists(pf)) return pf;
        }
        return null;
    }

    /// <summary>Resolves pkg.sub.name against +pkg/+sub/name.m on the search path; k receives the index of the op naming the function.</summary>
    UserFunction? FindPackageFunction(string root, List<Op> ops, out int k)
    {
        k = -1;
        var dirs = new List<string> { Directory.GetCurrentDirectory() };
        dirs.AddRange(SearchPath);
        var rel = "+" + root; var qual = root;
        for (int i = 0; i < ops.Count && ops[i].Kind == '.' && ops[i].Field != null; i++)
        {
            foreach (var d in dirs)
            {
                var f = Path.Combine(d, rel, ops[i].Field! + ".m");
                if (File.Exists(f)) { k = i; return LoadFile(f, ops[i].Field!); }
            }
            qual += "." + ops[i].Field;
            if (CoreMLib.GetPackage(qual) is { } psrc) { k = i; return LoadSource(psrc, "<core>/" + qual + ".m", qual); }
            rel = Path.Combine(rel, "+" + ops[i].Field);
            if (!dirs.Any(d => Directory.Exists(Path.Combine(d, rel))) && !CoreMLib.HasPackagePrefix(qual)) return null;
        }
        return null;
    }

    UserFunction? FindOnPath(string name, bool includeShadowed = false)
    {
        if (includeShadowed && FindFile(name) == null && CoreMLib.Get(name) is { } osrc)
            return LoadSource(osrc, "<core>/" + name + ".m", name);
        if (missingCache.Contains(name)) return null;
        var p = FindFile(name);
        if (p == null)
        {
            var src = CoreMLib.Get(name);
            // Octave's bundled m-files never shadow a MatOct builtin (unless tuned to prefer the m-file)
            if (src != null && CoreMLib.IsOctaveLib(name) && Builtins.ContainsKey(name) && (!MFileFirst.Contains(name) || CoreMLib.IsOctaveGraphics(name))) src = null;
            if (src != null) return LoadSource(src, "<core>/" + name + ".m", name);
            missingCache.Add(name); return null;
        }
        return LoadFile(p, name);
    }

    public UserFunction LoadFile(string path, string name)
    {
        var mtime = File.GetLastWriteTimeUtc(path);
        if (fileCache.TryGetValue(path, out var c) && c.mtime == mtime) return c.fn;
        var unit = Parser.ParseProgram(File.ReadAllText(path));
        UserFunction fn;
        if (unit.Class != null)
        {
            if (!unit.Class.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !Path.GetFileNameWithoutExtension(path).Equals(unit.Class.Name, StringComparison.OrdinalIgnoreCase))
                throw new OctaveException($"classdef '{unit.Class.Name}' must be defined in a file named {unit.Class.Name}.m");
            fn = new UserFunction { Name = unit.Class.Name, File = path };
            fileCache[path] = (mtime, fn);
            fn.Class = BuildClass(unit.Class, path, unit.Functions);
            return fn;
        }
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

    readonly Dictionary<string, UserFunction> coreLibCache = new();
    UserFunction LoadSource(string src, string path, string name)
    {
        if (coreLibCache.TryGetValue(name, out var f)) return f;
        var unit = Parser.ParseProgram(src);
        if (unit.Class != null)
        {
            var cf = new UserFunction { Name = unit.Class.Name, File = path };
            coreLibCache[name] = cf;
            cf.Class = BuildClass(unit.Class, path, unit.Functions);
            return cf;
        }
        var defs = unit.Functions.Select(d => UserFunction.From(d, path)).ToList();
        var sibs = defs.ToDictionary(d => d.Name, d => d);
        foreach (var d in defs) d.SetSiblings(sibs);
        return coreLibCache[name] = defs[0];
    }

    void ExecScript(UserFunction script)
    {
        foreach (var kv in script.Siblings) CommandLineFunctions[kv.Key] = kv.Value;
        var saveLine = Current.Line; var saveName = Current.FrameName;
        Current.FrameName = script.Name; Current.Line = 0;
        PushFile(script.File);
        try { ExecBlock(script.Body); }
        catch (Exception ex) when (ex is not ControlException)
        {
            var oe = AddFrame(ex, Current);
            if (!ReferenceEquals(oe, ex)) throw oe;
            throw;
        }
        finally { Current.Line = saveLine; Current.FrameName = saveName; PopFile(); }
    }

    public Value[] CallUser(UserFunction f, Value[] args, int nargout, Dictionary<string, Value>? preset = null)
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
        if (preset != null) foreach (var kv in preset) scope.Vars[kv.Key] = kv.Value;
        bool needDefaults = f.Defaults != null && args.Length < fixedIn;

        var save = Current; var saveEnd = endStack.Count == 0 ? null : endStack.ToList();
        Current = scope; depth++; CallStack.Add(scope); PushFile(f.File);
        endStack.Clear();
        try
        {
            if (needDefaults)
                for (int k = args.Length; k < fixedIn && k < f.Defaults!.Count; k++)
                    if (f.Defaults[k] is { } dx && f.Params[k] != "~") scope.Vars[f.Params[k]] = Eval(dx);
            ExecBlock(f.Body);
        }
        catch (Exception ex) when (ex is not ControlException)
        {
            var oe = AddFrame(ex, scope);
            if (!ReferenceEquals(oe, ex)) throw oe;
            throw;
        }
        finally { Current = save; depth--; CallStack.RemoveAt(CallStack.Count - 1); PopFile(); endStack.Clear(); if (saveEnd != null) endStack.AddRange(saveEnd); }

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
            if (v is ArrayValue ovm) ovm.Exclusive = false;
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
        var save = Current; var saveEnd = endStack.Count == 0 ? null : endStack.ToList();
        Current = scope; depth++; endStack.Clear();
        try { return EvalMulti(fh.Body!, nargout).ToArray(); }
        finally { Current = save; depth--; endStack.Clear(); if (saveEnd != null) endStack.AddRange(saveEnd); }
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
