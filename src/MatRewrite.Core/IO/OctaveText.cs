// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.IO;

/// <summary>Octave text save format ("# name: x / # type: matrix ...").</summary>
public static class OctaveText
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Num(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Inf";
        if (double.IsNegativeInfinity(d)) return "-Inf";
        if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(Inv);
        return d.ToString("R", Inv);
    }

    static string Cx(double re, double im) => $"({Num(re)},{Num(im)})";

    public static void WriteFile(TextWriter w, IEnumerable<(string name, Value v)> vars)
    {
        w.Write($"# Created by MatRewrite, {DateTime.Now:ddd MMM dd HH:mm:ss yyyy}\n");
        foreach (var (name, v) in vars)
        {
            w.Write($"# name: {name}\n");
            WriteValue(w, v);
            w.Write("\n\n");
        }
    }

    static void DimsLine(StringBuilder sb, int[] d)
    {
        sb.Append($"# ndims: {d.Length}\n");
        foreach (var x in d) sb.Append(' ').Append(x);
        sb.Append('\n');
    }

    public static void WriteValue(TextWriter w, Value v)
    {
        var sb = new StringBuilder();
        Write(sb, v);
        w.Write(sb.ToString());
    }

    static void Write(StringBuilder sb, Value v)
    {
        switch (v)
        {
            case MatrixValue m when m.IsChar:
            {
                if (m.NDims > 2) throw new OctaveException("save: N-D char arrays are not supported");
                sb.Append(m.DoubleQuoted ? "# type: string\n" : "# type: sq_string\n");
                sb.Append($"# elements: {m.R}\n");
                for (int i = 0; i < m.R; i++)
                {
                    var row = m.RowString(i);
                    sb.Append($"# length: {Encoding.UTF8.GetByteCount(row)}\n{row}\n");
                }
                if (m.R > 0) sb.Length--; // last newline comes from the caller
                sb.Append('\n');
                break;
            }
            case MatrixValue m when m.IsInteger:
            {
                var tn = m.ClassName;
                if (m.Numel == 1 && m.NDims == 2) { sb.Append($"# type: {tn} scalar\n{Num(m.Re[0])}\n"); break; }
                sb.Append($"# type: {tn} matrix\n");
                DimsLine(sb, m.Dims);
                foreach (var x in m.Re) sb.Append(' ').Append(Num(x)).Append('\n');
                break;
            }
            case MatrixValue m:
            {
                string pre = m.IsLogical ? "bool" : m.IsSingle ? "float" : "";
                bool cx = m.IsComplex;
                string kindS = m.IsLogical ? "bool" : (pre == "float" ? "float " : "") + (cx ? "complex " : "") + "scalar";
                string kindM = m.IsLogical ? "bool matrix" : (pre == "float" ? "float " : "") + (cx ? "complex " : "") + "matrix";
                if (m.Numel == 1 && m.NDims == 2)
                {
                    sb.Append($"# type: {kindS}\n{(cx ? Cx(m.Re[0], m.Im![0]) : Num(m.Re[0]))}\n");
                    break;
                }
                sb.Append($"# type: {kindM}\n");
                if (m.NDims > 2)
                {
                    DimsLine(sb, m.Dims);
                    for (int k = 0; k < m.Numel; k++) sb.Append(' ').Append(cx ? Cx(m.Re[k], m.Im![k]) : Num(m.Re[k])).Append('\n');
                    break;
                }
                sb.Append($"# rows: {m.R}\n# columns: {m.C}\n");
                for (int i = 0; i < m.R; i++)
                {
                    for (int j = 0; j < m.C; j++)
                    {
                        int k = i + j * m.R;
                        sb.Append(' ').Append(cx ? Cx(m.Re[k], m.Im![k]) : Num(m.Re[k]));
                    }
                    sb.Append('\n');
                }
                break;
            }
            case CellValue c:
            {
                if (c.NDims > 2) throw new OctaveException("save: N-D cell arrays are not supported");
                sb.Append($"# type: cell\n# rows: {c.R}\n# columns: {c.C}\n");
                for (int j = 0; j < c.C; j++)
                {
                    for (int i = 0; i < c.R; i++)
                    {
                        sb.Append("# name: <cell-element>\n");
                        Write(sb, c.Data[i + j * c.R]);
                        sb.Append("\n\n");
                    }
                    sb.Append('\n');
                }
                break;
            }
            case StructValue s when s.Numel == 1 && s.NDims == 2:
            {
                sb.Append("# type: scalar struct\n");
                DimsLine(sb, new[] { 1, 1 });
                sb.Append($"# length: {s.Fields.Count}\n");
                foreach (var f in s.Fields)
                {
                    sb.Append($"# name: {f}\n");
                    Write(sb, s.Elems[0][f]);
                    sb.Append("\n\n");
                }
                break;
            }
            case StructValue s:
            {
                if (s.NDims > 2) throw new OctaveException("save: N-D struct arrays are not supported");
                sb.Append("# type: struct\n");
                DimsLine(sb, s.Dims);
                sb.Append($"# length: {s.Fields.Count}\n");
                foreach (var f in s.Fields)
                {
                    sb.Append($"# name: {f}\n");
                    var cell = new CellValue(s.R, s.C, s.Elems.Select(e => e[f]).ToArray());
                    Write(sb, cell);
                    sb.Append("\n\n");
                }
                break;
            }
            default:
                throw new OctaveException($"save: wrong type argument '{v.ClassName}'");
        }
    }

    // ------------------------------------------------------------------ reading
    sealed class Reader
    {
        readonly string[] lines; int pos;
        public Reader(string text) { lines = text.Replace("\r\n", "\n").Split('\n'); }
        public bool Eof { get { SkipBlank(); return pos >= lines.Length; } }
        void SkipBlank() { while (pos < lines.Length && lines[pos].Trim() == "") pos++; }
        public string Peek() { SkipBlank(); return pos < lines.Length ? lines[pos] : ""; }
        public string Next() { SkipBlank(); if (pos >= lines.Length) throw new OctaveException("load: unexpected end of file"); return lines[pos++]; }
        public string NextRaw() { if (pos >= lines.Length) throw new OctaveException("load: unexpected end of file"); return lines[pos++]; }
        public string Header(string key)
        {
            var l = Next();
            var p = $"# {key}:";
            if (!l.StartsWith(p)) throw new OctaveException($"load: failed to extract keyword '{key}'");
            return l[p.Length..].Trim();
        }
        public bool IsHeader(string key) => Peek().StartsWith($"# {key}:");
        public bool IsComment => Peek().StartsWith("#") && !Peek().StartsWith("# name:");
    }

    public static List<(string name, Value v)> Read(string text)
    {
        var r = new Reader(text);
        var res = new List<(string, Value)>();
        while (!r.Eof)
        {
            if (!r.IsHeader("name")) { r.Next(); continue; }
            var name = r.Header("name");
            res.Add((name, ReadValue(r)));
        }
        return res;
    }

    static double P(string s)
    {
        s = s.Trim();
        return s switch
        {
            "Inf" or "inf" => double.PositiveInfinity,
            "-Inf" or "-inf" => double.NegativeInfinity,
            "NaN" or "nan" or "NA" or "-NaN" => double.NaN,
            _ => double.Parse(s, NumberStyles.Float, Inv)
        };
    }

    static (double re, double im) PC(string s)
    {
        s = s.Trim().TrimStart('(').TrimEnd(')');
        var parts = s.Split(',');
        return (P(parts[0]), P(parts[1]));
    }

    static string[] Tok(string line) => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    static int[] ReadDims(Reader r)
    {
        int nd = int.Parse(r.Header("ndims"));
        var d = Tok(r.Next()).Select(int.Parse).ToArray();
        if (d.Length != nd) throw new OctaveException("load: failed to read dimensions");
        return d;
    }

    static Value ReadValue(Reader r)
    {
        var type = r.Header("type");
        bool isFloat = type.StartsWith("float ");
        if (isFloat) type = type[6..];
        Value result;
        switch (type)
        {
            case "scalar": result = MatrixValue.Scalar(P(r.Next())); break;
            case "complex scalar": { var (a, b) = PC(r.Next()); result = MatrixValue.Cplx(a, b); break; }
            case "bool": result = MatrixValue.Bool(P(r.Next()) != 0); break;
            case "matrix":
            case "complex matrix":
            case "bool matrix":
            {
                bool cx = type.StartsWith("complex");
                var kind = type.StartsWith("bool") ? NumKind.Logical : NumKind.Double;
                if (r.IsHeader("ndims"))
                {
                    var d = ReadDims(r);
                    int n = NDArray.Prod(d);
                    var m = new MatrixValue(1, n, kind, cx);
                    for (int k = 0; k < n; k++)
                    {
                        var t = r.Next();
                        if (cx) { var (a, b) = PC(t); m.Re[k] = a; m.Im![k] = b; } else m.Re[k] = P(t);
                    }
                    m.SetDims(d);
                    result = m;
                }
                else
                {
                    int rows = int.Parse(r.Header("rows")), cols = int.Parse(r.Header("columns"));
                    var m = new MatrixValue(rows, cols, kind, cx);
                    for (int i = 0; i < rows; i++)
                    {
                        var t = Tok(r.Next());
                        for (int j = 0; j < cols; j++)
                        {
                            if (cx) { var (a, b) = PC(t[j]); m.Re[i + j * rows] = a; m.Im![i + j * rows] = b; }
                            else m.Re[i + j * rows] = P(t[j]);
                        }
                    }
                    result = cx ? m.Narrow() : m;
                }
                break;
            }
            case "string":
            case "sq_string":
            {
                int el = int.Parse(r.Header("elements"));
                var rows = new List<string>();
                for (int i = 0; i < el; i++)
                {
                    int len = int.Parse(r.Header("length"));
                    var s = r.NextRaw();
                    // multi-line strings: keep consuming lines until byte length reached
                    while (Encoding.UTF8.GetByteCount(s) < len) s += "\n" + r.NextRaw();
                    rows.Add(s);
                }
                if (el == 0) { result = MatrixValue.Str(""); ((MatrixValue)result).R = 0; break; }
                int w = rows.Max(x => x.Length);
                var m = new MatrixValue(el, w, NumKind.Char);
                for (int i = 0; i < el; i++)
                    for (int j = 0; j < w; j++) m.Re[i + j * el] = j < rows[i].Length ? rows[i][j] : ' ';
                m.DoubleQuoted = type == "string";
                result = m;
                break;
            }
            case "cell":
            {
                int rows = int.Parse(r.Header("rows")), cols = int.Parse(r.Header("columns"));
                var c = new CellValue(rows, cols);
                for (int k = 0; k < rows * cols; k++)
                {
                    r.Header("name");
                    c.Data[k] = ReadValue(r);
                }
                result = c;
                break;
            }
            case "scalar struct":
            {
                if (r.IsHeader("ndims")) ReadDims(r);
                int nf = int.Parse(r.Header("length"));
                var s = new StructValue();
                for (int f = 0; f < nf; f++)
                {
                    var fn = r.Header("name");
                    s.Set(fn, ReadValue(r));
                }
                result = s;
                break;
            }
            case "struct":
            {
                var d = ReadDims(r);
                int nf = int.Parse(r.Header("length"));
                var s = new StructValue(d[0], d[1], new List<string>());
                for (int f = 0; f < nf; f++)
                {
                    var fn = r.Header("name");
                    var cv = ReadValue(r) as CellValue ?? throw new OctaveException("load: invalid struct field");
                    s.AddField(fn);
                    for (int k = 0; k < s.Elems.Length; k++) s.Elems[k][fn] = cv.Data[k];
                }
                result = s;
                break;
            }
            default:
            {
                // integer types
                var parts = type.Split(' ');
                var kind = NumTypes.FromName(parts[0]);
                if (kind == null || parts.Length != 2) throw new OctaveException($"load: unknown type '{type}'");
                if (parts[1] == "scalar") result = MatrixValue.Scalar(P(r.Next())).CastInPlace(kind.Value);
                else
                {
                    var d = ReadDims(r);
                    int n = NDArray.Prod(d);
                    var m = new MatrixValue(1, n, kind.Value);
                    for (int k = 0; k < n; k++) m.Re[k] = P(r.Next());
                    m.SetDims(d);
                    result = m;
                }
                break;
            }
        }
        if (isFloat && result is MatrixValue fm) fm.CastInPlace(NumKind.Single);
        return result;
    }

    public static bool LooksLikeText(byte[] head)
    {
        var s = Encoding.ASCII.GetString(head, 0, Math.Min(head.Length, 200));
        return s.StartsWith("# ") || s.Contains("\n# name:");
    }
}
