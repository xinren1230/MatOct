// MatRewrite - GPL-3.0-or-later
using System.Text;
using System.Text.RegularExpressions;
using MatRewrite.Core.Runtime;
using static MatRewrite.Core.Runtime.Tagged;
namespace MatRewrite.Core.Builtins;

/// <summary>Phase 13: MATLAB string-array functions, operating on char rows and cellstr arrays.</summary>
public static partial class BuiltinLibrary
{
    static bool IsTextual(Value v) => v is MatrixValue m && (m.IsChar || m.Numel == 0) || v is CellValue;
    static string Txt(Value v, string fn) => v is MatrixValue m && m.IsChar ? (m.R <= 1 ? m.AsString() : throw new OctaveException($"{fn}: char input must be a row vector")) : v is MatrixValue e && e.Numel == 0 ? "" : Is(v, "categorical") || Is(v, "datetime") ? ElemString(v, 0) : throw new OctaveException($"{fn}: input must be text");
    static Value MapS(Value v, string fn, Func<string, string> f)
    {
        if (v is CellValue c) return new CellValue(c.R, c.C, c.Data.Select(x => (Value)Str(f(Txt(x, fn)))).ToArray()).WithDims<CellValue>(c.Dims);
        if (v is StructValue s && s.Tag != null) { var l = CellStrs(s); var r = new CellValue(s.Dims[0], s.Dims[1], l.Select(x => (Value)Str(f(x))).ToArray()); return r; }
        if (v is MatrixValue m && m.IsChar && m.R > 1) { var rows = Enumerable.Range(0, m.R).Select(i => f(m.RowString(i))).ToList(); return CharMat(rows); }
        return Str(f(Txt(v, fn)));
    }
    static Value MapN(Value v, string fn, Func<string, double> f, bool logical = false)
    {
        List<string> items; int[] dims;
        if (v is CellValue c) { items = c.Data.Select(x => Txt(x, fn)).ToList(); dims = c.Dims; }
        else if (v is StructValue s && s.Tag != null) { items = CellStrs(s); dims = s.Dims; }
        else { items = new() { Txt(v, fn) }; dims = new[] { 1, 1 }; }
        var m = new MatrixValue(1, items.Count, logical ? NumKind.Logical : NumKind.Double);
        for (int i = 0; i < items.Count; i++) m.Re[i] = f(items[i]);
        m.SetDims(dims); return m;
    }
    static List<string> Pats(Value v, string fn) => v is CellValue c ? c.Data.Select(x => Txt(x, fn)).ToList() : new() { Txt(v, fn) };
    static bool IgnoreCase(Value[] a, int from) { for (int i = from; i + 1 < a.Length; i++) if (IsString(a[i]) && S(a[i], "").Equals("IgnoreCase", StringComparison.OrdinalIgnoreCase)) return Ops.IsTrue(a[i + 1]); return false; }
    static int CountOcc(string s, string p, StringComparison sc) { if (p == "") return 0; int n = 0, i = 0; while ((i = s.IndexOf(p, i, sc)) >= 0) { n++; i += p.Length; } return n; }

    static void RegisterPhase13Strings()
    {
        var B = cur!.Builtins;
        Reg1("strlength", a => MapN(a[0], "strlength", s => s.Length));
        Reg1("contains", a => { var p = Pats(a[1], "contains"); var sc = IgnoreCase(a, 2) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; return MapN(a[0], "contains", s => p.Any(q => s.Contains(q, sc)) ? 1 : 0, true); });
        Reg1("matches", a => { var p = Pats(a[1], "matches"); var sc = IgnoreCase(a, 2) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; return MapN(a[0], "matches", s => p.Any(q => s.Equals(q, sc)) ? 1 : 0, true); });
        Reg1("count", a => { var p = Pats(a[1], "count"); var sc = IgnoreCase(a, 2) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal; return MapN(a[0], "count", s => p.Sum(q => CountOcc(s, q, sc))); });
        Reg1("replace", a =>
        {
            var olds = Pats(a[1], "replace"); var news = Pats(a[2], "replace");
            return MapS(a[0], "replace", s =>
            {
                for (int i = 0; i < olds.Count; i++) if (olds[i] != "") s = s.Replace(olds[i], news.Count == 1 ? news[0] : news[i]);
                return s;
            });
        });
        Reg1("erase", a => { var p = Pats(a[1], "erase"); return MapS(a[0], "erase", s => { foreach (var q in p) if (q != "") s = s.Replace(q, ""); return s; }); });
        Reg1("reverse", a => MapS(a[0], "reverse", s => new string(s.Reverse().ToArray())));
        if (!B.ContainsKey("type")) Reg0("type", (it, a) => { foreach (var f in a) { var p = S(f, "type"); var q = File.Exists(p) ? p : File.Exists(p + ".m") ? p + ".m" : throw new OctaveException($"type: '{p}' undefined"); it.Out.Write(File.ReadAllText(q)); it.Out.WriteLine(); } });
        Reg1("newline", a => Str("\n"));
        Reg1("strip", a =>
        {
            string side = a.Length > 1 && IsString(a[1]) && S(a[1], "") is "left" or "right" or "both" ? S(a[1], "") : "both";
            var chars = a.Length > 2 ? Txt(a[2], "strip").ToCharArray() : a.Length == 2 && side == "both" && !(S(a[1], "") is "both") ? Txt(a[1], "strip").ToCharArray() : null;
            return MapS(a[0], "strip", s => side switch
            {
                "left" => chars == null ? s.TrimStart() : s.TrimStart(chars),
                "right" => chars == null ? s.TrimEnd() : s.TrimEnd(chars),
                _ => chars == null ? s.Trim() : s.Trim(chars)
            });
        });
        Reg1("pad", a =>
        {
            int? w = a.Length > 1 && a[1] is MatrixValue wm && !wm.IsChar ? (int)wm.Re[0] : null;
            int k = w != null ? 2 : 1;
            string side = a.Length > k && IsString(a[k]) && S(a[k], "") is "left" or "right" or "both" ? S(a[k++], "") : "right";
            char pc = a.Length > k ? Txt(a[k], "pad")[0] : ' ';
            int width = w ?? (a[0] is CellValue c ? c.Data.Max(x => Txt(x, "pad").Length) : Txt(a[0], "pad").Length);
            return MapS(a[0], "pad", s =>
            {
                if (s.Length >= width) return s;
                int tot = width - s.Length;
                return side switch { "left" => new string(pc, tot) + s, "both" => new string(pc, tot / 2) + s + new string(pc, tot - tot / 2), _ => s + new string(pc, tot) };
            });
        });
        Func<string, Value, bool, string> extract = (s, p, before) =>
        {
            if (p is MatrixValue pm && !pm.IsChar) { int pos = (int)pm.Re[0]; return before ? s[..Math.Clamp(pos - 1, 0, s.Length)] : s[Math.Clamp(pos, 0, s.Length)..]; }
            var q = Txt(p, "extract"); int i = s.IndexOf(q, StringComparison.Ordinal);
            if (i < 0) return "";
            return before ? s[..i] : s[(i + q.Length)..];
        };
        Reg1("extractBefore", a => MapS(a[0], "extractBefore", s => extract(s, a[1], true)));
        Reg1("extractAfter", a => MapS(a[0], "extractAfter", s => extract(s, a[1], false)));
        Reg1("extractBetween", a => MapS(a[0], "extractBetween", s =>
        {
            if (a[1] is MatrixValue p1 && !p1.IsChar) { int st = (int)p1.Re[0], en = (int)M(a[2], "").Re[0]; return s.Substring(st - 1, Math.Max(0, en - st + 1)); }
            var x = Txt(a[1], ""); var y = Txt(a[2], ""); int i = s.IndexOf(x, StringComparison.Ordinal); if (i < 0) return "";
            int j = s.IndexOf(y, i + x.Length, StringComparison.Ordinal); return j < 0 ? "" : s[(i + x.Length)..j];
        }));
        Reg1("insertBefore", a => MapS(a[0], "insertBefore", s => { var t = Txt(a[2], ""); if (a[1] is MatrixValue p && !p.IsChar) return s.Insert((int)p.Re[0] - 1, t); var q = Txt(a[1], ""); int i = s.IndexOf(q, StringComparison.Ordinal); return i < 0 ? s : s.Insert(i, t); }));
        Reg1("insertAfter", a => MapS(a[0], "insertAfter", s => { var t = Txt(a[2], ""); if (a[1] is MatrixValue p && !p.IsChar) return s.Insert((int)p.Re[0], t); var q = Txt(a[1], ""); int i = s.IndexOf(q, StringComparison.Ordinal); return i < 0 ? s : s.Insert(i + q.Length, t); }));
        Reg1("split", a =>
        {
            var dels = a.Length > 1 ? Pats(a[1], "split") : null;
            List<string> Sp(string s) => dels == null ? s.Split(new[] { ' ', '\t', '\n', '\r', '\f', '\v' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                : Regex.Split(s, string.Join("|", dels.OrderByDescending(d => d.Length).Select(Regex.Escape))).ToList();
            if (a[0] is CellValue c && c.Numel != 1)
            {
                var parts = c.Data.Select(x => Sp(Txt(x, "split"))).ToList();
                int m = parts.Count == 0 ? 0 : parts[0].Count;
                if (parts.Any(p => p.Count != m)) throw new OctaveException("split: all elements must split into the same number of parts");
                var r = new CellValue(parts.Count, m);
                for (int i = 0; i < parts.Count; i++) for (int j = 0; j < m; j++) r.Data[i + j * parts.Count] = Str(parts[i][j]);
                return r;
            }
            var s0 = a[0] is CellValue c1 ? Txt(c1.Data[0], "split") : Txt(a[0], "split");
            return TCol(Sp(s0));
        });
        Reg1("splitlines", a => TCol(Txt(a[0] is CellValue c ? c.Data[0] : a[0], "splitlines").Replace("\r\n", "\n").Split('\n')));
        Reg1("join", a =>
        {
            if (a[0] is MatrixValue m && m.IsChar) return m;
            var c = (CellValue)a[0];
            var d = a.Length > 1 && !(a[1] is MatrixValue dm && !dm.IsChar) ? Pats(a[1], "join") : new() { " " };
            string J(IList<string> xs) { var sb = new StringBuilder(); for (int i = 0; i < xs.Count; i++) { if (i > 0) sb.Append(d.Count == 1 ? d[0] : d[i - 1]); sb.Append(xs[i]); } return sb.ToString(); }
            int dim = a.Length > 2 ? I(a[2], "join") : a.Length == 2 && a[1] is MatrixValue dd && !dd.IsChar ? (int)dd.Re[0] : (c.C > 1 ? 2 : 1);
            var strs = c.Data.Select(x => Txt(x, "join")).ToArray();
            if (c.R == 1 || c.C == 1) return Str(J(strs));
            if (dim == 2) return TCol(Enumerable.Range(0, c.R).Select(i => J(Enumerable.Range(0, c.C).Select(j => strs[i + j * c.R]).ToList())));
            return TRow(Enumerable.Range(0, c.C).Select(j => J(Enumerable.Range(0, c.R).Select(i => strs[i + j * c.R]).ToList())));
        });
        Reg1("compose", a =>
        {
            var fmts = Pats(a[0], "compose");
            if (a.Length == 1) return MapS(a[0], "compose", s => Printf.Format(Printf.Escapes(s), Array.Empty<Value>()));
            var args = a.Skip(1).ToList();
            int rows = args.Max(x => x is CellValue cc ? cc.R : x.Dims[0]);
            if (args.Count == 1 && args[0] is MatrixValue r1 && r1.R == 1 && r1.C > 1 && fmts.Count == 1 && CountSpecs(fmts[0]) < r1.C && CountSpecs(fmts[0]) == 1) rows = 1;
            var outs = new List<string>();
            for (int i = 0; i < rows; i++)
            {
                var row = new List<Value>();
                foreach (var x in args)
                {
                    if (x is CellValue cc) { int j0 = cc.R == 1 ? 0 : i; for (int j = 0; j < cc.C; j++) row.Add(cc.Data[j0 + j * cc.R]); }
                    else if (x is MatrixValue m && m.IsChar) row.Add(m.R > 1 ? Str(m.RowString(i)) : m);
                    else if (x is MatrixValue m2) { int i0 = m2.R == 1 ? 0 : i; for (int j = 0; j < m2.C; j++) row.Add(Num(m2.Re[i0 + j * m2.R])); }
                }
                outs.Add(Printf.Format(Printf.Escapes(fmts.Count == 1 ? fmts[0] : fmts[i]), row));
            }
            return outs.Count == 1 ? Str(outs[0]) : TCol(outs);
        });
        Reg1("string", a =>
        {
            var v = a[0];
            if (v is MatrixValue m && m.IsChar) return m.R <= 1 ? m : TCol(Enumerable.Range(0, m.R).Select(i => m.RowString(i)));
            if (v is CellValue c) return new CellValue(c.R, c.C, c.Data.Select(x => (Value)Str(x is MatrixValue xm && xm.IsChar ? xm.AsString() : x is MatrixValue xn && xn.Numel == 1 ? N2S(xn) : throw new OctaveException("string: cell elements must be text or scalars"))).ToArray()).WithDims<CellValue>(c.Dims);
            if (v is StructValue s && s.Tag != null && !IsTable(s)) { var l = CellStrs(s); return l.Count == 1 ? Str(l[0]) : new CellValue(s.Dims[0], s.Dims[1], l.Select(x => (Value)Str(x)).ToArray()); }
            if (v is MatrixValue n)
            {
                string F(int k) => n.IsLogical ? (n.Re[k] != 0 ? "true" : "false") : n.Is64 ? n.ExactString(k) : N2S((MatrixValue)n.Gather(new[] { k }, 1, 1));
                if (n.Numel == 1) return Str(F(0));
                var r = new CellValue(n.R, n.C, Enumerable.Range(0, n.Numel).Select(k => (Value)Str(F(k))).ToArray()); return r.WithDims<CellValue>(n.Dims);
            }
            throw new OctaveException("string: unsupported input");
        });
        Reg1("strings", a =>
        {
            int r = a.Length > 0 ? I(a[0], "strings") : 1, c = a.Length > 1 ? I(a[1], "strings") : a.Length == 1 ? r : 1;
            if (a.Length == 0) return Str("");
            var cv = new CellValue(r, c); for (int i = 0; i < cv.Numel; i++) cv.Data[i] = Str(""); return cv;
        });
        Reg1("isstring", a => a.Length != 1 ? throw new OctaveException("Invalid call to isstring") : Bool(false));
        Reg1("isStringScalar", a => Bool(a[0] is MatrixValue m && m.IsChar && m.R <= 1));
        Reg1("ischarlike", a => Bool(IsTextual(a[0])));
        Reg1("convertStringsToChars", a => a[0]);
        Reg1("convertCharsToStrings", a => a[0]);
        Reg1("isletter", a => a[0] is MatrixValue m ? Ops.Map(m, x => char.IsLetter((char)x) ? 1 : 0, null, null, NumKind.Logical) : Bool(false));
        Reg1("lettersPattern", a => Str("[A-Za-z]+"));
        Reg1("digitsPattern", a => Str("\\d+"));
    }
    static string N2S(MatrixValue m) => ((MatrixValue)Num2StrValue(m, null)).AsString();
    static CellValue TRow(IEnumerable<string> s) => CellValue.RowOf(s.Select(x => (Value)MatrixValue.Str(x)));
    static int CountSpecs(string f) => Regex.Matches(f.Replace("%%", ""), "%[-+ #0]*[\\d*]*(\\.[\\d*]+)?[diouxXfeEgGcs]").Count;
}
