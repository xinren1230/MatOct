// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.Json;
using MatRewrite.Core.Runtime;
using static MatRewrite.Core.Runtime.Tagged;
namespace MatRewrite.Core.Builtins;

/// <summary>Phase 13: file I/O (tables, matrices, cells, lines, JSON) and file-system helpers.</summary>
public static partial class BuiltinLibrary
{
    static List<List<string>> ParseDelimited(string text, char? delim)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim() == "") lines.RemoveAt(lines.Count - 1);
        char d = delim ?? (lines.Count > 0 && lines[0].Contains(',') ? ',' : lines.Count > 0 && lines[0].Contains('\t') ? '\t' : lines.Count > 0 && lines[0].Contains(';') ? ';' : ' ');
        var rows = new List<List<string>>();
        foreach (var ln in lines)
        {
            var row = new List<string>(); var sb = new StringBuilder(); bool q = false;
            if (d == ' ') { rows.Add(ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList()); continue; }
            for (int i = 0; i < ln.Length; i++)
            {
                char ch = ln[i];
                if (q) { if (ch == '"') { if (i + 1 < ln.Length && ln[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(ch); }
                else if (ch == '"') q = true;
                else if (ch == d) { row.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            row.Add(sb.ToString());
            rows.Add(row);
        }
        return rows;
    }
    static bool TryNum(string s, out double v)
    {
        s = s.Trim();
        if (s == "") { v = double.NaN; return true; }
        if (s.Equals("NaN", StringComparison.OrdinalIgnoreCase)) { v = double.NaN; return true; }
        if (s.Equals("Inf", StringComparison.OrdinalIgnoreCase)) { v = double.PositiveInfinity; return true; }
        if (s.Equals("-Inf", StringComparison.OrdinalIgnoreCase)) { v = double.NegativeInfinity; return true; }
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) { v = 1; return true; }
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) { v = 0; return true; }
        return double.TryParse(s, NumberStyles.Float, InvC, out v);
    }
    static string FileText(Interpreter it, Value f, string fn)
    {
        var p = S(f, fn);
        if (!File.Exists(p)) throw new OctaveException($"{fn}: unable to find file '{p}'");
        return File.ReadAllText(p);
    }
    static char? DelimOpt(Dictionary<string, Value> o)
    {
        if (!o.TryGetValue("Delimiter", out var d)) return null;
        var s = S(d, "Delimiter");
        return s switch { "comma" or "," => ',', "tab" or "\\t" or "\t" => '\t', "semi" or ";" => ';', "space" or " " => ' ', "bar" or "|" => '|', _ => s[0] };
    }
    static string CsvField(string s, char d) => s.IndexOfAny(new[] { d, '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    static string NumStr(double x) => double.IsNaN(x) ? "NaN" : double.IsPositiveInfinity(x) ? "Inf" : double.IsNegativeInfinity(x) ? "-Inf" : x.ToString("G15", InvC);
    static string FileDelim(string path, Dictionary<string, Value> o) => DelimOpt(o) is char c ? c.ToString() : path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dat") ? "," : ",";

    static void RegisterPhase13IO()
    {
        var B = cur!.Builtins;
        string[] rOpts = { "Delimiter", "ReadVariableNames", "ReadRowNames", "NumHeaderLines", "HeaderLines", "VariableNames", "FileType", "TextType", "Range", "OutputType", "VariableNamingRule" };
        Reg("readtable", (it, a, n) =>
        {
            var (pos, o) = SplitOpts(a, rOpts);
            var rows = ParseDelimited(FileText(it, pos[0], "readtable"), DelimOpt(o));
            int skip = o.TryGetValue("NumHeaderLines", out var nh) ? I(nh, "readtable") : o.TryGetValue("HeaderLines", out var hl) ? I(hl, "readtable") : 0;
            rows = rows.Skip(skip).ToList();
            bool hdr = !o.TryGetValue("ReadVariableNames", out var rv) || Ops.IsTrue(rv);
            if (hdr && rows.Count > 0 && !o.ContainsKey("ReadVariableNames") && rows[0].All(s => TryNum(s, out _) && s.Trim() != "")) hdr = false;
            bool preserve = o.TryGetValue("VariableNamingRule", out var vnr) && S(vnr, "") == "preserve";
            var header = hdr && rows.Count > 0 ? rows[0] : null;
            var body = rows.Skip(hdr ? 1 : 0).ToList();
            int w = Math.Max(header?.Count ?? 0, body.Count == 0 ? 0 : body.Max(r => r.Count));
            var names = Enumerable.Range(0, w).Select(j => header != null && j < header.Count && header[j].Trim() != "" ? (preserve ? header[j].Trim() : ValidName(header[j], j)) : "Var" + (j + 1)).ToList();
            if (o.TryGetValue("VariableNames", out var vn)) names = CellStrs(vn);
            for (int j = 0; j < names.Count; j++) for (int k = 0; k < j; k++) if (names[k] == names[j]) names[j] += "_" + (j + 1);
            var cols = new List<Value>();
            for (int j = 0; j < w; j++)
            {
                var cells = body.Select(r => j < r.Count ? r[j] : "").ToList();
                var nums = new double[cells.Count]; bool allNum = cells.Count > 0 && cells.Select((s, i) => TryNum(s, out nums[i])).All(b => b);
                if (allNum && cells.All(s => s.Trim() == "")) allNum = true;
                if (allNum) cols.Add(MatrixValue.Col(nums));
                else cols.Add(TCol(cells.Select(s => s.Trim())));
            }
            CellValue? rn = null;
            if (o.TryGetValue("ReadRowNames", out var rr) && Ops.IsTrue(rr) && w > 0)
            { rn = TCol(CellStrs(cols[0])); cols.RemoveAt(0); names.RemoveAt(0); }
            var t = MakeTable(names, cols, rn);
            if (cols.Count == 0) t.Meta!["H"] = body.Count;
            return One(t);
        });
        Reg0("writetable", (it, a) =>
        {
            var t = TableOf(a[0], "writetable");
            var (pos, o) = SplitOpts(a.Skip(1).ToArray(), "Delimiter", "WriteVariableNames", "WriteRowNames", "FileType", "WriteMode", "QuoteStrings");
            string path = pos.Count > 0 ? S(pos[0], "writetable") : "table.txt";
            char d = DelimOpt(o) ?? ',';
            bool wn = !o.TryGetValue("WriteVariableNames", out var wv) || Ops.IsTrue(wv);
            bool wr = o.TryGetValue("WriteRowNames", out var wrv) && Ops.IsTrue(wrv) && t.Meta!.ContainsKey("RowNames");
            bool tt = Is(t, "timetable");
            int h = Height(t);
            var cols = new List<(string name, int width, Func<int, int, string> get)>();
            if (tt) { var rt = (Value)t.Meta!["RowTimes"]; cols.Add(("Time", 1, (i, j) => ElemString(rt, i))); }
            if (wr) { var rn = (CellValue)t.Meta!["RowNames"]; cols.Add(("Row", 1, (i, j) => S(rn.Data[i], ""))); }
            foreach (var f in t.Fields)
            {
                var c = t.Elems[0][f];
                int cw = c is MatrixValue mm && !mm.IsChar ? Math.Max(1, mm.Numel / Math.Max(1, h)) : 1;
                cols.Add((f, cw, (i, j) => c switch
                {
                    MatrixValue m when m.IsChar => m.RowString(i),
                    MatrixValue m when m.IsLogical => m.Re[i + j * h] != 0 ? "1" : "0",
                    MatrixValue m => m.Is64 ? m.ExactString(i + j * h) : NumStr(m.Re[i + j * h]),
                    CellValue cc => cc.Data[i] is MatrixValue s && s.IsChar ? s.AsString() : cc.Data[i] is MatrixValue x && x.Numel == 1 ? NumStr(x.Re[0]) : "",
                    _ => ElemString(c, i)
                }));
            }
            var sb = new StringBuilder();
            if (wn) sb.Append(string.Join(d, cols.SelectMany(c => c.width == 1 ? new[] { c.name } : Enumerable.Range(1, c.width).Select(k => c.name + "_" + k)).Select(s => CsvField(s, d)))).Append('\n');
            for (int i = 0; i < h; i++)
                sb.Append(string.Join(d, cols.SelectMany(c => Enumerable.Range(0, c.width).Select(j => CsvField(c.get(i, j), d))))).Append('\n');
            bool app = o.TryGetValue("WriteMode", out var wm) && S(wm, "") == "append";
            if (app) File.AppendAllText(path, sb.ToString()); else File.WriteAllText(path, sb.ToString());
        });
        Reg("readmatrix", (it, a, n) =>
        {
            var (pos, o) = SplitOpts(a, rOpts);
            var rows = ParseDelimited(FileText(it, pos[0], "readmatrix"), DelimOpt(o));
            int skip = o.TryGetValue("NumHeaderLines", out var nh) ? I(nh, "readmatrix") : -1;
            if (skip < 0) { skip = 0; while (skip < rows.Count && rows[skip].Any(s => s.Trim() != "" && !TryNum(s, out _))) skip++; if (skip == rows.Count) skip = 0; }
            rows = rows.Skip(skip).ToList();
            int r = rows.Count, c = r == 0 ? 0 : rows.Max(x => x.Count);
            var m = new MatrixValue(r, c);
            for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) m.Re[i + j * r] = j < rows[i].Count && TryNum(rows[i][j], out var v) ? v : double.NaN;
            return One(m);
        });
        Reg("readcell", (it, a, n) =>
        {
            var (pos, o) = SplitOpts(a, rOpts);
            var rows = ParseDelimited(FileText(it, pos[0], "readcell"), DelimOpt(o));
            int r = rows.Count, c = r == 0 ? 0 : rows.Max(x => x.Count);
            var cv = new CellValue(r, c);
            for (int i = 0; i < r; i++) for (int j = 0; j < c; j++)
            {
                var s = j < rows[i].Count ? rows[i][j] : "";
                cv.Data[i + j * r] = s.Trim() == "" ? new MatrixValue(0, 0) : TryNum(s, out var v) ? Num(v) : Str(s);
            }
            return One(cv);
        });
        Reg("readvars", (it, a, n) =>
        {
            var t = (StructValue)B["readtable"](it, a, 1)[0];
            return t.Fields.Take(Math.Max(1, n)).Select(f => t.Elems[0][f]).ToArray();
        });
        Reg0("writematrix", (it, a) =>
        {
            var (pos, o) = SplitOpts(a, "Delimiter", "WriteMode", "FileType");
            var path = pos.Count > 1 ? S(pos[1], "writematrix") : "matrix.txt";
            char d = DelimOpt(o) ?? ',';
            var sb = new StringBuilder();
            if (pos[0] is CellValue cc) { WriteCellText(sb, cc, d); }
            else
            {
                var m = (MatrixValue)pos[0];
                for (int i = 0; i < m.R; i++)
                    sb.Append(string.Join(d, Enumerable.Range(0, m.C).Select(j => m.IsChar ? ((char)m.Re[i + j * m.R]).ToString() : m.IsLogical ? (m.Re[i + j * m.R] != 0 ? "1" : "0") : m.Is64 ? m.ExactString(i + j * m.R) : NumStr(m.Re[i + j * m.R])))).Append('\n');
            }
            bool app = o.TryGetValue("WriteMode", out var wm) && S(wm, "") == "append";
            if (app) File.AppendAllText(path, sb.ToString()); else File.WriteAllText(path, sb.ToString());
        });
        Reg0("writecell", (it, a) =>
        {
            var (pos, o) = SplitOpts(a, "Delimiter", "WriteMode", "FileType");
            var sb = new StringBuilder(); WriteCellText(sb, (CellValue)pos[0], DelimOpt(o) ?? ',');
            File.WriteAllText(pos.Count > 1 ? S(pos[1], "writecell") : "cell.txt", sb.ToString());
        });
        Reg("readlines", (it, a, n) =>
        {
            var txt = FileText(it, a[0], "readlines").Replace("\r\n", "\n");
            var ls = txt.Split('\n').ToList();
            if (ls.Count > 0 && ls[^1] == "" ) ls.RemoveAt(ls.Count - 1);
            var (_, o) = SplitOpts(a.Skip(1).ToArray(), "EmptyLineRule", "WhitespaceRule", "LineEnding", "Encoding");
            if (o.TryGetValue("EmptyLineRule", out var er) && S(er, "") is "skip") ls = ls.Where(l => l.Trim() != "").ToList();
            if (o.TryGetValue("WhitespaceRule", out var wr)) { var w = S(wr, ""); ls = ls.Select(l => w == "trim" ? l.Trim() : w == "trimleading" ? l.TrimStart() : w == "trimtrailing" ? l.TrimEnd() : l).ToList(); }
            return One(TCol(ls));
        });
        Reg0("writelines", (it, a) =>
        {
            var (pos, o) = SplitOpts(a, "WriteMode", "LineEnding", "Encoding");
            var lines = CellStrs(pos[0]);
            var le = o.TryGetValue("LineEnding", out var lev) ? S(lev, "").Replace("\\n", "\n").Replace("\\r", "\r") : "\n";
            var txt = string.Concat(lines.Select(l => l + le));
            var p = S(pos[1], "writelines");
            if (o.TryGetValue("WriteMode", out var wm) && S(wm, "") == "append") File.AppendAllText(p, txt); else File.WriteAllText(p, txt);
        });
        // ---------------------------------------------------------- file system
        Reg("dir", (it, a, n) =>
        {
            string spec = a.Length > 0 ? S(a[0], "dir") : ".";
            string folder, pattern;
            bool recursive = spec.Contains("**");
            if (Directory.Exists(spec)) { folder = spec; pattern = "*"; }
            else { folder = Path.GetDirectoryName(spec.Replace("**" + Path.DirectorySeparatorChar, "").Replace("**/", "")) is string dd && dd != "" ? dd : "."; pattern = Path.GetFileName(spec); }
            var full = Path.GetFullPath(folder);
            var entries = new List<FileSystemInfo>();
            if (Directory.Exists(full))
            {
                var di = new DirectoryInfo(full);
                var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                if (pattern == "*" && !recursive) { entries.Add(di); entries.Add(di.Parent ?? di); }
                entries.AddRange(di.EnumerateFileSystemInfos(pattern, opt).OrderBy(e => e.Name, StringComparer.Ordinal));
            }
            else if (File.Exists(spec)) entries.Add(new FileInfo(spec));
            var names = entries.Select((e, i) => pattern == "*" && !recursive && i < 2 && Directory.Exists(full) ? (i == 0 ? "." : "..") : e.Name).ToList();
            if (n == 0)
            {
                if (names.Count == 0) { it.Out.WriteLine($"warning: dir: nonexistent directory '{spec}'"); return Array.Empty<Value>(); }
                it.Out.WriteLine(string.Join("  ", names)); it.Out.WriteLine(); return Array.Empty<Value>();
            }
            var fl = new List<string> { "name", "folder", "date", "bytes", "isdir", "datenum", "statinfo" };
            var s = new StructValue(entries.Count, 1, fl);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i]; var el = s.Elems[i];
                el["name"] = Str(names[i]);
                el["folder"] = Str(e is FileInfo fi ? fi.DirectoryName ?? full : (i < 2 && pattern == "*" && !recursive ? full : ((DirectoryInfo)e).Parent?.FullName ?? full));
                el["date"] = Str(e.LastWriteTime.ToString("dd-MMM-yyyy HH:mm:ss", InvC));
                el["bytes"] = Num(e is FileInfo f2 ? f2.Length : 0);
                el["isdir"] = Bool(e is DirectoryInfo);
                el["datenum"] = Num(ToDatenumD(e.LastWriteTime));
                el["statinfo"] = new MatrixValue(0, 0);
            }
            return One(s);
        });
        Reg("movefile", (it, a, n) =>
        {
            try
            {
                string src = S(a[0], "movefile"), dst = S(a[1], "movefile");
                foreach (var f in Glob(src))
                {
                    var target = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(f)) : dst;
                    if (Directory.Exists(f)) Directory.Move(f, target); else File.Move(f, target, true);
                }
                return n > 0 ? new[] { Bool(true), Str(""), Str("") } : Array.Empty<Value>();
            }
            catch (Exception e) when (e is not OctaveException) { if (n > 0) return new[] { Bool(false), Str(e.Message), Str("movefile") }; throw new OctaveException("movefile: " + e.Message); }
        });
        Reg("copyfile", (it, a, n) =>
        {
            try
            {
                string src = S(a[0], "copyfile"), dst = S(a[1], "copyfile");
                foreach (var f in Glob(src))
                {
                    var target = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(f)) : dst;
                    if (Directory.Exists(f)) CopyDir(f, target); else File.Copy(f, target, true);
                }
                return n > 0 ? new[] { Bool(true), Str(""), Str("") } : Array.Empty<Value>();
            }
            catch (Exception e) when (e is not OctaveException) { if (n > 0) return new[] { Bool(false), Str(e.Message), Str("copyfile") }; throw new OctaveException("copyfile: " + e.Message); }
        });
        Reg1("isfolder", a => Bool(Directory.Exists(S(a[0], "isfolder"))));
        Reg1("isfile", a => Bool(File.Exists(S(a[0], "isfile"))));
        Reg1("tempname", a => Str(Path.Combine(a.Length > 0 ? S(a[0], "tempname") : Path.GetTempPath(), "tp" + Guid.NewGuid().ToString("N")[..12])));
        // ---------------------------------------------------------- JSON
        Reg1("jsonencode", a =>
        {
            bool pretty = a.Length >= 3 && S(a[1], "jsonencode").Equals("PrettyPrint", StringComparison.OrdinalIgnoreCase) && Ops.IsTrue(a[2]);
            var sb = new StringBuilder(); JsonEnc(sb, a[0]);
            var s = sb.ToString();
            if (pretty) s = JsonSerializer.Serialize(JsonDocument.Parse(s).RootElement, new JsonSerializerOptions { WriteIndented = true });
            return Str(s);
        });
        Reg1("jsondecode", a =>
        {
            using var doc = JsonDocument.Parse(S(a[0], "jsondecode"));
            return JsonDec(doc.RootElement);
        });
    }
    static IEnumerable<string> Glob(string src)
    {
        if (!src.Contains('*') && !src.Contains('?')) { if (!File.Exists(src) && !Directory.Exists(src)) throw new OctaveException($"no matching files for '{src}'"); return new[] { src }; }
        var d = Path.GetDirectoryName(src); if (string.IsNullOrEmpty(d)) d = ".";
        var r = Directory.GetFileSystemEntries(d, Path.GetFileName(src));
        if (r.Length == 0) throw new OctaveException($"no matching files for '{src}'");
        return r;
    }
    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true);
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }
    static void WriteCellText(StringBuilder sb, CellValue c, char d)
    {
        for (int i = 0; i < c.R; i++)
            sb.Append(string.Join(d, Enumerable.Range(0, c.C).Select(j => c.Data[i + j * c.R] is MatrixValue m ? (m.IsChar ? CsvField(m.AsString(), d) : m.Numel == 1 ? NumStr(m.Re[0]) : "") : ""))).Append('\n');
    }
    static void JsonEnc(StringBuilder sb, Value v)
    {
        switch (v)
        {
            case MatrixValue m when m.IsChar: sb.Append(JsonSerializer.Serialize(m.AsString())); return;
            case MatrixValue m when m.Numel == 1: sb.Append(m.IsLogical ? (m.Re[0] != 0 ? "true" : "false") : double.IsFinite(m.Re[0]) ? NumStr(m.Re[0]) : "null"); return;
            case MatrixValue m:
                {
                    string E(double x) => m.IsLogical ? (x != 0 ? "true" : "false") : double.IsFinite(x) ? NumStr(x) : "null";
                    if (m.R == 1 || m.C == 1) { sb.Append('[').Append(string.Join(",", m.Re.Take(m.Numel).Select(E))).Append(']'); return; }
                    sb.Append('[').Append(string.Join(",", Enumerable.Range(0, m.R).Select(i => "[" + string.Join(",", Enumerable.Range(0, m.C).Select(j => E(m.Re[i + j * m.R]))) + "]"))).Append(']');
                    return;
                }
            case CellValue c:
                sb.Append('['); for (int i = 0; i < c.Numel; i++) { if (i > 0) sb.Append(','); JsonEnc(sb, c.Data[i]); } sb.Append(']'); return;
            case StructValue s when s.Tag != null:
                if (IsTable(s)) { JsonEnc(sb, Tagged.Paren(s, Array.Empty<Value>()) is var _ ? TableToStructArr(s) : s); return; }
                if (s.Tag == "categorical" || s.Tag == "datetime") { var l = Enumerable.Range(0, Inner(s).Numel).Select(k => ElemString(s, k)).ToList(); if (l.Count == 1) sb.Append(JsonSerializer.Serialize(l[0])); else sb.Append('[').Append(string.Join(",", l.Select(x => JsonSerializer.Serialize(x)))).Append(']'); return; }
                JsonEnc(sb, Inner(s)); return;
            case StructValue s:
                {
                    void One1(Dictionary<string, Value> e)
                    { sb.Append('{'); bool first = true; foreach (var f in s.Fields) { if (!first) sb.Append(','); first = false; sb.Append(JsonSerializer.Serialize(f)).Append(':'); JsonEnc(sb, e[f]); } sb.Append('}'); }
                    if (s.Numel == 1) One1(s.Elems[0]);
                    else { sb.Append('['); for (int i = 0; i < s.Numel; i++) { if (i > 0) sb.Append(','); One1(s.Elems[i]); } sb.Append(']'); }
                    return;
                }
            default: sb.Append("null"); return;
        }
    }
    static StructValue TableToStructArr(StructValue t)
    {
        int h = Height(t); var s = new StructValue(h, 1, t.Fields.ToList());
        for (int i = 0; i < h; i++) foreach (var f in t.Fields) { var c = t.Elems[0][f]; s.Elems[i][f] = c is CellValue cc ? cc.Data[i] : TakeRows(c, new[] { i }); }
        return s;
    }
    static Value JsonDec(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number: return Num(e.GetDouble());
            case JsonValueKind.String: return Str(e.GetString()!);
            case JsonValueKind.True: return Bool(true);
            case JsonValueKind.False: return Bool(false);
            case JsonValueKind.Null: return new MatrixValue(0, 0);
            case JsonValueKind.Object:
                {
                    var s = new StructValue();
                    foreach (var p in e.EnumerateObject()) s.Set(ValidName(p.Name, 0), JsonDec(p.Value));
                    return s;
                }
            case JsonValueKind.Array:
                {
                    var items = e.EnumerateArray().ToList();
                    if (items.Count == 0) return new MatrixValue(0, 0);
                    if (items.All(x => x.ValueKind is JsonValueKind.Number or JsonValueKind.Null))
                        return MatrixValue.Col(items.Select(x => x.ValueKind == JsonValueKind.Null ? double.NaN : x.GetDouble()));
                    if (items.All(x => x.ValueKind is JsonValueKind.True or JsonValueKind.False))
                    { var m = new MatrixValue(items.Count, 1, NumKind.Logical); for (int i = 0; i < items.Count; i++) m.Re[i] = items[i].GetBoolean() ? 1 : 0; return m; }
                    var dec = items.Select(JsonDec).ToList();
                    if (dec.All(x => x is MatrixValue m && !m.IsChar && m.R > 1 && m.C == 1) && dec.Select(x => x.Numel).Distinct().Count() == 1)
                        return Ops.Transpose((ArrayValue)MatRewrite.Core.Runtime.Concat.Horz(dec), false);
                    if (dec.All(x => x is StructValue s && s.Tag == null && s.Numel == 1) && dec.Select(x => string.Join(",", ((StructValue)x).Fields)).Distinct().Count() == 1)
                    {
                        var f0 = ((StructValue)dec[0]).Fields.ToList(); var s = new StructValue(dec.Count, 1, f0);
                        for (int i = 0; i < dec.Count; i++) s.Elems[i] = new Dictionary<string, Value>(((StructValue)dec[i]).Elems[0]);
                        return s;
                    }
                    return CellValue.ColOf(dec);
                }
        }
        return new MatrixValue(0, 0);
    }
}
public static partial class BuiltinLibrary
{
    static void RegisterPhase13AppData()
    {
        StructValue AD(Interpreter it, Value h, string fn)
        {
            var o = it.Graphics.Lookup(D(h, fn)) ?? throw new OctaveException($"{fn}: invalid handle");
            if (!o.Props.TryGetValue("__appdata__", out var v) || v is not StructValue s) { s = new StructValue(); o.Props["__appdata__"] = s; }
            return s;
        }
        Reg0("setappdata", (it, a) => { var s = AD(it, a[0], "setappdata"); for (int i = 1; i + 1 < a.Length; i += 2) s.Set(S(a[i], "setappdata"), a[i + 1]); });
        Reg1("getappdata", a => throw new OctaveException("getappdata: internal"));
        Reg("getappdata", (it, a, n) => { var s = AD(it, a[0], "getappdata"); if (a.Length == 1) return One(s); var k = S(a[1], "getappdata"); return One(s.Fields.Contains(k) ? s.Elems[0][k] : new MatrixValue(0, 0)); });
        Reg("isappdata", (it, a, n) => One(Bool(AD(it, a[0], "isappdata").Fields.Contains(S(a[1], "isappdata")))));
        Reg0("rmappdata", (it, a) => { var s = AD(it, a[0], "rmappdata"); foreach (var k in a.Skip(1).Select(x => S(x, "rmappdata"))) { s.Fields.Remove(k); s.Elems[0].Remove(k); } });
        }

    /// <summary>Help text for a function: leading comment block of its .m source, or a built-in note.</summary>
    /// <summary>Raw help comment block of an m-file (Octave get_help_text), or null if not found.</summary>
    public static string? RawHelp(Interpreter it, string name)
    {
        string? src = null;
        var f = it.FindFile(name);
        if (f != null && File.Exists(f)) src = File.ReadAllText(f);
        src ??= CoreMLib.Get(name);
        if (src == null) return null;
        var sb = new System.Text.StringBuilder();
        foreach (var raw in src.Replace("\r", "").Split('\n'))
        {
            var t = raw.Trim();
            if (t.StartsWith("%") || t.StartsWith("#"))
            {
                var c = t.TrimStart('%', '#');
                if (sb.Length == 0 && c.Trim().Length == 0) continue;
                sb.Append(c.StartsWith(" ") ? c[1..] : c).Append('\n');
            }
            else if (sb.Length > 0)
            {
                if (sb.ToString().Contains("Copyright")) { sb.Clear(); continue; }
                break;
            }
        }
        if (sb.ToString().Contains("Copyright")) sb.Clear();
        return sb.ToString();
    }

    public static string HelpText(Interpreter it, string name)
    {
        if (name.Length > 0 && name.All(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
            return "Operators:\n\n  !   ~   !=  ~=  \"   #   %   &   &&  '   (   )   *   **  +   ++  ,   -   --\n"
                 + "  .'  .*  .^  ./  .\\  /   :   ;   <   <=  ==  =   >   >=  [   ]   \\   ^   |   ||\n";
        string? src = null;
        var f = it.FindFile(name);
        if (f != null && File.Exists(f)) src = File.ReadAllText(f);
        src ??= CoreMLib.Get(name);
        if (src != null)
        {
            var lines = src.Replace("\r", "").Split('\n');
            var sb = new System.Text.StringBuilder();
            bool started = false;
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (t.StartsWith("%") || t.StartsWith("#"))
                {
                    var c = t.TrimStart('%', '#');
                    if (!started && (c.Contains("Copyright") || c.TrimStart().StartsWith("-*-"))) { continue; }
                    started = true; sb.AppendLine(c.StartsWith(" ") ? c[1..] : c);
                }
                else if (started && t.Length > 0) break;
                else if (started) sb.AppendLine();
            }
            var txt = sb.ToString().Trim();
            if (txt.Length > 0) return $"'{name}' is a function from the file {(f ?? "<core>/" + name + ".m")}\n\n{txt}\n";
            return $"'{name}' is a function from the file {(f ?? "<core>/" + name + ".m")} (no help text)\n";
        }
        if (it.Builtins.ContainsKey(name)) return $"'{name}' is a built-in function.\n";
        if (it.IsVariable(name)) return $"'{name}' is a variable.\n";
        if (Lexing.Lexer.IsKeyword(name)) return $"'{name}' is a language keyword.\n";
        throw new OctaveException($"help: '{name}' not found");
    }

    static void RegisterPhase13Help()
    {
        BuiltinFn h = (it, a, n) =>
        {
            if (a.Length == 0) { var t = "MatOct help. Usage: help NAME, doc NAME, lookfor WORD.\n"; if (n > 0) return One(Str(t)); it.Out.Write(t); return Array.Empty<Value>(); }
            var txt = HelpText(it, S(a[0], "help"));
            if (n > 0) return One(Str(txt));
            it.Out.Write(txt); return Array.Empty<Value>();
        };
        Reg("help", h); Reg("doc", h);
        Reg("unicode_idx", (it, a, n) =>
        {
            if (a.Length != 1 || a[0] is not MatrixValue m || !m.IsChar) throw new OctaveException("unicode_idx: STR must be a string or cellstring");
            var r = new double[m.Numel]; int idx = 0;
            for (int i = 0; i < m.Numel; i++)
            {
                var ch = (char)m.Re[i];
                if (!(char.IsLowSurrogate(ch) && i > 0 && char.IsHighSurrogate((char)m.Re[i - 1]))) idx++;
                r[i] = idx;
            }
            return new Value[] { new MatrixValue(m.Rows, m.Cols, r) };
        });
        Reg("get_help_text", (it, a, n) =>
        {
            var nm = S(a[0], "get_help_text");
            var txt = RawHelp(it, nm);
            string fmt;
            if (txt == null) { txt = ""; fmt = it.Builtins.ContainsKey(nm) ? "Not documented" : "Not found"; }
            else if (txt.Trim().Length == 0) fmt = "Not documented";
            else if (txt.Contains("-*- texinfo -*-")) fmt = "texinfo";
            else fmt = "plain text";
            return new Value[] { Str(txt), Str(fmt) };
        });
        Reg0("lookfor", (it, a) =>
        {
            var w = S(a[0], "lookfor");
            foreach (var nm in CoreMLib.Names.OrderBy(x => x))
            {
                var src = CoreMLib.Get(nm)!;
                var first = src.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("%"));
                if (first != null && (nm.Contains(w, StringComparison.OrdinalIgnoreCase) || first.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    it.Out.Write($"{nm,-20} {first.TrimStart('%', ' ')}\n");
            }
        });
    }
}
