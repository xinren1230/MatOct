// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>C-style printf used by sprintf/fprintf/printf with Octave's argument recycling.</summary>
public static class Printf
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Escapes(string s)
    {
        if (!s.Contains('\\')) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
            char e = s[++i];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'a': sb.Append('\a'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'v': sb.Append('\v'); break;
                case '\\': sb.Append('\\'); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append('\''); break;
                case '0': sb.Append('\0'); break;
                default: sb.Append('\\').Append(e); break;
            }
        }
        return sb.ToString();
    }

    sealed class Spec { public string Flags = ""; public int? Width; public int? Prec; public char Conv; public bool StarW, StarP; }

    // items: either string (from char args) or double
    public static string Format(string fmt, IList<Value> args)
    {
        var items = new List<object>();
        foreach (var a in args)
        {
            if (a is MatrixValue m)
            {
                if (m.IsChar) { items.Add(m.AsString()); continue; }
                for (int k = 0; k < m.Numel; k++) items.Add(m.Re[k]);
            }
            else if (a is CellValue) throw new OctaveException("printf: wrong type argument 'cell array'");
            else if (a is StructValue) throw new OctaveException("printf: wrong type argument 'struct'");
            else items.Add(a.ToString() ?? "");
        }
        // parse template into literal / spec pieces
        var pieces = new List<object>();
        var lit = new StringBuilder();
        for (int i = 0; i < fmt.Length; i++)
        {
            char c = fmt[i];
            if (c != '%') { lit.Append(c); continue; }
            if (i + 1 < fmt.Length && fmt[i + 1] == '%') { lit.Append('%'); i++; continue; }
            int j = i + 1;
            var sp = new Spec();
            while (j < fmt.Length && "-+ 0#".IndexOf(fmt[j]) >= 0) sp.Flags += fmt[j++];
            if (j < fmt.Length && fmt[j] == '*') { sp.StarW = true; j++; }
            else { int s = j; while (j < fmt.Length && char.IsDigit(fmt[j])) j++; if (j > s) sp.Width = int.Parse(fmt[s..j]); }
            if (j < fmt.Length && fmt[j] == '.')
            {
                j++;
                if (j < fmt.Length && fmt[j] == '*') { sp.StarP = true; j++; }
                else { int s = j; while (j < fmt.Length && char.IsDigit(fmt[j])) j++; sp.Prec = j > s ? int.Parse(fmt[s..j]) : 0; }
            }
            while (j < fmt.Length && "lhLqjzt".IndexOf(fmt[j]) >= 0) j++;
            if (j >= fmt.Length) { lit.Append(fmt[i..]); break; }
            sp.Conv = fmt[j];
            if (lit.Length > 0) { pieces.Add(lit.ToString()); lit.Clear(); }
            pieces.Add(sp);
            i = j;
        }
        if (lit.Length > 0) pieces.Add(lit.ToString());

        var sb = new StringBuilder();
        int nspec = pieces.Count(p => p is Spec);
        if (nspec == 0 || items.Count == 0)
        {
            foreach (var p in pieces)
                if (p is string s) sb.Append(s);
                else if (items.Count == 0 && p is Spec sp0 && (sp0.Conv == 's' || sp0.Conv == 'c') && sp0.Width is int w0) sb.Append(new string(' ', w0));
            // a template without conversions is printed once per call
            return sb.ToString();
        }
        int idx = 0;
        while (idx < items.Count)
        {
            foreach (var p in pieces)
            {
                if (p is string s) { sb.Append(s); continue; }
                var sp = (Spec)p;
                if (idx >= items.Count) { if (idx > 0) break; continue; }
                int? width = sp.Width, prec = sp.Prec;
                if (sp.StarW) { width = (int)ToNum(items[idx++]); if (idx >= items.Count) break; }
                if (sp.StarP) { prec = (int)ToNum(items[idx++]); if (idx >= items.Count) break; }
                var item = items[idx++];
                sb.Append(FormatOne(sp.Conv, sp.Flags, width, prec, item, items, ref idx));
            }
        }
        return sb.ToString();
    }

    static double ToNum(object o) => o is double d ? d : (o is string s && s.Length > 0 ? s[0] : 0);

    static string FormatOne(char conv, string flags, int? width, int? prec, object item, List<object> items, ref int idx)
    {
        string body;
        switch (conv)
        {
            case 's':
                if (item is string str) body = prec is int p0 && p0 < str.Length ? str[..p0] : str;
                else
                {
                    double d = (double)item;
                    body = d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ShortG(d) : ShortG(d);
                    if (prec is int p1) body = FormatG(d, p1, flags.Contains('#'));
                }
                return Pad(body, width, flags, false);
            case 'c':
                if (item is string cs) return Pad(cs, width, flags, false);
                return Pad(((char)(int)(double)item).ToString(), width, flags, false);
            case 'd': case 'i': case 'u':
            case 'x': case 'X': case 'o':
            case 'f': case 'F': case 'e': case 'E': case 'g': case 'G':
            {
                if (item is string s2)
                {
                    // numeric conversion of a string argument: Octave prints the string
                    return Pad(s2, width, flags.Replace("0", ""), false);
                }
                double d = (double)item;
                char cv = conv;
                if ((cv is 'd' or 'i' or 'u' or 'x' or 'X' or 'o') && (d != Math.Floor(d) || double.IsInfinity(d) || double.IsNaN(d)))
                {
                    // non-integer value with integer conversion -> %f / %g style
                    if (double.IsInfinity(d) || double.IsNaN(d)) cv = 'f';
                    else { cv = 'f'; if (prec == null) { body = ShortG(d); return PadNum(body, width, flags); } }
                }
                body = cv switch
                {
                    'd' or 'i' or 'u' => ((long)d).ToString(Inv).TrimStart('-'),
                    'x' => ((long)d).ToString("x", Inv),
                    'X' => ((long)d).ToString("X", Inv),
                    'o' => Convert.ToString((long)d, 8),
                    'f' or 'F' => NonFinite(d) ?? Math.Abs(d).ToString("F" + (prec ?? 6), Inv),
                    'e' or 'E' => NonFinite(d) ?? Display.FormatE(Math.Abs(d), prec ?? 6),
                    'g' or 'G' => NonFinite(d) ?? FormatG(Math.Abs(d), prec ?? 6, flags.Contains('#')),
                    _ => ""
                };
                if (cv == 'E' || cv == 'G') body = body.ToUpperInvariant();
                if (body.StartsWith("-")) body = body[1..];
                string sign = (d < 0 || (d == 0 && double.IsNegative(d) && cv is 'f' or 'e' or 'g')) && !double.IsNaN(d) ? "-" : flags.Contains('+') ? "+" : flags.Contains(' ') ? " " : "";
                if (cv is 'x' or 'X' or 'o') sign = "";
                bool zero = flags.Contains('0') && !flags.Contains('-') && !(double.IsNaN(d) || double.IsInfinity(d));
                if (width is int w && zero && sign.Length + body.Length < w) body = new string('0', w - sign.Length - body.Length) + body;
                return Pad(sign + body, width, flags, true);
            }
            default:
                return "%" + conv;
        }
    }

    static string PadNum(string body, int? width, string flags) => Pad(body, width, flags, true);

    static string? NonFinite(double d) => double.IsNaN(d) ? "NaN" : double.IsInfinity(d) ? "Inf" : null;

    static string Pad(string s, int? width, string flags, bool numeric)
    {
        if (width is not int w || s.Length >= w) return s;
        return flags.Contains('-') ? s.PadRight(w) : s.PadLeft(w);
    }

    /// <summary>%g formatting (C semantics).</summary>
    public static string FormatG(double d, int prec, bool alt = false)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d > 0 ? "Inf" : "-Inf";
        if (prec == 0) prec = 1;
        if (d == 0) return alt ? "0." + new string('0', prec - 1) : "0";
        string e = Display.FormatE(d, prec - 1);
        int ei = e.IndexOf('e');
        int x = int.Parse(e[(ei + 1)..], Inv);
        string r;
        if (x < -4 || x >= prec) r = e;
        else r = d.ToString("F" + Math.Max(prec - 1 - x, 0), Inv);
        if (!alt)
        {
            int ePos = r.IndexOf('e');
            string mant = ePos >= 0 ? r[..ePos] : r, exp = ePos >= 0 ? r[ePos..] : "";
            if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
            r = mant + exp;
        }
        return r;
    }

    /// <summary>Shortest reasonable representation (like %g with up to 5..17 digits as Octave does for %d of non-integers).</summary>
    public static string ShortG(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsInfinity(d)) return d > 0 ? "Inf" : "-Inf";
        if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(Inv);
        return FormatG(d, 6);
    }
}
