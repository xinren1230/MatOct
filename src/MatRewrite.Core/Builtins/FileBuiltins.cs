// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MatRewrite.Core.IO;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{

    static void RegisterFiles()
    {
        // ---------------------------------------------------------- fopen family
        Reg("fopen", (it, a, n) =>
        {
            NArgs(a, 1, 4, "fopen");
            if (a[0] is MatrixValue fm && !fm.IsChar)
            {
                if (fm.Numel == 1 && it.Files.TryGetValue((int)fm.Re[0], out var of)) return Out(Str(of.Name), Str(of.Mode), Str("native"));
                return Out(Num(-1), Str(""), Str(""));
            }
            var name = S(a[0], "fopen");
            if (name == "all") return Out(MatrixValue.Row(it.Files.Keys.OrderBy(x => x).Select(x => (double)x)));
            var mode = a.Length > 1 ? S(a[1], "fopen") : "r";
            try { return Out(Num(it.OpenFile(name, mode)), Str("")); }
            catch (OctaveException) { throw; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return Out(Num(-1), Str(e is FileNotFoundException or DirectoryNotFoundException ? "No such file or directory" : e.Message));
            }
        });
        RegI("fclose", (it, a) =>
        {
            NArgs(a, 1, 1, "fclose");
            if (IsString(a[0]) && S(a[0], "fclose") == "all") { it.CloseAllFiles(); return Num(0); }
            var f = it.GetFile(a[0], "fclose");
            f.Stream.Dispose();
            it.Files.Remove((int)D(a[0], "fclose"));
            return Num(0);
        });
        RegI("fgetl", (it, a) => { var l = it.GetFile(a[0], "fgetl").ReadLine(false); return l == null ? Num(-1) : Str(l); });
        RegI("fgets", (it, a) => { var l = it.GetFile(a[0], "fgets").ReadLine(true); return l == null ? Num(-1) : Str(l); });
        RegI("fskipl", (it, a) =>
        {
            var f = it.GetFile(a[0], "fskipl"); int k = a.Length > 1 ? I(a[1], "fskipl") : 1, c = 0;
            while (c < k && f.ReadLine(true) != null) c++;
            return Num(c);
        });
        RegI("feof", (it, a) => Bool(it.GetFile(a[0], "feof").AtEof));
        RegI("ftell", (it, a) => Num(it.GetFile(a[0], "ftell").Stream.Position));
        RegI("fseek", (it, a) =>
        {
            var f = it.GetFile(a[0], "fseek");
            long off = (long)D(a[1], "fseek");
            var origin = SeekOrigin.Begin;
            if (a.Length > 2)
            {
                if (IsString(a[2])) origin = S(a[2], "fseek") switch { "bof" => SeekOrigin.Begin, "cof" => SeekOrigin.Current, "eof" => SeekOrigin.End, _ => throw new OctaveException("fseek: invalid value for origin") };
                else origin = D(a[2], "fseek") switch { -1 => SeekOrigin.Begin, 0 => SeekOrigin.Current, 1 => SeekOrigin.End, _ => throw new OctaveException("fseek: invalid value for origin") };
            }
            try { f.Stream.Seek(off, origin); return Num(0); } catch (IOException) { return Num(-1); }
        });
        Reg0("frewind", (it, a) => it.GetFile(a[0], "frewind").Stream.Seek(0, SeekOrigin.Begin));
        Reg0("fflush", (it, a) =>
        {
            if (a.Length > 0 && a[0] is MatrixValue m && m.Numel == 1 && m.Re[0] >= 3) it.GetFile(a[0], "fflush").Stream.Flush();
            else it.Out.Flush();
        });

        // ---------------------------------------------------------- text output
        Reg("fprintf", (it, a, n) =>
        {
            int start = 0; Action<string> w = s => it.Out.Write(s);
            if (a.Length > 0 && a[0] is MatrixValue f && !f.IsChar && f.Numel == 1)
            {
                start = 1;
                int fid = (int)f.Re[0];
                if (fid == 2) { it.Out.Flush(); w = s => Console.Error.Write(s); }
                else if (fid != 1) { var file = it.GetFile(f, "fprintf"); w = s => file.Write(s); }
            }
            if (a.Length <= start) return Array.Empty<Value>();
            var s = Printf.Format(Printf.Escapes(S(a[start], "fprintf")), a.Skip(start + 1).ToList());
            w(s);
            return n > 0 ? Out(Num(Encoding.UTF8.GetByteCount(s))) : Array.Empty<Value>();
        });
        Reg0("fputs", (it, a) => WriteTo(it, a[0], S(a[1], "fputs"), "fputs"));
        Reg0("fdisp", (it, a) => WriteTo(it, a[0], Display.Disp(a[1]), "fdisp"));

        // ---------------------------------------------------------- binary I/O
        Reg("fread", (it, a, n) =>
        {
            var f = it.GetFile(a[0], "fread");
            var (rows, cols, rowsInf) = SizeSpec(a.Length > 1 ? a[1] : Num(double.PositiveInfinity), "fread");
            var (inK, outK) = Precision(a.Length > 2 ? S(a[2], "fread") : "uint8", true);
            int sz = ByteSize(inK);
            long avail = (f.Stream.Length - f.Stream.Position) / sz;
            long want = cols < 0 ? (rowsInf ? avail : rows) : (long)rows * cols;
            if (cols < 0 && !rowsInf) want = Math.Min(rows, avail);
            int count = (int)Math.Min(want, avail);
            var buf = new byte[count * sz];
            f.Stream.ReadExactly(buf, 0, buf.Length);
            var vals = new double[count];
            for (int k = 0; k < count; k++) vals[k] = Decode(buf, k * sz, inK);
            MatrixValue m;
            if (cols < 0) m = rowsInf || a.Length <= 1 ? new MatrixValue(count, 1, vals) : new MatrixValue(count, count == 0 ? 0 : 1, vals);
            else
            {
                int c = cols == int.MaxValue ? (count + rows - 1) / Math.Max(rows, 1) : cols;
                var full = new double[rows * c];
                Array.Copy(vals, full, Math.Min(vals.Length, full.Length));
                m = new MatrixValue(rows, c, full);
            }
            if (outK != NumKind.Double) m.CastInPlace(outK);
            return Out(m, Num(count));
        });
        RegI("fwrite", (it, a) =>
        {
            var f = it.GetFile(a[0], "fwrite");
            var m = M(a[1], "fwrite");
            var (k, _) = Precision(a.Length > 2 ? S(a[2], "fwrite") : "uint8", false);
            int sz = ByteSize(k);
            var buf = new byte[m.Numel * sz];
            for (int i = 0; i < m.Numel; i++) Encode(buf, i * sz, k, m.Re[i]);
            if (f.Mode.StartsWith("a")) f.Stream.Seek(0, SeekOrigin.End);
            f.Stream.Write(buf);
            return Num(m.Numel);
        });

        // ---------------------------------------------------------- scanf
        Reg("fscanf", (it, a, n) =>
        {
            var f = it.GetFile(a[0], "fscanf");
            long startPos = f.Stream.Position;
            var text = f.ReadRest();
            var r = Scan(text, Printf.Escapes(S(a[1], "fscanf")), a.Length > 2 ? a[2] : null, out int used, out int cnt, out string err);
            f.Stream.Position = startPos + Encoding.UTF8.GetByteCount(text[..used]);
            return Out(r, Num(cnt), Str(err));
        });
        Reg("sscanf", (it, a, n) =>
        {
            var text = S(a[0], "sscanf");
            var r = Scan(text, Printf.Escapes(S(a[1], "sscanf")), a.Length > 2 ? a[2] : null, out int used, out int cnt, out string err);
            return Out(r, Num(cnt), Str(err), Num(used + 1));
        });
        Reg("textscan", (it, a, n) => Out(TextScan(it, a)));
        Reg("strread", (it, a, n) =>
        {
            var c = (CellValue)TextScan(it, a);
            return c.Data;
        });
        Reg("textread", (it, a, n) =>
        {
            var args = (Value[])a.Clone();
            args[0] = Str(File.ReadAllText(S(a[0], "textread")));
            return ((CellValue)TextScan(it, args)).Data;
        });

        // ---------------------------------------------------------- files & dirs
        Reg1("isfile", a => Bool(IsString(a[0]) && File.Exists(S(a[0], "isfile"))));
        Reg1("isfolder", a => Bool(IsString(a[0]) && Directory.Exists(S(a[0], "isfolder"))));
        Reg1("isdir", a => Bool(IsString(a[0]) && Directory.Exists(S(a[0], "isdir"))));
        Reg1("is_valid_file_id", a => Bool(a[0] is MatrixValue m && m.Numel == 1 && m.Re[0] >= 0));
        Reg1("fileread", a =>
        {
            var p = S(a[0], "fileread");
            if (!File.Exists(p)) throw new OctaveException($"fileread: cannot open file {p}");
            return Str(File.ReadAllText(p).Replace("\r\n", "\n"));
        });
        Reg0("delete", (it, a) =>
        {
            var p = S(a[0], "delete");
            var dir = Path.GetDirectoryName(p); if (string.IsNullOrEmpty(dir)) dir = ".";
            var pat = Path.GetFileName(p);
            var files = pat.Contains('*') || pat.Contains('?') ? Directory.GetFiles(dir, pat) : File.Exists(p) ? new[] { p } : Array.Empty<string>();
            if (files.Length == 0) it.Warning($"delete: no such file: {p}");
            foreach (var f in files) File.Delete(f);
        });
        Reg("mkdir", (it, a, n) =>
        {
            var p = a.Length > 1 ? Path.Combine(S(a[0], "mkdir"), S(a[1], "mkdir")) : S(a[0], "mkdir");
            Directory.CreateDirectory(p);
            return Out(Bool(true), Str(""), Str(""));
        });
        Reg("rmdir", (it, a, n) =>
        {
            var p = S(a[0], "rmdir");
            try { Directory.Delete(p, a.Length > 1 && S(a[1], "rmdir") == "s"); return Out(Bool(true), Str(""), Str("")); }
            catch (IOException e) { return Out(Bool(false), Str(e.Message), Str("rmdir")); }
        });
        Reg1("tempdir", a => Str(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
        Reg1("tempname", a => Str(Path.Combine(a.Length > 0 ? S(a[0], "tempname") : Path.GetTempPath(), (a.Length > 1 ? S(a[1], "tempname") : "oct-") + Path.GetRandomFileName().Replace(".", ""))));
        Reg("fileparts", (it, a, n) =>
        {
            var p = S(a[0], "fileparts");
            int slash = Math.Max(p.LastIndexOf('/'), p.LastIndexOf('\\'));
            var dir = slash >= 0 ? p[..slash] : "";
            var rest = p[(slash + 1)..];
            int dot = rest.LastIndexOf('.');
            var nm = dot >= 0 ? rest[..dot] : rest;
            var ext = dot >= 0 ? rest[dot..] : "";
            return Out(Str(dir), Str(nm), Str(ext));
        });
        Reg1("fullfile", a =>
        {
            var parts = a.Select(x => S(x, "fullfile")).Where(s => s != "").ToList();
            var sep = Path.DirectorySeparatorChar;
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                var s = parts[i];
                if (i > 0 && sb.Length > 0 && sb[^1] != '/' && sb[^1] != '\\') sb.Append(sep);
                sb.Append(i > 0 ? s.TrimStart('/', '\\') : s);
            }
            return Str(sb.ToString());
        });

        // ---------------------------------------------------------- delimited
        Reg1("dlmread", a => DlmRead(a, "dlmread"));
        Reg1("csvread", a =>
        {
            var args = new List<Value> { a[0], Str(",") };
            args.AddRange(a.Skip(1));
            return DlmRead(args.ToArray(), "csvread");
        });
        Reg0("dlmwrite", (it, a) => DlmWrite(a, "dlmwrite"));
        Reg0("csvwrite", (it, a) =>
        {
            var args = new List<Value> { a[0], a[1], Str(",") };
            args.AddRange(a.Skip(2));
            DlmWrite(args.ToArray(), "csvwrite");
        });

        // ---------------------------------------------------------- load / save
        Reg("save", (it, a, n) => { Save(it, a); return Array.Empty<Value>(); });
        Reg("load", (it, a, n) => Load(it, a, n));

        // ---------------------------------------------------------- containers.Map
        RegisterMap();
    }

    static void WriteTo(Interpreter it, Value fidv, string s, string fn)
    {
        int fid = I(fidv, fn);
        if (fid == 1) it.Out.Write(s);
        else if (fid == 2) { it.Out.Flush(); Console.Error.Write(s); }
        else it.GetFile(fidv, fn).Write(s);
    }

    // ---------------------------------------------------------------- precision
    static (NumKind inK, NumKind outK) Precision(string p, bool forRead)
    {
        p = p.Replace(" ", "");
        bool star = p.StartsWith("*");
        if (star) p = p[1..];
        string src = p, dst = "double";
        int arrow = p.IndexOf("=>");
        if (arrow >= 0) { src = p[..arrow]; dst = p[(arrow + 2)..]; }
        else if (star) dst = src;
        var k = Prec(src);
        var o = Prec(dst);
        return (k, o);
    }

    static NumKind Prec(string s) => s switch
    {
        "uchar" or "unsigned char" or "uint8" or "ubit8" => NumKind.UInt8,
        "schar" or "signed char" or "int8" or "integer*1" => NumKind.Int8,
        "char" or "char*1" => NumKind.Char,
        "int16" or "short" or "integer*2" => NumKind.Int16,
        "uint16" or "ushort" or "unsigned short" => NumKind.UInt16,
        "int32" or "int" or "long" or "integer*4" => NumKind.Int32,
        "uint32" or "uint" or "ulong" or "unsigned int" => NumKind.UInt32,
        "int64" or "integer*8" => NumKind.Int64,
        "uint64" => NumKind.UInt64,
        "single" or "float32" or "float" or "real*4" => NumKind.Single,
        "double" or "float64" or "real*8" => NumKind.Double,
        "logical" => NumKind.Logical,
        _ => throw new OctaveException($"fread: invalid PRECISION specified")
    };

    static int ByteSize(NumKind k) => k switch
    {
        NumKind.Int8 or NumKind.UInt8 or NumKind.Char or NumKind.Logical => 1,
        NumKind.Int16 or NumKind.UInt16 => 2,
        NumKind.Int32 or NumKind.UInt32 or NumKind.Single => 4,
        _ => 8
    };

    static double Decode(byte[] b, int p, NumKind k) => k switch
    {
        NumKind.Int8 => (sbyte)b[p],
        NumKind.UInt8 or NumKind.Char or NumKind.Logical => b[p],
        NumKind.Int16 => BitConverter.ToInt16(b, p),
        NumKind.UInt16 => BitConverter.ToUInt16(b, p),
        NumKind.Int32 => BitConverter.ToInt32(b, p),
        NumKind.UInt32 => BitConverter.ToUInt32(b, p),
        NumKind.Int64 => BitConverter.ToInt64(b, p),
        NumKind.UInt64 => BitConverter.ToUInt64(b, p),
        NumKind.Single => BitConverter.ToSingle(b, p),
        _ => BitConverter.ToDouble(b, p)
    };

    static void Encode(byte[] b, int p, NumKind k, double x)
    {
        byte[] r;
        switch (k)
        {
            case NumKind.Int8: b[p] = (byte)(sbyte)NumTypes.Saturate(k, x); return;
            case NumKind.UInt8: case NumKind.Char: case NumKind.Logical: b[p] = (byte)NumTypes.Saturate(NumKind.UInt8, x); return;
            case NumKind.Int16: r = BitConverter.GetBytes((short)NumTypes.Saturate(k, x)); break;
            case NumKind.UInt16: r = BitConverter.GetBytes((ushort)NumTypes.Saturate(k, x)); break;
            case NumKind.Int32: r = BitConverter.GetBytes((int)NumTypes.Saturate(k, x)); break;
            case NumKind.UInt32: r = BitConverter.GetBytes((uint)NumTypes.Saturate(k, x)); break;
            case NumKind.Int64: r = BitConverter.GetBytes((long)NumTypes.Saturate(k, x)); break;
            case NumKind.UInt64: r = BitConverter.GetBytes((ulong)NumTypes.Saturate(k, x)); break;
            case NumKind.Single: r = BitConverter.GetBytes((float)x); break;
            default: r = BitConverter.GetBytes(x); break;
        }
        r.CopyTo(b, p);
    }

    /// <summary>Size argument: n, Inf, [m n], [m Inf]. cols = -1 for vector forms, int.MaxValue for Inf.</summary>
    static (int rows, int cols, bool rowsInf) SizeSpec(Value v, string fn)
    {
        var m = M(v, fn);
        if (m.Numel == 1) return double.IsInfinity(m.Re[0]) ? (0, -1, true) : ((int)m.Re[0], -1, false);
        if (m.Numel == 2) return ((int)m.Re[0], double.IsInfinity(m.Re[1]) ? int.MaxValue : (int)m.Re[1], false);
        throw new OctaveException($"{fn}: invalid SIZE specification");
    }

    // ---------------------------------------------------------------- scanf engine
    static readonly Regex NumRx = new(@"\G[+-]?((\d+\.?\d*|\.\d+)([eE][+-]?\d+)?|[Ii]nf(inity)?|NaN|NA)", RegexOptions.Compiled);
    static readonly Regex IntRx = new(@"\G[+-]?\d+", RegexOptions.Compiled);

    sealed record Conv(char Type, bool Skip, int Width, string? Set, string Literal);

    static List<Conv> ParseFmt(string fmt)
    {
        var res = new List<Conv>();
        int i = 0;
        var lit = new StringBuilder();
        while (i < fmt.Length)
        {
            char c = fmt[i];
            if (c == '%' && i + 1 < fmt.Length && fmt[i + 1] != '%')
            {
                if (lit.Length > 0) { res.Add(new Conv('L', false, 0, null, lit.ToString())); lit.Clear(); }
                i++;
                bool skip = false; int width = 0;
                if (fmt[i] == '*') { skip = true; i++; }
                while (i < fmt.Length && char.IsDigit(fmt[i])) width = width * 10 + (fmt[i++] - '0');
                if (i < fmt.Length && fmt[i] == '.') { i++; while (i < fmt.Length && char.IsDigit(fmt[i])) i++; }
                while (i < fmt.Length && fmt[i] is 'l' or 'h' or 'L') i++;
                if (i >= fmt.Length) break;
                char t = fmt[i++];
                string? set = null;
                if (t == '[')
                {
                    int st = i;
                    if (i < fmt.Length && fmt[i] == '^') i++;
                    if (i < fmt.Length && fmt[i] == ']') i++;
                    while (i < fmt.Length && fmt[i] != ']') i++;
                    set = fmt[st..i]; i++;
                }
                res.Add(new Conv(t, skip, width, set, ""));
            }
            else if (c == '%') { lit.Append('%'); i += 2; }
            else { lit.Append(c); i++; }
        }
        if (lit.Length > 0) res.Add(new Conv('L', false, 0, null, lit.ToString()));
        return res;
    }

    static bool InSet(char c, string set)
    {
        bool neg = set.StartsWith("^");
        var s = neg ? set[1..] : set;
        bool hit = false;
        for (int k = 0; k < s.Length; k++)
        {
            if (k + 2 < s.Length && s[k + 1] == '-') { if (c >= s[k] && c <= s[k + 2]) hit = true; k += 2; }
            else if (s[k] == c) hit = true;
        }
        return hit != neg;
    }

    static MatrixValue Scan(string text, string fmt, Value? size, out int used, out int count, out string err)
    {
        var convs = ParseFmt(fmt);
        var vals = new List<double>();
        bool allChar = convs.Any(c => c.Type != 'L' && !c.Skip) && convs.Where(c => c.Type != 'L' && !c.Skip).All(c => c.Type is 's' or 'c' or '[');
        int pos = 0; count = 0; err = "";
        long limit = long.MaxValue;
        (int rows, int cols, bool rowsInf) spec = (0, -1, true);
        if (size != null) { spec = SizeSpec(size, "fscanf"); limit = spec.cols < 0 ? (spec.rowsInf ? long.MaxValue : spec.rows) : spec.cols == int.MaxValue ? long.MaxValue : (long)spec.rows * spec.cols; }
        bool hasConv = convs.Any(c => c.Type != 'L');
        bool stop = false;
        while (!stop && pos < text.Length && vals.Count < limit && hasConv)
        {
            int startPass = pos;
            foreach (var cv in convs)
            {
                if (vals.Count >= limit) { stop = true; break; }
                if (cv.Type == 'L')
                {
                    foreach (char lc in cv.Literal)
                    {
                        if (char.IsWhiteSpace(lc)) { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
                        else if (pos < text.Length && text[pos] == lc) pos++;
                        else { stop = true; break; }
                    }
                    if (stop) break;
                    continue;
                }
                if (cv.Type is not ('c' or '[')) while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
                if (pos >= text.Length) { stop = true; break; }
                int maxEnd = cv.Width > 0 ? Math.Min(text.Length, pos + cv.Width) : text.Length;
                string seg = text[..maxEnd];
                switch (cv.Type)
                {
                    case 'd': case 'i': case 'u':
                    {
                        var mt = IntRx.Match(seg, pos);
                        if (!mt.Success) { stop = true; break; }
                        pos += mt.Length;
                        if (!cv.Skip) { vals.Add(double.Parse(mt.Value, Inv)); count++; }
                        break;
                    }
                    case 'f': case 'g': case 'e': case 'n':
                    {
                        var mt = NumRx.Match(seg, pos);
                        if (!mt.Success) { stop = true; break; }
                        pos += mt.Length;
                        var t = mt.Value.TrimStart('+');
                        double d = t.TrimStart('-').ToLowerInvariant() switch
                        {
                            "inf" or "infinity" => t.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity,
                            "nan" or "na" => double.NaN,
                            _ => double.Parse(t, Inv)
                        };
                        if (!cv.Skip) { vals.Add(d); count++; }
                        break;
                    }
                    case 's':
                    {
                        int st = pos;
                        while (pos < maxEnd && !char.IsWhiteSpace(text[pos])) pos++;
                        if (!cv.Skip) { vals.AddRange(text[st..pos].Select(ch => (double)ch)); count++; }
                        break;
                    }
                    case 'c':
                    {
                        int w = cv.Width > 0 ? cv.Width : 1;
                        int e = Math.Min(text.Length, pos + w);
                        if (!cv.Skip) { vals.AddRange(text[pos..e].Select(ch => (double)ch)); count++; }
                        pos = e;
                        break;
                    }
                    case '[':
                    {
                        int st = pos;
                        while (pos < maxEnd && InSet(text[pos], cv.Set!)) pos++;
                        if (pos == st) { stop = true; break; }
                        if (!cv.Skip) { vals.AddRange(text[st..pos].Select(ch => (double)ch)); count++; }
                        break;
                    }
                    default:
                        throw new OctaveException($"fscanf: invalid format specifier '%{cv.Type}'");
                }
                if (stop) break;
            }
            if (pos == startPass) break;
        }
        used = pos;
        if (pos < text.Length && stop && text[pos..].Trim() != "") err = "";
        MatrixValue m;
        if (allChar && size == null) m = new MatrixValue(1, vals.Count, vals.ToArray(), null, NumKind.Char);
        else if (spec.cols < 0) m = vals.Count == 0 ? new MatrixValue(0, 0) : new MatrixValue(vals.Count, 1, vals.ToArray());
        else
        {
            int r = spec.rows;
            int c = spec.cols == int.MaxValue ? (vals.Count + r - 1) / Math.Max(r, 1) : spec.cols;
            var full = new double[r * c];
            for (int k = 0; k < Math.Min(full.Length, vals.Count); k++) full[k] = vals[k];
            m = new MatrixValue(r, c, full);
        }
        if (allChar && size != null) m.Kind = NumKind.Char;
        return m;
    }

    // ---------------------------------------------------------------- textscan
    static Value TextScan(Interpreter it, Value[] a)
    {
        if (a.Length < 1) throw new OctaveException("Invalid call to textscan");
        string text;
        OctFile? file = null;
        if (IsString(a[0])) text = S(a[0], "textscan");
        else { file = it.GetFile(a[0], "textscan"); text = file.ReadRest(); }
        string fmt = a.Length > 1 && IsString(a[1]) ? S(a[1], "textscan") : "%f";
        int k = a.Length > 1 && IsString(a[1]) ? 2 : 1;
        long repeat = long.MaxValue;
        if (k < a.Length && a[k] is MatrixValue rm && !rm.IsChar) { if (rm.Numel == 1 && !double.IsInfinity(rm.Re[0]) && rm.Re[0] >= 0) repeat = (long)rm.Re[0]; k++; }
        string? delim = null; int header = 0; bool collect = false; var commentStyle = (string?)null; string emptyVal = "NaN";
        double emptyNum = double.NaN;
        for (; k + 1 < a.Length; k += 2)
        {
            var key = S(a[k], "textscan").ToLowerInvariant();
            switch (key)
            {
                case "delimiter":
                    delim = a[k + 1] is CellValue dc ? string.Concat(dc.Data.Select(x => Printf.Escapes(S(x, "textscan")))) : Printf.Escapes(S(a[k + 1], "textscan"));
                    break;
                case "headerlines": header = I(a[k + 1], "textscan"); break;
                case "collectoutput": collect = D(a[k + 1], "textscan") != 0; break;
                case "commentstyle": commentStyle = S(a[k + 1], "textscan"); break;
                case "emptyvalue": emptyNum = D(a[k + 1], "textscan"); emptyVal = ""; break;
                case "endofline": case "whitespace": case "multipledelimsasone": case "returnonerror": break;
                default: throw new OctaveException($"textscan: unrecognized option '{key}'");
            }
        }
        _ = emptyVal;
        var convs = ParseFmt(fmt).Where(c => c.Type != 'L').ToList();
        if (convs.Count == 0) convs.Add(new Conv('f', false, 0, null, ""));
        var lines = text.Replace("\r\n", "\n").Split('\n').Skip(header).ToList();
        if (commentStyle != null) lines = lines.Select(l => { int ci = l.IndexOf(commentStyle, StringComparison.Ordinal); return ci >= 0 ? l[..ci] : l; }).ToList();
        // tokenize all fields in order
        var tokens = new List<string>();
        foreach (var l in lines)
        {
            if (l.Trim() == "") continue;
            if (delim != null)
                tokens.AddRange(l.Split(delim.ToCharArray()).Select(t => t.Trim()));
            else
                tokens.AddRange(l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
        var cols = convs.Select(_ => new List<object>()).ToList();
        int ti = 0; long rec = 0;
        while (ti < tokens.Count && rec < repeat)
        {
            for (int c = 0; c < convs.Count && ti < tokens.Count; c++, ti++)
            {
                var t = tokens[ti];
                var cv = convs[c];
                if (cv.Type is 's' or 'q' or 'c' or '[')
                    cols[c].Add(cv.Type == 'q' ? t.Trim('"') : t);
                else
                {
                    double d;
                    if (t == "") d = emptyNum;
                    else
                    {
                        var mt = NumRx.Match(t, 0);
                        if (!mt.Success) { ti = tokens.Count; break; }
                        var s = mt.Value.TrimStart('+');
                        d = s.TrimStart('-').ToLowerInvariant() switch
                        {
                            "inf" or "infinity" => s.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity,
                            "nan" or "na" => double.NaN,
                            _ => double.Parse(s, Inv)
                        };
                    }
                    cols[c].Add(d);
                }
            }
            rec++;
        }
        if (file != null)
        {
            // textscan consumes the file
        }
        var outs = new List<Value>();
        for (int c = 0; c < convs.Count; c++)
        {
            if (convs[c].Skip) continue;
            var cv = convs[c];
            if (cv.Type is 's' or 'q' or 'c' or '[')
                outs.Add(CellValue.ColOf(cols[c].Select(o => (Value)Str((string)o))));
            else
            {
                var m = MatrixValue.Col(cols[c].Select(o => (double)o));
                if (cv.Type == 'd') m.CastInPlace(NumKind.Int32);
                else if (cv.Type == 'u') m.CastInPlace(NumKind.UInt32);
                outs.Add(m);
            }
        }
        if (collect)
        {
            var merged = new List<Value>();
            foreach (var o in outs)
            {
                if (merged.Count > 0 && merged[^1] is MatrixValue pm && o is MatrixValue om && pm.Kind == om.Kind && pm.R == om.R)
                    merged[^1] = Concat.Horz(new List<Value> { pm, om });
                else merged.Add(o);
            }
            outs = merged;
        }
        return CellValue.RowOf(outs);
    }

    // ---------------------------------------------------------------- dlmread / dlmwrite
    static Value DlmRead(Value[] a, string fn)
    {
        NArgs(a, 1, 6, fn);
        var path = S(a[0], fn);
        if (!File.Exists(path)) throw new OctaveException($"{fn}: unable to open file '{path}'");
        string? sep = null; int r0 = 0, c0 = 0, r1 = int.MaxValue, c1 = int.MaxValue;
        int k = 1;
        if (k < a.Length && IsString(a[k])) { sep = Printf.Escapes(S(a[k], fn)); if (sep == "") sep = null; k++; }
        if (k < a.Length && a[k] is MatrixValue rng && rng.Numel == 4)
        { r0 = (int)rng.Re[0]; c0 = (int)rng.Re[1]; r1 = (int)rng.Re[2]; c1 = (int)rng.Re[3]; k++; }
        else
        {
            if (k < a.Length) { r0 = I(a[k], fn); k++; }
            if (k < a.Length) { c0 = I(a[k], fn); k++; }
        }
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim() == "") lines.RemoveAt(lines.Count - 1);
        if (sep == null)
        {
            var sample = lines.FirstOrDefault(l => l.Trim() != "") ?? "";
            sep = sample.Contains(',') ? "," : sample.Contains('\t') ? "\t" : sample.Contains(';') ? ";" : " ";
        }
        var rows = new List<double[]>();
        foreach (var l in lines)
        {
            var parts = sep == " " ? l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries) : l.Split(sep[0]);
            rows.Add(parts.Select(p =>
            {
                var t = p.Trim();
                if (t == "") return 0.0;
                var mt = NumRx.Match(t, 0);
                if (!mt.Success) return 0.0;
                var s = mt.Value.TrimStart('+');
                return s.TrimStart('-').ToLowerInvariant() switch
                {
                    "inf" or "infinity" => s.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity,
                    "nan" or "na" => double.NaN,
                    _ => double.Parse(s, Inv)
                };
            }).ToArray());
        }
        int nr = rows.Count, nc = rows.Count == 0 ? 0 : rows.Max(x => x.Length);
        r1 = Math.Min(r1, nr - 1); c1 = Math.Min(c1, nc - 1);
        int outR = Math.Max(0, r1 - r0 + 1), outC = Math.Max(0, c1 - c0 + 1);
        var m = new MatrixValue(outR, outC);
        for (int i = 0; i < outR; i++)
            for (int j = 0; j < outC; j++)
            {
                var row = rows[i + r0];
                m.Re[i + j * outR] = j + c0 < row.Length ? row[j + c0] : 0;
            }
        return m;
    }

    static void DlmWrite(Value[] a, string fn)
    {
        NArgs(a, 2, 20, fn);
        var path = S(a[0], fn);
        var m = M(a[1], fn);
        string sep = ",", nl = "\n", prec = "%.16g"; bool append = false; int r0 = 0, c0 = 0;
        int k = 2;
        if (k < a.Length && IsString(a[k]) && S(a[k], fn) is var s0 && !(s0 is "delimiter" or "newline" or "precision" or "-append" or "append" or "roffset" or "coffset"))
        { sep = Printf.Escapes(s0); k++; if (k < a.Length && !IsString(a[k])) { r0 = I(a[k], fn); k++; if (k < a.Length && !IsString(a[k])) { c0 = I(a[k], fn); k++; } } }
        while (k < a.Length)
        {
            var key = S(a[k], fn);
            switch (key)
            {
                case "-append": append = true; k++; break;
                case "append": append = S(a[k + 1], fn) == "on"; k += 2; break;
                case "delimiter": sep = Printf.Escapes(S(a[k + 1], fn)); k += 2; break;
                case "newline": { var v = S(a[k + 1], fn); nl = v switch { "pc" => "\r\n", "unix" => "\n", "mac" => "\r", _ => Printf.Escapes(v) }; k += 2; break; }
                case "precision": prec = a[k + 1] is MatrixValue pm && !pm.IsChar ? $"%.{(int)pm.Re[0]}g" : S(a[k + 1], fn); k += 2; break;
                case "roffset": r0 = I(a[k + 1], fn); k += 2; break;
                case "coffset": c0 = I(a[k + 1], fn); k += 2; break;
                default: throw new OctaveException($"{fn}: unknown option '{key}'");
            }
        }
        var sb = new StringBuilder();
        for (int i = 0; i < r0; i++) sb.Append(new string(sep[0], Math.Max(0, m.C + c0 - 1))).Append(nl);
        for (int i = 0; i < m.R; i++)
        {
            var cells = new List<string>();
            for (int j = 0; j < c0; j++) cells.Add("");
            for (int j = 0; j < m.C; j++)
            {
                int idx = i + j * m.R;
                var re = Printf.Format(prec, new List<Value> { Num(m.Re[idx]) });
                if (m.IsComplex && m.Im![idx] != 0)
                {
                    var im = Printf.Format(prec, new List<Value> { Num(Math.Abs(m.Im[idx])) });
                    re += (m.Im[idx] < 0 ? "-" : "+") + im + "i";
                }
                cells.Add(re);
            }
            sb.Append(string.Join(sep, cells)).Append(nl);
        }
        if (append) File.AppendAllText(path, sb.ToString()); else File.WriteAllText(path, sb.ToString());
    }

    // ---------------------------------------------------------------- save / load
    static bool Glob(string name, string pat) =>
        Regex.IsMatch(name, "^" + Regex.Escape(pat).Replace(@"\*", ".*").Replace(@"\?", ".") + "$");

    static void Save(Interpreter it, Value[] a)
    {
        var args = a.Select(x => S(x, "save")).ToList();
        string fmt = "text"; bool append = false, structMode = false;
        var names = new List<string>();
        string? file = null;
        foreach (var s in args)
        {
            switch (s)
            {
                case "-text": fmt = "text"; continue;
                case "-ascii": fmt = "ascii"; continue;
                case "-double": case "-tabs": continue;
                case "-mat": case "-mat-binary": case "-v6": case "-6": case "-v7": case "-7": case "-mat7-binary": case "-v7.3": fmt = "mat"; continue;
                case "-binary": case "-float-binary": case "-hdf5": case "-float-hdf5": case "-zip": case "-z":
                    throw new OctaveException($"save: format '{s}' is not supported");
                case "-append": append = true; continue;
                case "-struct": structMode = true; continue;
            }
            if (s.StartsWith("-")) throw new OctaveException($"save: Unrecognized option '{s}'");
            if (file == null) file = s; else names.Add(s);
        }
        if (file == null) throw new OctaveException("save: no filename specified");
        if (fmt == "text" && file.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && !args.Contains("-text")) fmt = "mat";
        var vars = new List<(string, Value)>();
        if (structMode)
        {
            if (names.Count == 0) throw new OctaveException("save: missing struct name");
            var sv = it.GetVar(names[0]) as StructValue ?? throw new OctaveException($"save: '{names[0]}' is not a scalar structure");
            if (sv.Numel != 1) throw new OctaveException($"save: '{names[0]}' is not a scalar structure");
            var fl = names.Skip(1).ToList();
            foreach (var f in sv.Fields)
                if (fl.Count == 0 || fl.Any(p => Glob(f, p))) vars.Add((f, sv.Elems[0][f]));
        }
        else
        {
            var all = it.VariableNames.ToList();
            if (names.Count == 0) vars.AddRange(all.Select(nm => (nm, it.GetVar(nm)!)));
            else
                foreach (var p in names)
                {
                    var hits = all.Where(nm => Glob(nm, p)).ToList();
                    if (hits.Count == 0 && !p.Contains('*') && !p.Contains('?'))
                        throw new OctaveException($"save: no such variable '{p}'");
                    foreach (var h in hits) if (!vars.Any(v => v.Item1 == h)) vars.Add((h, it.GetVar(h)!));
                }
        }
        vars.RemoveAll(v => v.Item2 is FunctionHandleValue || v.Item2 is MapValue);
        if (append && File.Exists(file) && fmt != "ascii")
        {
            var old = ReadAny(file);
            foreach (var o in old) if (!vars.Any(v => v.Item1 == o.name)) vars.Insert(0, o);
        }
        switch (fmt)
        {
            case "mat":
                File.WriteAllBytes(file, MatV5.Write(vars));
                break;
            case "ascii":
            {
                var sb = new StringBuilder();
                foreach (var (_, v) in vars)
                {
                    var m = v as MatrixValue ?? throw new OctaveException("save: only numeric data can be saved with -ascii");
                    for (int i = 0; i < m.R; i++)
                    {
                        for (int j = 0; j < m.C; j++) sb.Append(' ').Append(m.Re[i + j * m.R].ToString("0.00000000e+00", Inv));
                        sb.Append('\n');
                    }
                }
                if (append) File.AppendAllText(file, sb.ToString()); else File.WriteAllText(file, sb.ToString());
                break;
            }
            default:
            {
                using var w = new StreamWriter(file, false, new UTF8Encoding(false)) { NewLine = "\n" };
                OctaveText.WriteFile(w, vars);
                break;
            }
        }
    }

    static List<(string name, Value v)> ReadAny(string file)
    {
        var bytes = File.ReadAllBytes(file);
        if (MatV5.IsMat(bytes)) return MatV5.Read(bytes);
        var text = Encoding.UTF8.GetString(bytes);
        if (OctaveText.LooksLikeText(bytes)) return OctaveText.Read(text);
        // plain numeric ASCII matrix
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Select(l => { int c = l.IndexOfAny(new[] { '%', '#' }); return c >= 0 ? l[..c] : l; })
            .Where(l => l.Trim() != "").ToList();
        var rows = lines.Select(l => l.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => double.Parse(t, NumberStyles.Float, Inv)).ToArray()).ToList();
        if (rows.Count > 0 && rows.Any(r => r.Length != rows[0].Length))
            throw new OctaveException($"load: unable to determine file format of '{file}'");
        var m = new MatrixValue(rows.Count, rows.Count == 0 ? 0 : rows[0].Length);
        for (int i = 0; i < m.R; i++) for (int j = 0; j < m.C; j++) m.Re[i + j * m.R] = rows[i][j];
        var vn = Regex.Replace(Path.GetFileNameWithoutExtension(file), @"[^A-Za-z0-9_]", "_");
        if (vn == "" || char.IsDigit(vn[0])) vn = "X" + vn;
        return new List<(string, Value)> { (vn, m) };
    }

    static Value[] Load(Interpreter it, Value[] a, int nargout)
    {
        var args = a.Select(x => S(x, "load")).Where(s => !(s.StartsWith("-") && s.Length > 1)).ToList();
        if (args.Count == 0) throw new OctaveException("load: no filename specified");
        var file = args[0];
        if (!File.Exists(file))
        {
            if (File.Exists(file + ".mat")) file += ".mat";
            else throw new OctaveException($"load: unable to find file {file}");
        }
        var vars = ReadAny(file);
        var pats = args.Skip(1).ToList();
        if (pats.Count > 0) vars = vars.Where(v => pats.Any(p => Glob(v.name, p))).ToList();
        if (nargout > 0)
        {
            var s = new StructValue();
            foreach (var (nm, v) in vars) s.Set(nm, v);
            var head = File.ReadAllBytes(file);
            if (vars.Count == 1 && !MatV5.IsMat(head) && !OctaveText.LooksLikeText(head)) return Out(vars[0].v);
            return Out(s);
        }
        foreach (var (nm, v) in vars) it.SetVar(nm, v);
        return Array.Empty<Value>();
    }

    // ---------------------------------------------------------------- containers.Map
    static void RegisterMap()
    {
        Reg1("containers.Map", a =>
        {
            int k = 0;
            string kt = "char", vt = "any";
            bool uniform = true;
            Value? keys = null, vals = null;
            if (a.Length >= 1 && !(IsString(a[0]) && S(a[0], "containers.Map") is "KeyType" or "ValueType" or "UniformValues"))
            {
                keys = a[0];
                if (a.Length < 2) throw new OctaveException("Map: the number of keys and values must match");
                vals = a[1]; k = 2;
            }
            for (; k + 1 < a.Length; k += 2)
            {
                var key = S(a[k], "containers.Map");
                switch (key)
                {
                    case "KeyType": kt = S(a[k + 1], "containers.Map"); break;
                    case "ValueType": vt = S(a[k + 1], "containers.Map"); break;
                    case "UniformValues": uniform = D(a[k + 1], "containers.Map") != 0; break;
                    default: throw new OctaveException($"Map: invalid option '{key}'");
                }
            }
            _ = uniform;
            if (keys != null && kt == "char")
            {
                bool numeric = keys is MatrixValue km && !km.IsChar || keys is CellValue kc && kc.Data.Length > 0 && kc.Data.All(x => x is MatrixValue xm && !xm.IsChar);
                if (numeric) kt = "double";
            }
            var map = new MapValue(kt, vt);
            if (keys != null)
            {
                var kl = map.Expand(keys).ToList();
                List<Value> vl;
                if (kl.Count == 1 && !(vals is CellValue vc0 && keys is CellValue)) vl = new List<Value> { vals! };
                else if (vals is CellValue vc) vl = vc.Data.ToList();
                else if (vals is MatrixValue vm && vm.Numel == kl.Count) vl = vm.Re.Select(x => (Value)Num(x)).ToList();
                else throw new OctaveException("Map: the number of keys and values must match");
                if (vl.Count != kl.Count) throw new OctaveException("Map: the number of keys and values must match");
                for (int i = 0; i < kl.Count; i++) map.Set(kl[i], vl[i]);
            }
            return map;
        });
        Reg1("isKey", a =>
        {
            var m = a[0] as MapValue ?? throw new OctaveException("isKey: first argument must be a containers.Map");
            if (a[1] is CellValue c)
            {
                var r = new MatrixValue(c.R, c.C, NumKind.Logical);
                for (int i = 0; i < c.Data.Length; i++) r.Re[i] = m.Has(c.Data[i]) ? 1 : 0;
                return r;
            }
            return Bool(m.Has(a[1]));
        });
        Reg1("keys", a => (a[0] as MapValue ?? throw new OctaveException("keys: argument must be a containers.Map")).Keys());
        Reg1("values", a =>
        {
            var m = a[0] as MapValue ?? throw new OctaveException("values: argument must be a containers.Map");
            return a.Length > 1 ? m.Values(a[1]) : m.Values();
        });
        Reg1("remove", a =>
        {
            var m = a[0] as MapValue ?? throw new OctaveException("remove: argument must be a containers.Map");
            m.Remove(a[1]);
            return m;
        });
    }
}
