// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Strings from a char matrix (one per row) or a cellstr.</summary>
    static List<string> StrList(Value v, string fn)
    {
        if (v is CellValue c) return c.Data.Select(x => S(x, fn)).ToList();
        var m = M(v, fn);
        if (m.R <= 1) return new List<string> { m.AsString() };
        return Enumerable.Range(0, m.R).Select(m.RowString).ToList();
    }

    /// <summary>Build a padded char matrix from rows.</summary>
    public static MatrixValue CharMatrix(IList<string> rows)
    {
        if (rows.Count == 0) return new MatrixValue(0, 0, NumKind.Char);
        int w = rows.Max(s => s.Length);
        var m = new MatrixValue(rows.Count, w, NumKind.Char);
        for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < w; j++) m.Re[i + j * rows.Count] = j < rows[i].Length ? rows[i][j] : ' ';
        return m;
    }

    static Value MapStrings(Value v, string fn, Func<string, Value> f, Func<Value>? emptyCell = null)
    {
        if (v is CellValue c) return new CellValue(c.R, c.C, c.Data.Select(x => f(S(x, fn))).ToArray());
        return f(S(v, fn));
    }

    static Value CompareStr(Value a, Value b, Func<string, string, bool> eq)
    {
        string? AsStr(Value v) => v is MatrixValue m && (m.IsChar || m.Numel == 0) && m.R <= 1 ? m.AsString() : (v is MatrixValue m2 && m2.IsChar ? null : null);
        bool One(Value x, Value y)
        {
            if (x is MatrixValue mx && y is MatrixValue my && mx.IsChar && my.IsChar)
            {
                if (mx.R != my.R || mx.C != my.C) return mx.R <= 1 && my.R <= 1 && mx.Numel == 0 && my.Numel == 0 || (mx.R <= 1 && my.R <= 1 && eq(mx.AsString(), my.AsString()) && mx.Numel == my.Numel);
                if (mx.R <= 1) return eq(mx.AsString(), my.AsString());
                return eq(mx.AsString(), my.AsString());
            }
            if (x is MatrixValue ex && y is MatrixValue ey && ex.Numel == 0 && ey.Numel == 0 && (ex.IsChar || ey.IsChar)) return true;
            return false;
        }
        if (a is CellValue ca && b is CellValue cb)
        {
            if (ca.Numel == 1) (ca, cb) = (cb, ca);
            var r = new MatrixValue(ca.R, ca.C, NumKind.Logical);
            if (cb.Numel == 1) { for (int k = 0; k < ca.Numel; k++) r.Re[k] = One(ca.Data[k], cb.Data[0]) ? 1 : 0; return r; }
            if (ca.Numel != cb.Numel) return Bool(false);
            for (int k = 0; k < ca.Numel; k++) r.Re[k] = One(ca.Data[k], cb.Data[k]) ? 1 : 0;
            return r;
        }
        if (a is CellValue c1 || b is CellValue)
        {
            var c = a as CellValue ?? (CellValue)b; var s = a is CellValue ? b : a;
            var r = new MatrixValue(c.R, c.C, NumKind.Logical);
            for (int k = 0; k < c.Numel; k++) r.Re[k] = One(c.Data[k], s) ? 1 : 0;
            return r;
        }
        return Bool(One(a, b));
    }

    public static string Num2Str(Value v, Value? arg = null)
    {
        if (v is MatrixValue cm && cm.IsChar) return cm.AsString();
        var m = M(v, "num2str");
        if (arg != null)
        {
            if (IsString(arg)) return Printf.Format(Printf.Escapes(S(arg, "num2str")), new[] { v });
            int prec = I(arg, "num2str");
            return string.Join("  ", Enumerable.Range(0, m.Numel).Select(k => Printf.FormatG(m.Re[k], prec)));
        }
        if (m.Numel == 0) return "";
        if (m.IsComplex)
        {
            string One(int k) { var re = Printf.ShortG(m.Re[k]); var im = m.Im![k]; return re + (im < 0 || double.IsNegative(im) ? "-" : "+") + Printf.ShortG(Math.Abs(im)) + "i"; }
            return string.Join("  ", Enumerable.Range(0, m.Numel).Select(One));
        }
        bool allInt = m.Re.All(x => x == Math.Floor(x) || !double.IsFinite(x));
        if (m.IsScalar)
        {
            double x = m.Re[0];
            if (allInt) return double.IsNaN(x) ? "NaN" : double.IsInfinity(x) ? (x > 0 ? "Inf" : "-Inf") : Math.Abs(x) < 1e15 ? ((long)x).ToString(Inv) : Printf.FormatG(x, 15);
            int nd = (int)Math.Floor(Math.Log10(Math.Abs(x)));
            nd = Math.Min(Math.Max(nd + 5, 5), 16);
            return Printf.FormatG(x, nd);
        }
        var rows = new List<string>();
        if (allInt)
        {
            double mx = m.Re.Where(double.IsFinite).Select(Math.Abs).DefaultIfEmpty(0).Max();
            int w = (mx == 0 ? 1 : (int)Math.Floor(Math.Log10(mx)) + 1) + (m.Re.Any(x => x < 0) ? 1 : 0);
            w = Math.Max(w, m.Re.Any(x => double.IsNaN(x)) ? 3 : 1); if (m.Re.Any(double.IsInfinity)) w = Math.Max(w, m.Re.Any(x => x == double.NegativeInfinity) ? 4 : 3);
            for (int i = 0; i < m.R; i++)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < m.C; j++) sb.Append(Printf.ShortG(m.Re[i + j * m.R]).PadLeft(w + 2));
                rows.Add(sb.ToString());
            }
        }
        else
        {
            double mx = m.Re.Where(double.IsFinite).Select(Math.Abs).DefaultIfEmpty(0).Max();
            int nd = mx == 0 ? 0 : (int)Math.Floor(Math.Log10(mx));
            nd = Math.Min(Math.Max(nd + 5, 5), 16);
            int w = nd + 7 + (m.Re.Any(x => x < 0) ? 1 : 0);
            for (int i = 0; i < m.R; i++)
            {
                var sb = new StringBuilder();
                for (int j = 0; j < m.C; j++) sb.Append(Printf.FormatG(m.Re[i + j * m.R], nd).PadLeft(w));
                rows.Add(sb.ToString());
            }
        }
        int lead = rows.Min(r => r.Length - r.TrimStart().Length);
        rows = rows.Select(r => r[lead..].TrimEnd()).ToList();
        return string.Join("\n", rows);
    }

    static Value Num2StrValue(Value v, Value? arg)
    {
        var s = Num2Str(v, arg);
        if (s.Contains('\n')) return CharMatrix(s.Split('\n'));
        return Str(s);
    }

    static string Mat2Str(Value v, int prec)
    {
        var m = M(v, "mat2str");
        string F(int k)
        {
            if (m.IsLogical) return m.Re[k] != 0 ? "true" : "false";
            string re = Printf.FormatG(m.Re[k], prec);
            if (m.Im == null) return re;
            double im = m.Im[k];
            return re + (im < 0 ? "-" : "+") + Printf.FormatG(Math.Abs(im), prec) + "i";
        }
        if (m.IsChar)
        {
            if (m.R <= 1) return "\"" + m.AsString() + "\"";
            return "[" + string.Join(";", Enumerable.Range(0, m.R).Select(i => "\"" + m.RowString(i) + "\"")) + "]";
        }
        if (m.IsScalar) return F(0);
        var sb = new StringBuilder("[");
        for (int i = 0; i < m.R; i++)
        {
            if (i > 0) sb.Append(';');
            for (int j = 0; j < m.C; j++) { if (j > 0) sb.Append(' '); sb.Append(F(i + j * m.R)); }
        }
        return sb.Append(']').ToString();
    }

    static string ConvertRegex(string pat)
    {
        // Octave (PCRE) and .NET syntax mostly coincide; translate (?<name> is shared, \< \> word boundaries are not.
        return pat.Replace("(?<", "(?<").Replace("\\<", "\\b").Replace("\\>", "\\b");
    }

    static void RegisterStrings()
    {
        Reg1("num2str", a => Num2StrValue(a[0], a.Length > 1 ? a[1] : null));
        Reg1("int2str", a => Num2StrValue(Ops.Map(M(a[0], "int2str"), RoundHalfAway), null));
        Reg1("mat2str", a => Str(Mat2Str(a[0], a.Length > 1 ? I(a[1], "mat2str") : 15)));
        Reg1("str2double", a =>
        {
            double P(Value v)
            {
                if (v is not MatrixValue m || !m.IsChar) return double.NaN;
                var s = m.AsString().Trim();
                var low = s.ToLowerInvariant();
                if (low is "inf" or "+inf") return double.PositiveInfinity;
                if (low == "-inf") return double.NegativeInfinity;
                if (low == "nan") return double.NaN;
                return double.TryParse(s, NumberStyles.Float, Inv, out var d) ? d : double.NaN;
            }
            if (a[0] is CellValue c) { var r = new MatrixValue(c.R, c.C); for (int k = 0; k < c.Numel; k++) r.Re[k] = P(c.Data[k]); return r; }
            return Num(P(a[0]));
        });
        Reg("str2num", (it, a, n) =>
        {
            var s = S(a[0], "str2num");
            try
            {
                var sub = new Interpreter(TextWriter.Null);
                sub.Eval("__str2num_tmp__ = [" + s + "];");
                return Out(sub.GetVar("__str2num_tmp__")!, Bool(true));
            }
            catch (Exception) { return Out(new MatrixValue(0, 0), Bool(false)); }
        });
        Reg1("sprintf", a =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to sprintf");
            return Str(Printf.Format(Printf.Escapes(S(a[0], "sprintf")), a.Skip(1).ToList()));
        });
        Reg0("printf", (it, a) => it.Out.Write(Printf.Format(Printf.Escapes(S(a[0], "printf")), a.Skip(1).ToList())));
        Reg("fprintf", (it, a, n) =>
        {
            int start = 0; var w = it.Out;
            if (a.Length > 0 && a[0] is MatrixValue f && !f.IsChar && f.Numel == 1) { start = 1; if (f.Re[0] == 2) { it.Out.Flush(); w = Console.Error; } }
            if (a.Length <= start) return Array.Empty<Value>();
            var s = Printf.Format(Printf.Escapes(S(a[start], "fprintf")), a.Skip(start + 1).ToList());
            w.Write(s);
            return n > 0 ? Out(Num(Encoding.UTF8.GetByteCount(s))) : Array.Empty<Value>();
        });
        Reg0("puts", (it, a) => it.Out.Write(S(a[0], "puts")));
        Reg0("fputs", (it, a) => it.Out.Write(S(a[1], "fputs")));
        Reg0("fdisp", (it, a) => it.Out.Write(Display.Disp(a[1])));
        Reg0("disp", (it, a) => { NArgs(a, 1, 2, "disp"); it.Out.Write(Display.Disp(a[0])); });
        Reg0("display", (it, a) => it.Out.Write(Display.Show("", a[0])));
        Reg1("char", a =>
        {
            if (a.Length == 1 && a[0] is MatrixValue m) { var r = m.Copy(); r.Kind = NumKind.Char; r.Im = null; return r; }
            var rows = new List<string>();
            foreach (var v in a) rows.AddRange(StrList(v is MatrixValue mm && !mm.IsChar ? new Func<Value>(() => { var c = mm.Copy(); c.Kind = NumKind.Char; return c; })() : v, "char"));
            return CharMatrix(rows);
        });
        Reg1("strvcat", a => CharMatrix(a.SelectMany(v => StrList(v, "strvcat")).Where(s => s.Length > 0).ToList()));
        Reg1("strcat", a =>
        {
            if (a.Any(v => v is CellValue))
            {
                var cell = (CellValue)a.First(v => v is CellValue);
                var res = new CellValue(cell.R, cell.C);
                for (int k = 0; k < cell.Numel; k++)
                    res.Data[k] = Str(string.Concat(a.Select(v => v is CellValue c ? S(c.Data[c.Numel == 1 ? 0 : k], "strcat") : S(v, "strcat"))));
                return res;
            }
            // char args: trailing whitespace of each char arg is removed
            return Str(string.Concat(a.Select(v => v is MatrixValue m && m.IsChar ? m.AsString().TrimEnd(' ', '\t', '\n', '\0') : S(v, "strcat"))));
        });
        Reg1("strtrim", a => MapStrings(a[0], "strtrim", s => Str(s.Trim(' ', '\t', '\n', '\v', '\f', '\r', '\0'))));
        Reg1("deblank", a => MapStrings(a[0], "deblank", s => Str(s.TrimEnd(' ', '\t', '\n', '\v', '\f', '\r', '\0'))));
        BuiltinFn upper = (it, a, n) => Out(a[0] is MatrixValue m && !m.IsChar ? m : MapStrings(a[0], "upper", s => Str(s.ToUpperInvariant())));
        BuiltinFn lower = (it, a, n) => Out(a[0] is MatrixValue m && !m.IsChar ? m : MapStrings(a[0], "lower", s => Str(s.ToLowerInvariant())));
        Reg("upper", (it, a, n) => a[0] is MatrixValue m && m.IsChar && m.R > 1 ? Out(CharMatrix(StrList(m, "upper").Select(s => s.ToUpperInvariant()).ToList())) : upper(it, a, n));
        Reg("lower", (it, a, n) => a[0] is MatrixValue m && m.IsChar && m.R > 1 ? Out(CharMatrix(StrList(m, "lower").Select(s => s.ToLowerInvariant()).ToList())) : lower(it, a, n));
        Reg("toupper", upper); Reg("tolower", lower);
        Reg1("strcmp", a => { NArgs(a, 2, 2, "strcmp"); return CompareStr(a[0], a[1], (x, y) => x == y); });
        Reg1("strcmpi", a => CompareStr(a[0], a[1], (x, y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase)));
        Reg1("strncmp", a => { int n = I(a[2], "strncmp"); return CompareStr(a[0], a[1], (x, y) => x.Length >= n && y.Length >= n && x[..n] == y[..n]); });
        Reg1("strncmpi", a => { int n = I(a[2], "strncmpi"); return CompareStr(a[0], a[1], (x, y) => x.Length >= n && y.Length >= n && string.Equals(x[..n], y[..n], StringComparison.OrdinalIgnoreCase)); });
        Reg1("strfind", a =>
        {
            var pat = S(a[1], "strfind");
            Value F(string s)
            {
                var hits = new List<double>();
                if (pat.Length > 0) for (int k = s.IndexOf(pat, StringComparison.Ordinal); k >= 0; k = s.IndexOf(pat, k + 1, StringComparison.Ordinal)) hits.Add(k + 1);
                return hits.Count == 0 ? new MatrixValue(1, 0) : MatrixValue.Row(hits);
            }
            return MapStrings(a[0], "strfind", F);
        });
        Reg1("strrep", a =>
        {
            string from = S(a[1], "strrep"), to = S(a[2], "strrep");
            return MapStrings(a[0], "strrep", s => Str(from.Length == 0 ? s : s.Replace(from, to)));
        });
        Reg1("strsplit", a =>
        {
            var s = S(a[0], "strsplit");
            string[] dels = a.Length > 1 ? (a[1] is CellValue dc ? dc.Data.Select(d => Printf.Escapes(S(d, "strsplit"))).ToArray() : new[] { Printf.Escapes(S(a[1], "strsplit")) }) : new[] { " " };
            var parts = s.Split(dels, StringSplitOptions.None);
            return CellValue.RowOf(parts.Select(Str));
        });
        Reg1("ostrsplit", a =>
        {
            var s = S(a[0], "ostrsplit"); var seps = S(a[1], "ostrsplit").ToCharArray();
            return CellValue.RowOf(s.Split(seps).Select(Str));
        });
        Reg1("strjoin", a =>
        {
            var c = a[0] as CellValue ?? throw new OctaveException("strjoin: CSTR must be a cell array of strings");
            var d = a.Length > 1 ? Printf.Escapes(S(a[1], "strjoin")) : " ";
            return Str(string.Join(d, c.Data.Select(x => S(x, "strjoin"))));
        });
        Reg("strtok", (it, a, n) =>
        {
            var s = S(a[0], "strtok"); var delim = a.Length > 1 ? S(a[1], "strtok") : " \t\n\v\f\r\0";
            int i = 0; while (i < s.Length && delim.Contains(s[i])) i++;
            int j = i; while (j < s.Length && !delim.Contains(s[j])) j++;
            return Out(Str(s[i..j]), Str(s[j..]));
        });
        Reg1("blanks", a => Str(new string(' ', I(a[0], "blanks"))));
        Reg1("substr", a => { var s = S(a[0], "substr"); int o = I(a[1], "substr"); int l = a.Length > 2 ? I(a[2], "substr") : s.Length - o + 1; return Str(s.Substring(o - 1, l)); });
        Reg1("index", a => Num(S(a[0], "index").IndexOf(S(a[1], "index"), StringComparison.Ordinal) + 1));
        Reg1("rindex", a => Num(S(a[0], "rindex").LastIndexOf(S(a[1], "rindex"), StringComparison.Ordinal) + 1));
        Reg1("regexprep", a =>
        {
            var pat = ConvertRegex(S(a[1], "regexprep"));
            var rep = Regex.Replace(S(a[2], "regexprep"), @"\$(\d)", "$${$1}");
            bool once = a.Skip(3).Any(v => IsString(v) && S(v, "regexprep") == "once");
            bool icase = a.Skip(3).Any(v => IsString(v) && S(v, "regexprep") == "ignorecase");
            var re = new Regex(pat, icase ? RegexOptions.IgnoreCase : RegexOptions.None);
            return MapStrings(a[0], "regexprep", s => Str(once ? re.Replace(s, rep, 1) : re.Replace(s, rep)));
        });
        Reg("regexp", (it, a, n) => RegexpImpl(a, n, false));
        Reg("regexpi", (it, a, n) => RegexpImpl(a, n, true));
        Reg1("validatestring", a =>
        {
            var s = S(a[0], "validatestring"); var opts = (CellValue)a[1];
            foreach (var o in opts.Data) { var os = S(o, "validatestring"); if (os.StartsWith(s, StringComparison.OrdinalIgnoreCase)) return Str(os); }
            throw new OctaveException($"validatestring: '{s}' does not match any valid string");
        });
        Reg1("dec2bin", a =>
        {
            var m = M(a[0], "dec2bin"); int len = a.Length > 1 ? I(a[1], "dec2bin") : 0;
            var rows = m.Re.Select(x => Convert.ToString((long)x, 2)).ToList();
            int w = Math.Max(len, rows.Max(r => r.Length));
            return CharMatrix(rows.Select(r => r.PadLeft(w, '0')).ToList());
        });
        Reg1("dec2hex", a => { var m = M(a[0], "dec2hex"); int len = a.Length > 1 ? I(a[1], "dec2hex") : 0; var rows = m.Re.Select(x => ((long)x).ToString("X")).ToList(); int w = Math.Max(len, rows.Max(r => r.Length)); return CharMatrix(rows.Select(r => r.PadLeft(w, '0')).ToList()); });
        Reg1("bin2dec", a => { var l = StrList(a[0], "bin2dec"); return MatrixValue.Col(l.Select(s => (double)Convert.ToInt64(s.Replace(" ", ""), 2))); });
        Reg1("hex2dec", a => { var l = StrList(a[0], "hex2dec"); return MatrixValue.Col(l.Select(s => (double)Convert.ToInt64(s, 16))); });
        Reg1("dec2base", a => { long x = (long)D(a[0], "dec2base"); int b = I(a[1], "dec2base"); const string dig = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"; var sb = new StringBuilder(); do { sb.Insert(0, dig[(int)(x % b)]); x /= b; } while (x > 0); return Str(sb.ToString()); });
        Reg1("iscellstr", a => Bool(a[0] is CellValue c && c.Data.All(v => v is MatrixValue m && (m.IsChar || (m.R == 0 && m.C == 0 && false)) )));
        Reg1("isstrprop", a =>
        {
            var t = S(a[1], "isstrprop");
            Func<char, bool> f = t switch
            {
                "alpha" => char.IsLetter, "digit" => char.IsDigit, "alnum" or "alphanum" => char.IsLetterOrDigit, "upper" => char.IsUpper,
                "lower" => char.IsLower, "wspace" or "space" => char.IsWhiteSpace, "punct" => char.IsPunctuation, _ => throw new OctaveException($"isstrprop: invalid string type: \"{t}\"")
            };
            return CharPred(a[0], f);
        });
        Reg1("toascii", a => M(a[0], "toascii").AsDouble());
        Reg1("do_string_escapes", a => Str(Printf.Escapes(S(a[0], "do_string_escapes"))));
        Reg1("undo_string_escapes", a => Str(S(a[0], "undo_string_escapes").Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\"", "\\\"")));
        Reg1("cellstr", a =>
        {
            if (a[0] is CellValue c) return c;
            return CellValue.ColOf(StrList(a[0], "cellstr").Select(s => Str(s.TrimEnd())));
        });
    }

    static Value[] RegexpImpl(Value[] a, int nargout, bool icase)
    {
        var pat = ConvertRegex(S(a[1], "regexp"));
        var opts = a.Skip(2).Select(v => S(v, "regexp")).ToList();
        bool once = opts.Remove("once");
        if (opts.Remove("ignorecase")) icase = true;
        opts.Remove("matchcase");
        var re = new Regex(pat, icase ? RegexOptions.IgnoreCase : RegexOptions.None);

        if (a[0] is CellValue cin)
        {
            // cell input: each output is a cell of per-string results
            var per = cin.Data.Select(v => RegexpImpl(new[] { v, a[1] }.Concat(a.Skip(2)).ToArray(), Math.Max(nargout, 1), icase)).ToList();
            int no = Math.Max(nargout, 1);
            return Enumerable.Range(0, no).Select(k => (Value)new CellValue(cin.R, cin.C, per.Select(p => p[k]).ToArray())).ToArray();
        }

        var s = S(a[0], "regexp");
        var matches = re.Matches(s).Cast<Match>().Where(m => true).ToList();
        if (once) matches = matches.Take(1).ToList();
        var names = re.GetGroupNames().Where(g => !int.TryParse(g, out _)).ToList();

        Value Tokens(Match m)
        {
            var toks = new List<Value>();
            for (int g = 1; g < m.Groups.Count; g++) toks.Add(Str(m.Groups[g].Value));
            if (toks.Count == 0) toks.Add(Str(m.Value));
            return CellValue.RowOf(toks);
        }
        Value Names()
        {
            var st = new StructValue();
            foreach (var nm in names) st.Set(nm, matches.Count > 0 ? Str(matches[0].Groups[nm].Value) : Str(""));
            return st;
        }
        Value Split()
        {
            var parts = new List<Value>(); int pos = 0;
            foreach (var m in matches) { parts.Add(Str(s[pos..m.Index])); pos = m.Index + m.Length; }
            parts.Add(Str(s[pos..]));
            return CellValue.RowOf(parts);
        }
        Value Get(string what)
        {
            switch (what)
            {
                case "match": return once ? (matches.Count > 0 ? Str(matches[0].Value) : Str("")) : CellValue.RowOf(matches.Select(m => Str(m.Value)));
                case "tokens": return once ? (matches.Count > 0 ? Tokens(matches[0]) : new CellValue(1, 0)) : CellValue.RowOf(matches.Select(Tokens));
                case "start": return once ? (matches.Count > 0 ? Num(matches[0].Index + 1) : new MatrixValue(1, 0)) : (matches.Count == 0 ? new MatrixValue(1, 0) : MatrixValue.Row(matches.Select(m => m.Index + 1.0)));
                case "end": return once ? (matches.Count > 0 ? Num(matches[0].Index + matches[0].Length) : new MatrixValue(1, 0)) : (matches.Count == 0 ? new MatrixValue(1, 0) : MatrixValue.Row(matches.Select(m => (double)(m.Index + m.Length))));
                case "names": return Names();
                case "split": return Split();
                case "tokenExtents":
                    return CellValue.RowOf(matches.Select(m =>
                    {
                        int ng = Math.Max(m.Groups.Count - 1, 1); var te = new MatrixValue(ng, 2);
                        for (int g = 0; g < ng; g++) { var gr = m.Groups.Count > 1 ? m.Groups[g + 1] : m.Groups[0]; te.Re[g] = gr.Index + 1; te.Re[g + ng] = gr.Index + gr.Length; }
                        return (Value)te;
                    }));
            }
            throw new OctaveException($"regexp: unknown option \"{what}\"");
        }
        var order = opts.Count > 0 ? opts : new List<string> { "start", "end", "tokenExtents", "match", "tokens", "names", "split" };
        int count = Math.Max(1, Math.Min(Math.Max(nargout, 1), order.Count));
        if (opts.Count > 0) count = Math.Max(count, Math.Min(order.Count, Math.Max(nargout, 1)));
        return order.Take(count).Select(Get).ToArray();
    }
}
