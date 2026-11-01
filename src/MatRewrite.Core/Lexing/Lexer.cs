// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Lexing;

public enum TokKind { Num, ImagNum, Str, Ident, Kw, Punct, Newline, EOF }

public sealed record Token(TokKind Kind, string Text, double Num, int Line, int Col, bool SpaceBefore)
{
    public bool Is(string punct) => (Kind == TokKind.Punct || Kind == TokKind.Kw) && Text == punct;
    public override string ToString() => $"{Kind}:{Text}";
}

/// <summary>
/// Context-sensitive lexer for Octave/MATLAB: handles transpose vs. string quotes,
/// whitespace-separated elements inside [] and {}, comments (%, #, block %{ %}),
/// continuation (... and \), and command syntax for a set of known commands.
/// </summary>
public sealed class Lexer
{
    public static bool IsKeyword(string s) => Keywords.Contains(s);
    public static readonly HashSet<string> Keywords = new()
    {
        "if","elseif","else","end","endif","for","endfor","parfor","endparfor","while","endwhile","do","until",
        "switch","case","otherwise","endswitch","try","catch","end_try_catch","function","endfunction",
        "return","break","continue","global","persistent","unwind_protect","unwind_protect_cleanup","end_unwind_protect"
    };
    static readonly HashSet<string> CommandWords = new()
    {
        "format","clc","clear","close","hold","more","pkg","warning","help","which","cd","figure","addpath","rmpath",
        "type","echo","clearvars","dbstop","dbclear","dbstep","dbcont","dbquit","dbstatus","dbstack","keyboard","run","disp","load","save","grid","axis","box","colormap","colorbar","print","drawnow","clf","cla","legend","mkdir","rmdir","delete","graphics_toolkit","diary","beep_on_error","page_screen_output","shading","rotate3d"
    };

    readonly string src;
    int pos, line = 1, lineStart;
    readonly List<Token> toks = new();
    readonly Stack<char> brackets = new(); // '[', '{', '('

    public Lexer(string source) { src = source.Replace("\r\n", "\n").Replace('\r', '\n'); }

    public static List<Token> Tokenize(string s) => new Lexer(s).Run();

    char Cur => pos < src.Length ? src[pos] : '\0';
    char Peek(int k = 1) => pos + k < src.Length ? src[pos + k] : '\0';
    bool InMatrix => brackets.Count > 0 && brackets.Peek() != '(';
    Token? Last => toks.Count > 0 ? toks[^1] : null;

    bool LastEndsValue()
    {
        var t = Last;
        if (t == null) return false;
        return t.Kind switch
        {
            TokKind.Num or TokKind.ImagNum or TokKind.Str or TokKind.Ident => true,
            TokKind.Kw => t.Text == "end",
            TokKind.Punct => t.Text is ")" or "]" or "}" or "'" or ".'",
            _ => false
        };
    }

    bool AtStatementStart()
    {
        var t = Last;
        return t == null || t.Kind == TokKind.Newline || (t.Kind == TokKind.Punct && (t.Text == ";" || t.Text == ",") && brackets.Count == 0);
    }

    void Add(TokKind k, string text, bool space, double num = 0) =>
        toks.Add(new Token(k, text, num, line, pos - lineStart + 1, space));

    OctaveException Err(string msg) => new($"parse error near line {line}: {msg}");

    bool IsLineBlank(int from, out int end)
    {
        end = from;
        while (end < src.Length && src[end] != '\n') end++;
        return true;
    }

    /// <summary>Block comment: "%{" or "#{" alone on its line.</summary>
    bool TryBlockComment()
    {
        if (!((Cur == '%' || Cur == '#') && Peek() == '{')) return false;
        // must be alone on line
        int ls = pos; while (ls > 0 && src[ls - 1] != '\n') ls--;
        if (src[ls..pos].Trim().Length != 0) return false;
        int le = pos; while (le < src.Length && src[le] != '\n') le++;
        if (src[pos..le].Trim() != "%{" && src[pos..le].Trim() != "#{") return false;
        int depth = 0;
        while (pos < src.Length)
        {
            int e = pos; while (e < src.Length && src[e] != '\n') e++;
            var t = src[pos..e].Trim();
            if (t == "%{" || t == "#{") depth++;
            else if (t == "%}" || t == "#}") depth--;
            pos = e;
            if (pos < src.Length) { pos++; line++; lineStart = pos; }
            if (depth == 0) break;
        }
        // emit a newline so the statement ends
        Add(TokKind.Newline, "\n", false);
        return true;
    }

    public List<Token> Run()
    {
        while (true)
        {
            bool space = false;
            // whitespace, continuation
            while (true)
            {
                if (Cur == ' ' || Cur == '\t') { pos++; space = true; continue; }
                if (Cur == '.' && Peek() == '.' && Peek(2) == '.')
                {
                    while (pos < src.Length && src[pos] != '\n') pos++;
                    if (pos < src.Length) { pos++; line++; lineStart = pos; }
                    space = true; continue;
                }
                break;
            }
            if (pos >= src.Length) break;
            char c = Cur;

            if (c == '%' || c == '#')
            {
                if (TryBlockComment()) continue;
                while (pos < src.Length && src[pos] != '\n') pos++;
                continue;
            }

            // implicit comma inside [] / {}
            if (InMatrix && space && LastEndsValue() && StartsValue())
                Add(TokKind.Punct, ",", true);

            if (c == '\n')
            {
                pos++;
                if (InMatrix) { if (!(Last != null && Last.Is(";"))) Add(TokKind.Punct, ";", space); }
                else if (brackets.Count == 0) Add(TokKind.Newline, "\n", space);
                line++; lineStart = pos;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek()))) { LexNumber(space); continue; }

            if (char.IsLetter(c) || c == '_')
            {
                int s = pos;
                while (char.IsLetterOrDigit(Cur) || Cur == '_') pos++;
                var w = src[s..pos];
                bool afterDot = Last != null && Last.Is(".");
                if (!afterDot && Keywords.Contains(w)) Add(TokKind.Kw, w, space);
                else
                {
                    bool cmdStart = AtStatementStart() && brackets.Count == 0;
                    Add(TokKind.Ident, w, space);
                    if (cmdStart && CommandWords.Contains(w)) TryCommandSyntax();
                }
                continue;
            }

            if (c == '"') { LexDqString(space); continue; }
            if (c == '\'')
            {
                if (LastEndsValue() && !(space && InMatrix)) { pos++; Add(TokKind.Punct, "'", space); }
                else LexSqString(space);
                continue;
            }

            // punctuation
            string[] ops3 = { ".^'", "!==" };
            string? op = null;
            string two = pos + 1 < src.Length ? src.Substring(pos, 2) : c.ToString();
            string[] ops2 = { "==", "~=", "!=", "<=", ">=", "&&", "||", ".*", "./", ".\\", ".^", ".'", "+=", "-=", "*=", "/=", "^=", "++", "--" };
            if (ops2.Contains(two))
            {
                op = two;
                if ((op == "++" || op == "--")) op = null; // treat as two unary/binary ops
            }
            if (op == null) op = c.ToString();
            if ("+-*/\\^'.<>=&|!~,;:()[]{}@".IndexOf(op[0]) < 0)
                throw Err($"invalid character '{c}'");
            pos += op.Length;
            switch (op)
            {
                case "(": brackets.Push('('); break;
                case "[": brackets.Push('['); break;
                case "{": brackets.Push('{'); break;
                case ")": case "]": case "}":
                    if (brackets.Count > 0) brackets.Pop();
                    // remove a trailing ';' or ',' row separator before closing bracket
                    if (op != ")" && Last != null && (Last.Is(";") || Last.Is(",")) && Last.SpaceBefore == true && false) toks.RemoveAt(toks.Count - 1);
                    break;
            }
            Add(TokKind.Punct, op, space);
        }
        Add(TokKind.Newline, "\n", false);
        Add(TokKind.EOF, "", false);
        return toks;
    }

    bool StartsValue()
    {
        char c = Cur, n = Peek();
        if (char.IsLetterOrDigit(c) || c == '_' || c == '"' || c == '\'' || c == '(' || c == '[' || c == '{' || c == '@') return true;
        if (c == '.' && char.IsDigit(n)) return true;
        if ((c == '~' || c == '!') && n != '=') return true;
        if ((c == '+' || c == '-') && n != ' ' && n != '\t' && n != '=' && n != '\n') return true;
        return false;
    }

    void LexNumber(bool space)
    {
        int s = pos;
        if (Cur == '0' && (Peek() == 'x' || Peek() == 'X'))
        {
            pos += 2; int hs = pos;
            while (Uri.IsHexDigit(Cur)) pos++;
            Add(TokKind.Num, src[s..pos], space, Convert.ToInt64(src[hs..pos], 16));
            return;
        }
        while (char.IsDigit(Cur)) pos++;
        if (Cur == '.' && !(Peek() == '*' || Peek() == '/' || Peek() == '\\' || Peek() == '^' || Peek() == '\'') && !(Peek() == '.' && Peek(2) == '.'))
        {
            pos++;
            while (char.IsDigit(Cur)) pos++;
        }
        if ((Cur == 'e' || Cur == 'E' || Cur == 'd' || Cur == 'D') &&
            (char.IsDigit(Peek()) || ((Peek() == '+' || Peek() == '-') && char.IsDigit(Peek(2)))))
        {
            pos++;
            if (Cur == '+' || Cur == '-') pos++;
            while (char.IsDigit(Cur)) pos++;
        }
        var text = src[s..pos].Replace('d', 'e').Replace('D', 'e');
        double v = double.Parse(text, CultureInfo.InvariantCulture);
        if ((Cur == 'i' || Cur == 'j' || Cur == 'I' || Cur == 'J') && !(char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            pos++;
            Add(TokKind.ImagNum, src[s..pos], space, v);
        }
        else Add(TokKind.Num, src[s..pos], space, v);
    }

    void LexSqString(bool space)
    {
        pos++;
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= src.Length || Cur == '\n') throw Err("unterminated character string constant");
            if (Cur == '\'')
            {
                if (Peek() == '\'') { sb.Append('\''); pos += 2; continue; }
                pos++; break;
            }
            sb.Append(Cur); pos++;
        }
        Add(TokKind.Str, sb.ToString(), space);
    }

    void LexDqString(bool space)
    {
        pos++;
        var sb = new StringBuilder();
        while (true)
        {
            if (pos >= src.Length || Cur == '\n') throw Err("unterminated character string constant");
            char c = Cur;
            if (c == '"')
            {
                if (Peek() == '"') { sb.Append('"'); pos += 2; continue; }
                pos++; break;
            }
            if (c == '\\' && pos + 1 < src.Length)
            {
                pos++;
                char e = Cur; pos++;
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'a': sb.Append('\a'); break;
                    case '0': sb.Append('\0'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    default: sb.Append('\\').Append(e); break;
                }
                continue;
            }
            sb.Append(c); pos++;
        }
        Add(TokKind.Str, sb.ToString(), space);
        toks[^1] = toks[^1] with { Num = 1 }; // Num=1 marks a double-quoted string
    }

    /// <summary>"format long", "hold on", "clear a b": words become string arguments.</summary>
    void TryCommandSyntax()
    {
        int p = pos;
        // need whitespace then a word char; not "=" or "(" after optional spaces
        if (p >= src.Length || (src[p] != ' ' && src[p] != '\t')) return;
        while (p < src.Length && (src[p] == ' ' || src[p] == '\t')) p++;
        if (p >= src.Length) return;
        char c = src[p];
        if (c == '\n' || c == ';' || c == ',' || c == '%' || c == '#' || c == '=' || c == '(') return;
        if ((c == '+' || c == '-' || c == '*' || c == '/' || c == '<' || c == '>' || c == '&' || c == '|' || c == '^')
            && p + 1 < src.Length && (src[p + 1] == ' ' || src[p + 1] == '=')) return;
        if (c == '\'' || c == '"') return; // treat as normal function syntax with string arg? fall back
        // collect words until ; , newline or comment
        pos = p;
        Add(TokKind.Punct, "(", true);
        bool first = true;
        while (pos < src.Length && src[pos] != '\n' && src[pos] != ';' && src[pos] != ',' && src[pos] != '%' && src[pos] != '#')
        {
            if (src[pos] == ' ' || src[pos] == '\t') { pos++; continue; }
            int s = pos;
            while (pos < src.Length && !" \t\n;,%#".Contains(src[pos])) pos++;
            if (!first) Add(TokKind.Punct, ",", true);
            Add(TokKind.Str, src[s..pos], true);
            first = false;
        }
        Add(TokKind.Punct, ")", false);
    }
}
