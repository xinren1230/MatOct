// MatRewrite - GPL-3.0-or-later
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static bool IsCharV(Value v) => v is MatrixValue m && m.IsChar;
    static List<string> CharRows(MatrixValue m)
    {
        var l = new List<string>();
        if (m.Numel == 0 && m.R <= 1) { l.Add(""); return l; }
        for (int i = 0; i < m.R; i++) { var sb = new StringBuilder(); for (int j = 0; j < m.C; j++) sb.Append((char)m.Re[j * m.R + i]); l.Add(sb.ToString()); }
        return l;
    }
    static (List<string> strs, int[] dims) CellStrOf(Value v)
    {
        if (v is CellValue c) return (c.Data.Select(x => ((MatrixValue)x).AsString()).ToList(), c.Dims.ToArray());
        var m = (MatrixValue)v;
        var rows = m.R == 0 ? new List<string> { "" } : CharRows(m).Select(s => s.TrimEnd(' ', '\0')).ToList();
        return (rows, new[] { rows.Count, 1 });
    }
    static string StrRepNoOverlap(string s, string p, string r)
    {
        if (p.Length == 0) return s;
        var sb = new StringBuilder(); int i = 0;
        while (i < s.Length) { if (i + p.Length <= s.Length && string.CompareOrdinal(s, i, p, 0, p.Length) == 0) { sb.Append(r); i += p.Length; } else sb.Append(s[i++]); }
        return sb.ToString();
    }

    static void RegisterPhase7Strings()
    {
        foreach (var nm in new[] { "startsWith", "endsWith" })
        {
            bool start = nm == "startsWith";
            Reg(nm, (it, a, n) =>
            {
                if (a.Length != 2 && a.Length != 4) Usage(nm);
                if (!(IsCellStr(a[0]) || IsCharV(a[0]))) throw new OctaveException($"{nm}: STR must be a string or cell array of strings");
                if (!(IsCellStr(a[1]) || IsCharV(a[1]))) throw new OctaveException($"{nm}: PATTERN must be a string or cell array of strings");
                var (strs, dims) = CellStrOf(a[0]); var (pats, _) = CellStrOf(a[1]);
                if (IsCharV(a[0])) { strs = CharRows((MatrixValue)a[0]); dims = new[] { strs.Count, 1 }; }
                if (IsCharV(a[1])) pats = CharRows((MatrixValue)a[1]);
                bool ic = false;
                if (a.Length == 4)
                {
                    var o = IsCharV(a[2]) ? Sx(a[2]) : "";
                    if (o.Length == 0 || !"ignorecase".StartsWith(o.ToLowerInvariant())) throw new OctaveException($"{nm}: third input must be \"IgnoreCase\"");
                    if (!(a[3] is MatrixValue) || !IsScal(a[3]) || IsCplxV(a[3])) throw new OctaveException($"{nm}: \"IgnoreCase\" value must be a logical scalar");
                    ic = Ops.IsTrue(a[3]);
                }
                var cmp = ic ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var r = strs.Select(s => pats.Any(p => p.Length > 0 && s.Length >= p.Length && (start ? s.StartsWith(p, cmp) : s.EndsWith(p, cmp))) ? 1.0 : 0.0).ToArray();
                var res = new MatrixValue(1, 1, r, null, NumKind.Logical); res.SetDims(dims);
                return new Value[] { res };
            });
        }
        Reg("erase", (it, a, n) =>
        {
            if (a.Length != 2) Usage("erase");
            var str = a[0]; var ptn = a[1]; bool charMat = false;
            List<string> ss; int[] dims;
            if (IsCharV(str) || str is MatrixValue sm0 && sm0.Numel == 0 && sm0.Kind == NumKind.Char)
            {
                var m = (MatrixValue)str;
                if (m.R > 1) { ss = CharRows(m); dims = new[] { ss.Count, 1 }; charMat = true; }
                else { ss = new List<string> { m.AsString() }; dims = new[] { 1, 1 }; }
            }
            else if (IsCellStr(str)) { var c = (CellValue)str; ss = c.Data.Select(x => ((MatrixValue)x).AsString()).ToList(); dims = c.Dims.ToArray(); }
            else throw new OctaveException("erase: STR must be a string or cell array of strings");
            List<string> ps;
            if (IsCharV(ptn) || ptn is MatrixValue pm0 && pm0.Numel == 0)
            {
                var m = (MatrixValue)ptn;
                if (m.R > 1) { it.Warning("erase: using character array for PTN is not recommended, consider cell array of strings instead", "Octave:erase:chararray"); ps = CharRows(m); }
                else ps = new List<string> { m.AsString() };
            }
            else if (IsCellStr(ptn)) ps = ((CellValue)ptn).Data.Select(x => ((MatrixValue)x).AsString()).ToList();
            else throw new OctaveException("erase: PTN must be a string or cell array of strings");
            List<string> outs;
            if (ps.Count == 1) outs = ss.Select(s => StrRepNoOverlap(s, ps[0], "")).ToList();
            else
            {
                var alts = ps.Where(p => p.Length > 0).Select(System.Text.RegularExpressions.Regex.Escape).ToList();
                outs = alts.Count == 0 ? ss : ss.Select(s => System.Text.RegularExpressions.Regex.Replace(s, string.Join("|", alts), "")).ToList();
            }
            if (charMat) return new[] { Fc(it, "char", new CellValue(outs.Count, 1, outs.Select(o => (Value)MatrixValue.Str(o)).ToArray())) };
            if (str is CellValue) { var c = new CellValue(1, 1, outs.Select(o => (Value)MatrixValue.Str(o)).ToArray()); c.R = dims[0]; c.C = outs.Count / Math.Max(1, dims[0]); if (dims.Length > 2) c.SetDims(dims); return new Value[] { c }; }
            return new Value[] { MatrixValue.Str(outs[0]) };
        });
        Reg("strjust", (it, a, n) =>
        {
            if (a.Length > 2) TooMany("strjust"); if (a.Length < 1) Usage("strjust");
            if (!(a[0] is MatrixValue m && (m.IsChar || m.Numel == 0)) || SizeOf(a[0]).Length > 2) throw new OctaveException("strjust: S must be a string or 2-D character matrix");
            var s = (MatrixValue)a[0];
            if (s.Numel == 0) return new[] { a[0] };
            string pos = a.Length > 1 ? Sx(a[1]).ToLowerInvariant() : "right";
            var rows = CharRows(s); int nc = s.C;
            var outs = rows.Select(r =>
            {
                bool Nb(char ch) => ch != ' ' && ch != '\0';
                int first = -1, last = -1;
                for (int j = 0; j < nc; j++) if (Nb(r[j])) { if (first < 0) first = j; last = j; }
                if (first < 0) return new string(' ', nc).ToCharArray();
                int shift = pos == "right" ? nc - 1 - last : pos == "left" ? -first : (int)Math.Floor((nc + 1 - (last + 1) - (first + 1)) / 2.0);
                var o = new string(' ', nc).ToCharArray();
                for (int j = 0; j < nc; j++) if (Nb(r[j])) o[j + shift] = r[j];
                return o;
            }).ToList();
            var d = new double[s.R * nc];
            for (int i = 0; i < s.R; i++) for (int j = 0; j < nc; j++) d[j * s.R + i] = outs[i][j];
            return new Value[] { new MatrixValue(s.R, nc, d, null, NumKind.Char) };
        });
        Reg("regexptranslate", (it, a, n) =>
        {
            if (a.Length > 2) TooMany("regexptranslate"); if (a.Length != 2) Usage("regexptranslate");
            if (!IsCharV(a[0]) && !(a[0] is MatrixValue z && z.Numel == 0)) throw new OctaveException("regexptranslate: operation OP must be a string");
            var op = Sx(a[0]).ToLowerInvariant();
            if (op == "wildcard")
                return new[] { Fc(it, "strrep", Fc(it, "strrep", Fc(it, "strrep", a[1], MatrixValue.Str("."), MatrixValue.Str("\\.")), MatrixValue.Str("*"), MatrixValue.Str(".*")), MatrixValue.Str("?"), MatrixValue.Str(".")) };
            if (op == "escape")
            {
                string Esc(string s) { var sb = new StringBuilder(); foreach (var ch in s) { if ("[](){}.*+?^$|\\".IndexOf(ch) >= 0) sb.Append('\\'); sb.Append(ch); } return sb.ToString(); }
                if (a[1] is CellValue c) return new Value[] { new CellValue(c.R, c.C, c.Data.Select(x => (Value)MatrixValue.Str(Esc(((MatrixValue)x).AsString()))).ToArray()) };
                return new Value[] { MatrixValue.Str(Esc(Sx(a[1]))) };
            }
            throw new OctaveException("regexptranslate: invalid operation OP");
        });
        Reg("validatestring", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 5) Usage("validatestring");
            var rest = a.Skip(2).ToList(); int position = 0;
            if (rest.Count > 0 && IsNum(rest[^1]))
            {
                position = (int)Math.Truncate(D(rest[^1], "validatestring")); rest.RemoveAt(rest.Count - 1);
                if (position < 1) throw new OctaveException("validatestring: POSITION must be >= 1");
            }
            var chars = rest.Where(IsCharVOrEmptyChar).ToList();
            string funcname = "", varname = "";
            if (chars.Count > 2) throw new OctaveException("validatestring: invalid number of character inputs (> 3)");
            if (chars.Count >= 1) funcname = ((MatrixValue)chars[0]).AsString();
            if (chars.Count == 2) varname = ((MatrixValue)chars[1]).AsString();
            if (!(IsCharV(a[0]) && ((MatrixValue)a[0]).R == 1)) throw new OctaveException("validatestring: STR must be a single character string");
            if (!IsCellStr(a[1])) throw new OctaveException("validatestring: STRARRAY must be a cellstr");
            if (chars.Count >= 1 && ((MatrixValue)chars[0]).Numel > 0 && ((MatrixValue)chars[0]).R != 1) throw new OctaveException("validatestring: FUNCNAME must be a single character string");
            if (chars.Count == 2 && ((MatrixValue)chars[1]).Numel > 0 && ((MatrixValue)chars[1]).R != 1) throw new OctaveException("validatestring: VARNAME must be a single character string");
            if (funcname == "") funcname = "validatestring";
            var str = Sx(a[0]);
            var errstr = $"{funcname}: '{str}' ";
            if (varname != "") errstr += position > 0 ? $"(variable {varname}, argument #{position}) " : $"(variable {varname}) ";
            else if (position > 0) errstr += $"(argument #{position}) ";
            var arr = ((CellValue)a[1]).Data.Select(x => ((MatrixValue)x).AsString()).ToList();
            bool Pre(string s, string p, int len) => s.Length >= len && p.Length >= len && string.Compare(s, 0, p, 0, len, StringComparison.OrdinalIgnoreCase) == 0;
            var matches = Enumerable.Range(0, arr.Count).Where(k => Pre(arr[k], str, str.Length)).ToList();
            if (matches.Count == 0) throw new OctaveException($"{errstr}does not match any of\n{string.Join(", ", arr.Select(s => $"'{s}'"))}");
            if (matches.Count == 1) return new Value[] { MatrixValue.Str(arr[matches[0]]) };
            int minIdx = matches.OrderBy(k => arr[k].Length).First(); var shortS = arr[minIdx];
            if (matches.All(k => Pre(arr[k], shortS, shortS.Length))) return new Value[] { MatrixValue.Str(shortS) };
            throw new OctaveException($"{errstr}matches multiple possible values:\n{string.Join(", ", matches.Select(k => $"'{arr[k]}'"))}");
        });
        Reg("untabify", (it, a, n) =>
        {
            if (a.Length < 1) Usage("untabify");
            if (!(IsCharV(a[0]) || IsCellStr(a[0]) || a[0] is MatrixValue e0 && e0.Numel == 0 && e0.Kind == NumKind.Char)) throw new OctaveException("untabify: T must be a string or cellstring");
            int tw = a.Length > 1 ? I(a[1], "untabify") : 8; bool db = a.Length > 2 && Ops.IsTrue(a[2]);
            Value Rep(Value t)
            {
                var m = (MatrixValue)t;
                if (SizeOf(t).Length != 2) throw new OctaveException("untabify: character strings to untabify must have 2 dimensions");
                if (m.Numel == 0) return t;
                var rows = CharRows(m).Select(r =>
                {
                    var sb = new StringBuilder();
                    foreach (var ch in r) { if (ch == '\t') { int k = tw * (int)Math.Ceiling((sb.Length + 1) / (double)tw); sb.Append(' ', Math.Max(1, k - sb.Length)); } else sb.Append(ch); }
                    return (Value)MatrixValue.Str(sb.ToString());
                }).ToArray();
                return Fc(it, "char", new CellValue(rows.Length, 1, rows));
            }
            Value res = a[0] is CellValue c ? new CellValue(c.R, c.C, c.Data.Select(Rep).ToArray()) : Rep(a[0]);
            if (db) res = Fc(it, "deblank", res);
            return new[] { res };
        });
        Reg("compare_versions", (it, a, n) =>
        {
            if (a.Length != 3) Usage("compare_versions");
            if (!(IsCharV(a[0]) && IsCharV(a[1]))) throw new OctaveException("compare_versions: version numbers V1 and V2 must be strings");
            if (((MatrixValue)a[0]).R != 1 || ((MatrixValue)a[1]).R != 1) throw new OctaveException("compare_versions: version numbers V1 and V2 must be a single row");
            if (!IsCharV(a[2])) throw new OctaveException("compare_versions: OPERATOR must be a string");
            var op = Sx(a[2]);
            if (op.Length > 2) throw new OctaveException("compare_versions: OPERATOR must be 1 or 2 characters long");
            (List<double> num, string ch) Split(string v)
            {
                int fc = v.Select((c, i) => (c, i)).Where(t => !(char.IsDigit(t.c) || t.c == '.')).Select(t => t.i).DefaultIfEmpty(-1).First();
                string nochar = fc < 0 ? v : v.Substring(0, fc), c = fc < 0 ? "" : v.Substring(fc);
                var parts = nochar.Split('.').Select(p => double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : double.NaN).ToList();
                if (parts.All(double.IsNaN)) parts = new List<double>();
                return (parts, c);
            }
            string v1 = Sx(a[0]), v2 = Sx(a[1]);
            var (n1, c1) = Split(v1); var (n2, c2) = Split(v2);
            if (n1.Count == 0 && c1 == "") throw new OctaveException($"compare_versions: version string V1 is not valid: {v1}");
            if (n2.Count == 0 && c2 == "") throw new OctaveException($"compare_versions: version string V2 is not valid: {v2}");
            int ml = Math.Max(n1.Count, n2.Count); while (n1.Count < ml) n1.Add(0); while (n2.Count < ml) n2.Add(0);
            int mc = Math.Max(c1.Length, c2.Length); c1 = c1.PadRight(mc, '\0'); c2 = c2.PadRight(mc, '\0');
            bool eq = op.Contains('='), not = op.Contains('!') || op.Contains('~'), lt = op.Contains('<'), gt = op.Contains('>');
            if (gt && lt) throw new OctaveException("compare_versions: OPERATOR cannot contain both greater and less than symbols");
            if ((gt || lt) && not) throw new OctaveException("compare_versions: OPERATOR cannot contain not and greater than or less than symbols");
            if (op == "=") throw new OctaveException("compare_versions: equality OPERATOR is \"==\", not \"=\"");
            if (!(eq || not || lt || gt)) throw new OctaveException("compare_versions: no valid OPERATOR specified");
            var v = n1.Zip(n2, (x, y) => x - y).Concat(c1.Zip(c2, (x, y) => (double)(x - y))).Select(x => lt ? -x : x).ToList();
            int fd = v.FindIndex(x => x != 0);
            bool tf = fd < 0 ? eq : (lt || gt) && v[fd] > 0;
            if (not) tf = !tf;
            return new Value[] { Bool(tf) };
        });
    }
    static bool IsCharVOrEmptyChar(Value v) => v is MatrixValue m && m.IsChar;
}
