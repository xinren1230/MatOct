// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Lexing;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Parsing;

/// <summary>Recursive-descent parser for Octave/MATLAB source.</summary>
public sealed class Parser
{
    readonly List<Token> t;
    int p;
    int indexDepth; // >0 while parsing inside an index -> 'end' is a value

    public Parser(List<Token> tokens) { t = tokens; }

    public static ProgramUnit ParseProgram(string src) => new Parser(Lexer.Tokenize(src)).ParseUnit();

    Token Cur => t[p];
    Token PeekTok(int k = 1) => t[Math.Min(p + k, t.Count - 1)];
    Token Next() => t[p++];
    bool IsP(string s) => Cur.Kind == TokKind.Punct && Cur.Text == s;
    bool IsKw(params string[] s) => Cur.Kind == TokKind.Kw && s.Contains(Cur.Text);
    OctaveException Err(string msg) => new($"parse error near line {Cur.Line}, column {Cur.Col}: {msg}");
    void Expect(string s)
    {
        if (!IsP(s)) throw Err($"expected '{s}' but found '{(Cur.Kind == TokKind.Newline ? "newline" : Cur.Text)}'");
        p++;
    }

    void SkipNewlines()
    {
        while (Cur.Kind == TokKind.Newline || IsP(";") || IsP(",")) p++;
    }

    public ProgramUnit ParseUnit()
    {
        var u = new ProgramUnit();
        SkipNewlines();
        if (IsKw("function")) u.IsFunctionFile = true;
        if (Cur.Kind == TokKind.Ident && Cur.Text == "classdef") { u.IsFunctionFile = true; u.Class = ParseClassdef(); SkipNewlines(); }
        while (Cur.Kind != TokKind.EOF)
        {
            if (IsKw("function")) { u.Functions.Add(ParseFunction(out var promoted)); u.Functions.AddRange(promoted); }
            else
            {
                var s = ParseStatement();
                if (s != null) u.Statements.Add(s);
            }
            SkipNewlines();
        }
        return u;
    }

    FunctionDef ParseFunction(out List<FunctionDef> promoted)
    {
        int line = Cur.Line;
        p++; // function
        var outs = new List<string>();
        string name;
        // forms: function name, function r = name(...), function [a,b] = name(...)
        if (IsP("["))
        {
            p++;
            while (!IsP("]"))
            {
                if (IsP(",")) { p++; continue; }
                if (Cur.Kind != TokKind.Ident) throw Err("invalid output list");
                outs.Add(Next().Text);
            }
            p++;
            Expect("=");
            name = ParseFuncName();
        }
        else
        {
            var first = ParseFuncName();
            if (IsP("=")) { p++; outs.Add(first); name = ParseFuncName(); }
            else name = first;
        }
        var pars = new List<string>();
        List<Expr?>? defs = null;
        if (IsP("("))
        {
            p++;
            while (!IsP(")"))
            {
                if (IsP(",")) { p++; continue; }
                if (Cur.Kind == TokKind.Ident) pars.Add(Next().Text);
                else if (IsP("~") || IsP("!")) { p++; pars.Add("~"); }
                else throw Err("invalid parameter list");
                if (IsP("="))
                {
                    p++;
                    defs ??= new List<Expr?>();
                    while (defs.Count < pars.Count - 1) defs.Add(null);
                    defs.Add(ParseExpr());
                }
            }
            if (defs != null) while (defs.Count < pars.Count) defs.Add(null);
            p++;
        }
        var body = new List<Stmt>();
        var kids = new List<FunctionDef>();
        var kidPromoted = new List<FunctionDef>();
        string term;
        while (true)
        {
            body.AddRange(ParseBlock(out term, "end", "endfunction", "function"));
            if (term != "function") break;
            kids.Add(ParseFunction(out var kp));
            kidPromoted.AddRange(kp);
        }
        promoted = new List<FunctionDef>();
        if (term == "end" || term == "endfunction")
        {
            p++;
            return new FunctionDef(name, pars, outs, body) { Defaults = defs, Line = line, Nested = kids.Concat(kidPromoted).ToList() };
        }
        // no 'end': following functions are siblings (subfunctions)
        promoted.AddRange(kids); promoted.AddRange(kidPromoted);
        return new FunctionDef(name, pars, outs, body) { Line = line };
    }

    /// <summary>Skip a balanced "( ... )" attribute list; returns attribute names set to true/false.</summary>
    Dictionary<string, string> ParseAttrs()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!IsP("(")) return d;
        p++;
        int depth = 1; string? key = null; var val = new System.Text.StringBuilder();
        void Flush() { if (key != null) d[key] = val.Length == 0 ? "true" : val.ToString(); key = null; val.Clear(); }
        bool afterEq = false;
        while (depth > 0 && Cur.Kind != TokKind.EOF)
        {
            var tk = Next();
            if (tk.Kind == TokKind.Punct && tk.Text == "(") depth++;
            else if (tk.Kind == TokKind.Punct && tk.Text == ")") { depth--; if (depth == 0) break; }
            else if (depth == 1 && tk.Kind == TokKind.Punct && tk.Text == ",") { Flush(); afterEq = false; }
            else if (depth == 1 && tk.Kind == TokKind.Punct && tk.Text == "=") afterEq = true;
            else if (depth == 1 && tk.Kind == TokKind.Punct && (tk.Text == "~" || tk.Text == "!")) val.Append("~");
            else if (!afterEq && key == null) key = tk.Text;
            else val.Append(tk.Text);
        }
        Flush();
        return d;
    }

    static bool AttrTrue(Dictionary<string, string> a, string k) => a.TryGetValue(k, out var v) && v is "true" or "~false";
    static string? AttrVal(Dictionary<string, string> a, string k) => a.TryGetValue(k, out var v) ? v.Trim('\'', '"').ToLowerInvariant() : null;

    ClassDefAst ParseClassdef()
    {
        var c = new ClassDefAst { Line = Cur.Line };
        p++; // classdef
        c.Attrs = ParseAttrs();
        c.Name = ParseFuncName();
        if (IsP("<"))
        {
            p++;
            c.Supers.Add(ParseFuncName());
            while (IsP("&") || IsP("&&")) { p++; c.Supers.Add(ParseFuncName()); }
        }
        while (true)
        {
            SkipNewlines();
            if (IsKw("end")) { p++; break; }
            if (Cur.Kind == TokKind.EOF) throw Err("classdef: missing 'end'");
            var w = Cur.Kind == TokKind.Ident ? Cur.Text : "";
            if (w == "properties")
            {
                p++;
                var attrs = ParseAttrs();
                bool constant = AttrTrue(attrs, "Constant");
                string acc = AttrVal(attrs, "Access") ?? "public";
                string gacc = AttrVal(attrs, "GetAccess") ?? acc, sacc = AttrVal(attrs, "SetAccess") ?? acc;
                bool dep = AttrTrue(attrs, "Dependent"), hid = AttrTrue(attrs, "Hidden"), abs = AttrTrue(attrs, "Abstract");
                while (true)
                {
                    SkipNewlines();
                    if (IsKw("end")) { p++; break; }
                    if (Cur.Kind != TokKind.Ident) throw Err("invalid property declaration");
                    var name = Next().Text;
                    // optional size/class validation: (1,1) double {mustBe...}
                    if (IsP("(")) ParseAttrs();
                    while (Cur.Kind == TokKind.Ident && !(PeekTok().Kind == TokKind.Punct && PeekTok().Text == "=")) p++;
                    if (IsP("{")) { int d = 0; do { if (IsP("{")) d++; else if (IsP("}")) d--; p++; } while (d > 0 && Cur.Kind != TokKind.EOF); }
                    Expr? def = null;
                    if (IsP("=")) { p++; def = ParseExpr(); }
                    c.Props.Add(new PropDef(name, def, constant) { GetAccess = gacc, SetAccess = sacc, Dependent = dep, Hidden = hid, Abstract = abs });
                    if (IsP(";") || IsP(",")) p++;
                }
            }
            else if (w == "methods")
            {
                p++;
                var attrs = ParseAttrs();
                bool st = AttrTrue(attrs, "Static");
                string macc = AttrVal(attrs, "Access") ?? "public";
                bool mabs = AttrTrue(attrs, "Abstract");
                while (true)
                {
                    SkipNewlines();
                    if (IsKw("end")) { p++; break; }
                    if (IsKw("function"))
                    {
                        var f = ParseFunction(out var promoted);
                        c.Methods.Add(new MethodDef(f, st) { Access = macc });
                        foreach (var pf in promoted) c.Methods.Add(new MethodDef(pf, st) { Access = macc });
                        continue;
                    }
                    // method signature declaration without body (defined in a separate file) - skip line
                    while (Cur.Kind != TokKind.Newline && Cur.Kind != TokKind.EOF) p++;
                }
            }
            else if (w == "events")
            {
                p++; ParseAttrs();
                while (true)
                {
                    SkipNewlines();
                    if (IsKw("end")) { p++; break; }
                    if (Cur.Kind == TokKind.EOF) throw Err("events: missing 'end'");
                    if (Cur.Kind == TokKind.Ident) c.Events.Add(Next().Text); else p++;
                }
            }
            else if (w == "enumeration")
            {
                p++; ParseAttrs();
                while (true)
                {
                    SkipNewlines();
                    if (IsKw("end")) { p++; break; }
                    if (Cur.Kind == TokKind.EOF) throw Err("enumeration: missing 'end'");
                    if (Cur.Kind != TokKind.Ident) { p++; continue; }
                    var en = Next().Text;
                    var eargs = new List<Expr>();
                    if (IsP("("))
                    {
                        p++;
                        while (!IsP(")"))
                        {
                            if (IsP(",")) { p++; continue; }
                            eargs.Add(ParseExpr());
                        }
                        p++;
                    }
                    c.Enums.Add(new EnumDef(en, eargs));
                    if (IsP(",") || IsP(";")) p++;
                }
            }
            else throw Err($"unexpected '{Cur.Text}' in classdef block");
        }
        return c;
    }

    string ParseFuncName()
    {
        if (Cur.Kind != TokKind.Ident) throw Err("expected function name");
        var n = Next().Text;
        while (IsP(".") && PeekTok().Kind == TokKind.Ident) { p++; n += "." + Next().Text; }
        return n;
    }

    /// <summary>Parse statements until one of the terminator keywords (not consumed) or EOF.</summary>
    List<Stmt> ParseBlock(out string term, params string[] terms)
    {
        var list = new List<Stmt>();
        while (true)
        {
            SkipNewlines();
            if (Cur.Kind == TokKind.EOF) { term = "EOF"; return list; }
            if (Cur.Kind == TokKind.Kw && terms.Contains(Cur.Text)) { term = Cur.Text; return list; }
            var s = ParseStatement();
            if (s != null) list.Add(s);
        }
    }

    void ExpectEnd(params string[] alts)
    {
        if (IsKw("end") || (Cur.Kind == TokKind.Kw && alts.Contains(Cur.Text))) { p++; return; }
        throw Err($"expected 'end' but found '{Cur.Text}'");
    }

    /// <summary>Statement terminator: returns true if output should be printed.</summary>
    bool EndStatement()
    {
        if (IsP(";")) { p++; if (Cur.Kind == TokKind.Newline) p++; return false; }
        if (IsP(",")) { p++; return true; }
        if (Cur.Kind == TokKind.Newline) { p++; return true; }
        if (Cur.Kind == TokKind.EOF) return true;
        // allow keywords directly after (e.g. "if x, y=1, end")
        if (Cur.Kind == TokKind.Kw) return true;
        throw Err($"unexpected '{Cur.Text}'");
    }

    Stmt? ParseStatement()
    {
        int line = Cur.Line;
        var s = ParseStatementInner();
        return s == null ? null : s with { Line = line };
    }

    Stmt? ParseStatementInner()
    {
        if (Cur.Kind == TokKind.Kw)
        {
            switch (Cur.Text)
            {
                case "if": return ParseIf();
                case "for": case "parfor": return ParseFor();
                case "while":
                {
                    p++;
                    var cond = ParseExpr();
                    var body = ParseBlock(out _, "end", "endwhile");
                    ExpectEnd("endwhile");
                    return new WhileStmt(cond, body);
                }
                case "do":
                {
                    p++;
                    var body = ParseBlock(out _, "until");
                    p++;
                    var cond = ParseExpr();
                    return new DoUntilStmt(body, cond);
                }
                case "switch": return ParseSwitch();
                case "try":
                {
                    p++;
                    if (IsP(",")) p++;
                    var body = ParseBlock(out var term, "catch", "end", "end_try_catch");
                    string? v = null;
                    var cbody = new List<Stmt>();
                    if (term == "catch")
                    {
                        int catchLine = Cur.Line;
                        p++;
                        if (Cur.Kind == TokKind.Ident && Cur.Line == catchLine && !(PeekTok().Kind == TokKind.Punct && PeekTok().Text is "=" or "(" or "."))
                            v = Next().Text;
                        cbody = ParseBlock(out _, "end", "end_try_catch");
                    }
                    ExpectEnd("end_try_catch");
                    return new TryStmt(body, v, cbody);
                }
                case "unwind_protect":
                {
                    p++;
                    var body = ParseBlock(out _, "unwind_protect_cleanup");
                    p++;
                    var cl = ParseBlock(out _, "end_unwind_protect", "end");
                    p++;
                    return new UnwindStmt(body, cl);
                }
                case "break": p++; EndStatement(); return new BreakStmt();
                case "continue": p++; EndStatement(); return new ContinueStmt();
                case "return": p++; EndStatement(); return new ReturnStmt();
                case "global":
                case "persistent":
                {
                    bool pers = Cur.Text == "persistent";
                    p++;
                    var names = new List<string>(); var inits = new List<Expr?>();
                    while (Cur.Kind == TokKind.Ident)
                    {
                        names.Add(Next().Text);
                        if (IsP("=")) { p++; inits.Add(ParseExpr()); } else inits.Add(null);
                    }
                    EndStatement();
                    return new GlobalStmt(names, inits, pers);
                }
                case "function": throw Err("nested function definitions are not supported here");
                default: throw Err($"unexpected keyword '{Cur.Text}'");
            }
        }

        // multi-assignment [a, b] = ...
        if (IsP("[") && LooksLikeMultiAssign())
        {
            p++;
            var targets = new List<Expr>();
            while (!IsP("]"))
            {
                if (IsP(",")) { p++; continue; }
                if ((IsP("~") || IsP("!")) && (PeekTok().Is(",") || PeekTok().Is("]"))) { p++; targets.Add(new TildeExpr()); continue; }
                targets.Add(ParsePostfix());
            }
            p++;
            Expect("=");
            var rhs = ParseExpr();
            return new AssignStmt(targets, rhs, EndStatement());
        }

        var e = ParseExpr();
        if (IsP("="))
        {
            p++;
            CheckLValue(e);
            var rhs = ParseChainRhs();
            return new AssignStmt(new List<Expr> { e }, rhs, EndStatement());
        }
        if (e is AssignExpr ae0)
            return new AssignStmt(new List<Expr> { ae0.Target }, ae0.Value, EndStatement());
        if (Cur.Kind == TokKind.Punct && Cur.Text is "+=" or "-=" or "*=" or "/=" or "^=" or ".*=" or "./=" or ".^=" or "|=" or "&=")
        {
            var op = Next().Text[..^1];
            CheckLValue(e);
            var rhs = ParseExpr();
            return new AssignStmt(new List<Expr> { e }, new BinaryExpr(op, e, new ParenExpr(rhs)), EndStatement(), op);
        }
        // x++ / x-- statements
        if ((IsP("+") && PeekTok().Is("+")) || (IsP("-") && PeekTok().Is("-")))
        {
            var op = Cur.Text; p += 2;
            CheckLValue(e);
            return new AssignStmt(new List<Expr> { e }, new BinaryExpr(op, e, new NumberExpr(1, false, "1")), EndStatement(), op);
        }
        return new ExprStmt(e, EndStatement());
    }

    /// <summary>Right side of an assignment, allowing Octave chains such as a = b = 0.</summary>
    Expr ParseChainRhs()
    {
        var rhs = ParseExpr();
        if (IsP("=") && IsLValue(rhs)) { p++; return new AssignExpr(rhs, ParseChainRhs()); }
        return rhs;
    }

    static bool IsLValue(Expr e) => e switch
    {
        IdentExpr => true,
        IndexExpr ix => IsLValue(ix.Target),
        FieldExpr f => IsLValue(f.Target),
        _ => false,
    };

    void CheckLValue(Expr e)
    {
        switch (e)
        {
            case IdentExpr: return;
            case IndexExpr ix: CheckLValue(ix.Target); return;
            case FieldExpr f: CheckLValue(f.Target); return;
            default: throw Err("invalid assignment target");
        }
    }

    bool LooksLikeMultiAssign()
    {
        // scan to matching ']' and check that '=' (not '==') follows
        int depth = 0, k = p;
        for (; k < t.Count; k++)
        {
            var tk = t[k];
            if (tk.Kind == TokKind.Newline || tk.Kind == TokKind.EOF) return false;
            if (tk.Kind != TokKind.Punct) continue;
            if (tk.Text is "[" or "(" or "{") depth++;
            else if (tk.Text is "]" or ")" or "}") { depth--; if (depth == 0) break; }
        }
        return k + 1 < t.Count && t[k + 1].Is("=");
    }

    Stmt ParseIf()
    {
        p++; // if
        var clauses = new List<(Expr, List<Stmt>)>();
        var cond = ParseExpr();
        var body = ParseBlock(out var term, "elseif", "else", "end", "endif");
        clauses.Add((cond, body));
        List<Stmt>? els = null;
        while (true)
        {
            if (term == "elseif")
            {
                p++;
                SkipNewlines();
                var c = ParseExpr();
                var b = ParseBlock(out term, "elseif", "else", "end", "endif");
                clauses.Add((c, b));
            }
            else if (term == "else")
            {
                p++;
                els = ParseBlock(out term, "end", "endif");
            }
            else break;
        }
        ExpectEnd("endif");
        return new IfStmt(clauses, els);
    }

    Stmt ParseFor()
    {
        p++;
        bool paren = false;
        if (IsP("(")) { paren = true; p++; }
        var v = ParsePostfix();
        Expect("=");
        var range = ParseExpr();
        if (paren) Expect(")");
        var body = ParseBlock(out _, "end", "endfor", "endparfor");
        ExpectEnd("endfor", "endparfor");
        return new ForStmt(v, range, body);
    }

    Stmt ParseSwitch()
    {
        p++;
        var subj = ParseExpr();
        SkipNewlines();
        var cases = new List<(Expr, List<Stmt>)>();
        List<Stmt>? other = null;
        while (true)
        {
            SkipNewlines();
            if (IsKw("case"))
            {
                p++;
                var label = ParseExpr();
                var body = ParseBlock(out _, "case", "otherwise", "end", "endswitch");
                cases.Add((label, body));
            }
            else if (IsKw("otherwise"))
            {
                p++;
                other = ParseBlock(out _, "case", "otherwise", "end", "endswitch");
            }
            else break;
        }
        ExpectEnd("endswitch");
        return new SwitchStmt(subj, cases, other);
    }

    // ---------------- expressions ----------------
    public Expr ParseExpr() => ParseOrOr();

    Expr ParseOrOr()
    {
        var l = ParseAndAnd();
        while (IsP("||")) { p++; l = new BinaryExpr("||", l, ParseAndAnd()); }
        return l;
    }
    Expr ParseAndAnd()
    {
        var l = ParseOr();
        while (IsP("&&")) { p++; l = new BinaryExpr("&&", l, ParseOr()); }
        return l;
    }
    Expr ParseOr()
    {
        var l = ParseAnd();
        while (IsP("|")) { p++; l = new BinaryExpr("|", l, ParseAnd()); }
        return l;
    }
    Expr ParseAnd()
    {
        var l = ParseCmp();
        while (IsP("&")) { p++; l = new BinaryExpr("&", l, ParseCmp()); }
        return l;
    }
    Expr ParseCmp()
    {
        var l = ParseRange();
        while (Cur.Kind == TokKind.Punct && Cur.Text is "==" or "~=" or "!=" or "<" or "<=" or ">" or ">=")
        {
            var op = Next().Text;
            if (op == "~=") op = "!=";
            l = new BinaryExpr(op, l, ParseRange());
        }
        return l;
    }
    Expr ParseRange()
    {
        // a lone ':' inside an index is the magic colon
        if (IsP(":") && indexDepth > 0 && (PeekTok().Is(",") || PeekTok().Is(")") || PeekTok().Is("}")))
        {
            p++; return new ColonExpr();
        }
        var a = ParseAdd();
        if (IsP(":") && !(PeekTok().Is(",") || PeekTok().Is(")")))
        {
            p++;
            var b = ParseAdd();
            if (IsP(":"))
            {
                p++;
                var c = ParseAdd();
                return new RangeExpr(a, b, c);
            }
            return new RangeExpr(a, null, b);
        }
        return a;
    }
    Expr ParseAdd()
    {
        var l = ParseMul();
        while ((IsP("+") || IsP("-")) && !(PeekTok().Is(Cur.Text) && IsStmtEndAfter(2)))
        {
            var op = Next().Text;
            l = new BinaryExpr(op, l, ParseMul());
        }
        return l;
    }
    bool IsStmtEndAfter(int k)
    {
        var tk = PeekTok(k);
        return tk.Kind == TokKind.Newline || tk.Kind == TokKind.EOF || tk.Is(";") || tk.Is(",");
    }
    Expr ParseMul()
    {
        var l = ParseUnary();
        while (Cur.Kind == TokKind.Punct && Cur.Text is "*" or "/" or ".*" or "./" or "\\" or ".\\")
        {
            var op = Next().Text;
            l = new BinaryExpr(op, l, ParseUnary());
        }
        return l;
    }
    Expr ParseUnary()
    {
        // Octave pre-increment/decrement: ++x, --x (only directly before an identifier)
        if ((IsP("+") || IsP("-")) && PeekTok().Is(Cur.Text) && !PeekTok().SpaceBefore && p + 2 < t.Count
            && t[p + 2].Kind == TokKind.Ident && !t[p + 2].SpaceBefore && !InMatrixContext())
        {
            var op = Cur.Text; int save = p; p += 2;
            var target = ParsePostfix();
            if (IsLValue(target)) return new AssignExpr(target, new BinaryExpr(op, target, new NumberExpr(1, false, "1")));
            p = save;
        }
        if (Cur.Kind == TokKind.Punct && Cur.Text is "-" or "+" or "!" or "~")
        {
            var op = Next().Text;
            if (op == "~") op = "!";
            var operand = ParseUnary();
            if (op == "-" && operand is NumberExpr n && !n.Imag) return new NumberExpr(-n.Value, false, "-" + n.Text);
            return new UnaryExpr(op, operand);
        }
        return ParsePower();
    }
    Expr ParsePower()
    {
        var b = ParsePostfix();
        while (IsP("^") || IsP(".^"))
        {
            var op = Next().Text;
            // exponent may carry its own unary sign: 2^-1
            Expr ex;
            if (Cur.Kind == TokKind.Punct && Cur.Text is "-" or "+" or "!" or "~")
            {
                var u = Next().Text; if (u == "~") u = "!";
                ex = new UnaryExpr(u, ParsePowerOperand());
            }
            else ex = ParsePowerOperand();
            b = new BinaryExpr(op, b, ex);
        }
        return b;
    }
    Expr ParsePowerOperand()
    {
        if (Cur.Kind == TokKind.Punct && Cur.Text is "-" or "+" or "!" or "~")
        {
            var u = Next().Text; if (u == "~") u = "!";
            return new UnaryExpr(u, ParsePowerOperand());
        }
        return ParsePostfix();
    }

    Expr ParsePostfix()
    {
        var e = ParsePrimary();
        if (e is IdentExpr sid && IsP("@") && !Cur.SpaceBefore && PeekTok().Kind == TokKind.Ident && !PeekTok().SpaceBefore)
        {
            p++;
            e = new IdentExpr(sid.Name + "@" + ParseFuncName());
        }
        while (true)
        {
            if (IsP("(") && !(Cur.SpaceBefore && InMatrixContext()))
            {
                p++;
                indexDepth++;
                var args = ParseArgs(")");
                indexDepth--;
                e = new IndexExpr(e, args, false);
            }
            else if (IsP("{") && !(Cur.SpaceBefore && InMatrixContext()))
            {
                p++;
                indexDepth++;
                var args = ParseArgs("}");
                indexDepth--;
                e = new IndexExpr(e, args, true);
            }
            else if (IsP(".") && !Cur.SpaceBefore)
            {
                p++;
                if (IsP("("))
                {
                    p++;
                    int save = indexDepth; indexDepth = 0;
                    var dn = ParseExpr();
                    indexDepth = save;
                    Expect(")");
                    e = new FieldExpr(e, null, dn);
                }
                else if (Cur.Kind == TokKind.Ident || Cur.Kind == TokKind.Kw) e = new FieldExpr(e, Next().Text, null);
                else throw Err("invalid field access");
            }
            else if (IsP("'") || IsP(".'"))
            {
                e = new PostfixExpr(Next().Text, e);
            }
            else if ((IsP("+") || IsP("-")) && PeekTok().Is(Cur.Text) && !PeekTok().SpaceBefore && IsLValue(e)
                     && p + 2 < t.Count && (t[p + 2].Kind is TokKind.Newline or TokKind.EOF || t[p + 2].Is(")") || t[p + 2].Is("]") || t[p + 2].Is("}") || t[p + 2].Is(",") || t[p + 2].Is(";")))
            {
                var op = Cur.Text; p += 2;
                e = new AssignExpr(e, new BinaryExpr(op, e, new NumberExpr(1, false, "1")), true);
            }
            else break;
        }
        return e;
    }

    int matrixDepth;
    bool InMatrixContext() => matrixDepth > 0 && indexDepthAtMatrix == indexDepth;
    int indexDepthAtMatrix = -1;

    List<Expr> ParseArgs(string close)
    {
        var args = new List<Expr>();
        int saveM = matrixDepth; matrixDepth = 0;
        while (Cur.Kind == TokKind.Newline) p++;
        if (IsP(close)) { p++; matrixDepth = saveM; return args; }
        while (true)
        {
            var ax = ParseExpr();
            if (IsP("=") && IsLValue(ax)) { p++; ax = new AssignExpr(ax, ParseChainRhs()); }   // Octave: f (x = expr)
            args.Add(ax);
            if (IsP(",")) { p++; continue; }
            if (IsP(close)) { p++; break; }
            throw Err($"expected ',' or '{close}' in argument list");
        }
        matrixDepth = saveM;
        return args;
    }

    Expr ParsePrimary()
    {
        var tk = Cur;
        switch (tk.Kind)
        {
            case TokKind.Num: p++; return new NumberExpr(tk.Num, false, tk.Text);
            case TokKind.ImagNum: p++; return new NumberExpr(tk.Num, true, tk.Text);
            case TokKind.Str: p++; return new StringExpr(tk.Text, tk.Num == 1);
            case TokKind.Ident: p++; return new IdentExpr(tk.Text);
            case TokKind.Kw when tk.Text == "end" && indexDepth > 0: p++; return new EndExpr();
        }
        if (IsP("("))
        {
            p++;
            int save = matrixDepth; matrixDepth = 0;
            var e = ParseExpr();
            if (IsP("=") && IsLValue(e)) { p++; e = new AssignExpr(e, ParseChainRhs()); }
            matrixDepth = save;
            Expect(")");
            return new ParenExpr(e);
        }
        if (IsP("[")) { p++; return new MatrixExpr(ParseMatrixRows("]")); }
        if (IsP("{")) { p++; return new CellExpr(ParseMatrixRows("}")); }
        if (IsP("@"))
        {
            p++;
            if (IsP("("))
            {
                p++;
                var pars = new List<string>();
                while (!IsP(")"))
                {
                    if (IsP(",")) { p++; continue; }
                    if (Cur.Kind == TokKind.Ident) pars.Add(Next().Text);
                    else if (IsP("~") || IsP("!")) { p++; pars.Add("~"); }
                    else throw Err("invalid anonymous function parameter list");
                }
                p++;
                int saveI = indexDepth, saveM = matrixDepth;
                indexDepth = 0; matrixDepth = 0;
                var body = ParseExpr();
                indexDepth = saveI; matrixDepth = saveM;
                return new AnonFuncExpr(pars, body);
            }
            if (Cur.Kind == TokKind.Ident)
            {
                var n = Next().Text;
                while (IsP(".") && PeekTok().Kind == TokKind.Ident) { p++; n += "." + Next().Text; }
                return new FuncHandleExpr(n);
            }
            throw Err("invalid function handle");
        }
        if (IsP(":")) { p++; return new ColonExpr(); }
        throw Err($"unexpected '{(tk.Kind == TokKind.Newline ? "newline" : tk.Text)}'");
    }

    List<List<Expr>> ParseMatrixRows(string close)
    {
        var rows = new List<List<Expr>>();
        var row = new List<Expr>();
        int saveM = matrixDepth, saveIM = indexDepthAtMatrix;
        matrixDepth++; indexDepthAtMatrix = indexDepth;
        while (true)
        {
            if (IsP(close)) { p++; break; }
            if (Cur.Kind == TokKind.EOF) throw Err($"unterminated matrix, expected '{close}'");
            if (IsP(";") || Cur.Kind == TokKind.Newline) { p++; if (row.Count > 0) rows.Add(row); row = new List<Expr>(); continue; }
            if (IsP(",")) { p++; continue; }
            row.Add(ParseExpr());
        }
        if (row.Count > 0) rows.Add(row);
        matrixDepth = saveM; indexDepthAtMatrix = saveIM;
        return rows;
    }
}
