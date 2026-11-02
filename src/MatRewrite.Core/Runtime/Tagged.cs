// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
namespace MatRewrite.Core.Runtime;

/// <summary>MATLAB value types built on a tagged scalar struct: table/timetable (fields = variables),
/// categorical (Meta codes/cats), datetime (Meta t = datenum, fmt), duration (Meta d = days, fmt).</summary>
public static class Tagged
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static bool Is(Value v, string tag) => v is StructValue s && s.Tag == tag;
    public static bool IsTable(Value v) => v is StructValue s && s.Tag is "table" or "timetable";
    public static bool IsAny(Value v) => v is StructValue s && s.Tag != null;

    // ---------------------------------------------------------------- construction
    public static StructValue MakeTable(List<string> names, List<Value> cols, CellValue? rowNames = null, string tag = "table", Value? rowTimes = null)
    {
        var s = new StructValue { Tag = tag, Meta = new() };
        for (int i = 0; i < names.Count; i++) s.Set(names[i], cols[i]);
        int h = cols.Count > 0 ? Rows(cols[0]) : rowTimes != null ? Rows(rowTimes) : rowNames?.Numel ?? 0;
        s.Meta["H"] = h;
        if (rowNames != null && rowNames.Numel > 0) s.Meta["RowNames"] = rowNames;
        if (rowTimes != null) s.Meta["RowTimes"] = rowTimes;
        s.Meta["Description"] = "";
        return s;
    }
    public static StructValue MakeCat(MatrixValue codes, List<string> cats, bool ordinal = false)
        => new() { Tag = "categorical", Meta = new() { ["codes"] = codes, ["cats"] = cats, ["ordinal"] = ordinal } };
    public static StructValue MakeDT(MatrixValue t, string? fmt = null)
        => new() { Tag = "datetime", Meta = new() { ["t"] = t, ["fmt"] = fmt ?? "" } };
    public static StructValue MakeDur(MatrixValue d, string fmt = "hh:mm:ss")
        => new() { Tag = "duration", Meta = new() { ["d"] = d, ["fmt"] = fmt } };

    static StructValue Own(StructValue s)
    {
        var r = s.Copy();
        if (r.Meta!.TryGetValue("cats", out var c)) r.Meta["cats"] = new List<string>((List<string>)c);
        return r;
    }
    public static MatrixValue Inner(StructValue s) => (MatrixValue)s.Meta![s.Tag switch { "categorical" => "codes", "datetime" => "t", _ => "d" }];
    static StructValue WithInner(StructValue s, MatrixValue m)
    {
        var r = s.Copy();
        r.Meta![s.Tag switch { "categorical" => "codes", "datetime" => "t", _ => "d" }] = m;
        return r;
    }
    public static List<string> Cats(StructValue s) => (List<string>)s.Meta!["cats"];
    public static int Height(StructValue t) => (int)t.Meta!["H"];

    public static int[] TagDims(StructValue s)
    {
        if (s.Meta == null) return new[] { 1, 1 };
        if (s.Tag is "table" or "timetable") return new[] { Height(s), s.Fields.Count };
        return Inner(s).Dims;
    }
    public static int Rows(Value v) => v.Dims[0];

    // ---------------------------------------------------------------- generic row/element helpers
    static MatrixValue IdxVec(int[] rows) { var m = new MatrixValue(rows.Length, 1); for (int i = 0; i < rows.Length; i++) m.Re[i] = rows[i] + 1; return m; }

    /// <summary>Rows `rows` (0-based) of a column value (all trailing columns kept).</summary>
    public static Value TakeRows(Value col, int[] rows)
    {
        if (col is StructValue s && s.Tag is "categorical" or "datetime" or "duration")
            return WithInner(s, (MatrixValue)Indexing.Read(Inner(s), new Value[] { IdxVec(rows), MagicColon.Instance }));
        if (IsTable(col)) return TableParen((StructValue)col, new Value[] { IdxVec(rows), MagicColon.Instance });
        if (col is ArrayValue a)
        {
            var args = new List<Value> { IdxVec(rows) };
            for (int k = 1; k < Math.Max(2, a.NDims); k++) args.Add(MagicColon.Instance);
            return Indexing.Read(a, args.ToArray());
        }
        throw new OctaveException($"table: cannot index variable of class '{col.ClassName}'");
    }

    static int[] Sel(Value arg, int n, Func<string, int> byName, string what)
    {
        if (arg is MagicColon) return Enumerable.Range(0, n).ToArray();
        if (arg is MatrixValue m && m.IsChar) return new[] { byName(m.AsString()) };
        if (arg is CellValue c) return c.Data.Select(x => byName(((MatrixValue)x).AsString())).ToArray();
        if (arg is MatrixValue lm && lm.IsLogical) return Enumerable.Range(0, lm.Numel).Where(i => lm.Re[i] != 0).ToArray();
        if (arg is MatrixValue nm)
            return nm.Re.Select(d => { int i = (int)d - 1; if (d != Math.Floor(d) || i < 0) throw new OctaveException($"{what} index must be a positive integer"); return i; }).ToArray();
        throw new OctaveException($"invalid {what} subscript of class '{arg.ClassName}'");
    }
    static int[] RowSel(StructValue t, Value arg, bool grow = false)
    {
        int h = Height(t);
        var rn = t.Meta!.TryGetValue("RowNames", out var o) ? (CellValue)o : null;
        var r = Sel(arg, h, nm =>
        {
            if (rn != null) for (int i = 0; i < rn.Numel; i++) if (((MatrixValue)rn.Data[i]).AsString() == nm) return i;
            throw new OctaveException($"unrecognized row name '{nm}'");
        }, "row");
        if (!grow && r.Any(i => i >= h)) throw new OctaveException($"row index out of bounds; value {r.Max() + 1} out of bound {h}");
        return r;
    }
    public static int VarIndex(StructValue t, string nm)
    {
        int i = t.Fields.IndexOf(nm);
        if (i < 0) throw new OctaveException($"unrecognized table variable name '{nm}'");
        return i;
    }
    static int[] VarSel(StructValue t, Value arg) => Sel(arg, t.Fields.Count, nm => VarIndex(t, nm), "variable");

    // ---------------------------------------------------------------- indexing
    public static Value Paren(StructValue s, Value[] args)
    {
        if (s.Tag is "table" or "timetable") return TableParen(s, args);
        var inner = Inner(s);
        if (s.Tag == "categorical" && args.Length == 1 && args[0] is MatrixValue { IsChar: true } or CellValue) { }
        return WithInner(s, (MatrixValue)Indexing.Read(inner, args));
    }

    static StructValue TableParen(StructValue t, Value[] args)
    {
        if (args.Length == 1) args = new[] { args[0], MagicColon.Instance };
        if (args.Length != 2) throw new OctaveException("table: subscripting a table requires exactly two subscripts");
        var rows = RowSel(t, args[0]); var vars = VarSel(t, args[1]);
        var names = vars.Select(i => t.Fields[i]).ToList();
        var cols = vars.Select(i => TakeRows(t.Elems[0][t.Fields[i]], rows)).ToList();
        CellValue? rn = t.Meta!.TryGetValue("RowNames", out var o) ? (CellValue)Indexing.Read((CellValue)o, new Value[] { IdxVec(rows) }) : null;
        Value? rt = t.Meta.TryGetValue("RowTimes", out var tt) ? TakeRows((Value)tt, rows) : null;
        var r = MakeTable(names, cols, rn, t.Tag!, rt);
        r.Meta!["H"] = rows.Length;
        return r;
    }

    public static Value Brace(StructValue t, Value[] args)
    {
        if (t.Tag is not ("table" or "timetable")) throw new OctaveException($"'{{' undefined for arguments of type '{t.Tag}'");
        if (args.Length != 2) throw new OctaveException("table: brace subscripting requires exactly two subscripts");
        var sub = TableParen(t, args);
        return Variables(sub);
    }

    public static Value Variables(StructValue t)
    {
        var cols = t.Fields.Select(f => t.Elems[0][f]).ToList();
        if (cols.Count == 0) return MatrixValue.Empty();
        if (cols.Count == 1) return cols[0];
        return MatRewrite.Core.Runtime.Concat.Horz(cols);
    }

    public static bool Dot(StructValue s, string name, out Value v)
    {
        v = null!;
        switch (s.Tag)
        {
            case "table" or "timetable":
                if (s.HasField(name)) return false;
                if (name == "Properties") { v = Props(s); return true; }
                if (name == "Variables") { v = Variables(s); return true; }
                if (name is "Row" && s.Meta!.TryGetValue("RowNames", out var rn)) { v = (Value)rn; return true; }
                if (name is "Time" or "RowTimes" && s.Meta!.TryGetValue("RowTimes", out var rt)) { v = (Value)rt; return true; }
                throw new OctaveException($"unrecognized table variable name '{name}'");
            case "datetime":
            {
                var t = Inner(s);
                if (name == "Format") { v = MatrixValue.Str(DTFmt(s)); return true; }
                Func<DateTime, double>? f = name switch
                {
                    "Year" => d => d.Year, "Month" => d => d.Month, "Day" => d => d.Day, "Hour" => d => d.Hour,
                    "Minute" => d => d.Minute, "Second" => d => d.Second + d.Millisecond / 1000.0, _ => null
                };
                if (f == null) throw new OctaveException($"unknown datetime property '{name}'");
                v = Ops.Map(t, x => double.IsNaN(x) ? double.NaN : f(FromDatenum(x))); return true;
            }
            case "duration":
                if (name == "Format") { v = MatrixValue.Str((string)s.Meta!["fmt"]); return true; }
                throw new OctaveException($"unknown duration property '{name}'");
            case "categorical":
                throw new OctaveException($"unknown categorical property '{name}'");
        }
        return false;
    }

    static Value Props(StructValue t)
    {
        var p = new StructValue();
        p.Set("VariableNames", CellValue.RowOf(t.Fields.Select(f => (Value)MatrixValue.Str(f))));
        p.Set("RowNames", t.Meta!.TryGetValue("RowNames", out var rn) ? (Value)rn : new CellValue(0, 1));
        p.Set("Description", MatrixValue.Str((string)t.Meta["Description"]));
        if (t.Meta.TryGetValue("RowTimes", out var rt)) p.Set("RowTimes", (Value)rt);
        p.Set("DimensionNames", CellValue.RowOf(new Value[] { MatrixValue.Str(t.Tag == "timetable" ? "Time" : "Row"), MatrixValue.Str("Variables") }));
        return p;
    }

    /// <summary>s.name = rhs (or deeper chain via apply) for tagged values; returns null to use plain struct assignment.</summary>
    public static Value? DotAssign(StructValue s, string name, Func<Value?, Value> apply)
    {
        if (s.Tag is "table" or "timetable")
        {
            if (name == "Properties")
            {
                var np = apply(Props(s)) as StructValue ?? throw new OctaveException("invalid Properties assignment");
                var r = s.Copy();
                if (np.HasField("VariableNames"))
                {
                    var vn = CellStrs(np.Get("VariableNames"));
                    if (vn.Count != s.Fields.Count) throw new OctaveException("table: number of VariableNames must equal the number of variables");
                    var vals = s.Fields.Select(f => s.Elems[0][f]).ToList();
                    r = MakeTable(vn, vals, null, s.Tag); r.Meta = new Dictionary<string, object>(s.Meta!);
                }
                if (np.HasField("RowNames")) { var rn = np.Get("RowNames"); if (rn is CellValue rc && rc.Numel > 0) r.Meta!["RowNames"] = ColCell(rc); else r.Meta!.Remove("RowNames"); }
                if (np.HasField("Description")) r.Meta!["Description"] = ((MatrixValue)np.Get("Description")).AsString();
                return r;
            }
            if (name is "Time" or "RowTimes" && s.Meta!.ContainsKey("RowTimes")) { var r = s.Copy(); r.Meta!["RowTimes"] = apply((Value)s.Meta["RowTimes"]); return r; }
            {
                var r = s.Copy();
                var nv = apply(s.HasField(name) ? s.Elems[0][name] : null);
                if (r.Fields.Count > 0 && Rows(nv) != Height(r) && !(nv is MatrixValue em && em.Numel == 0))
                {
                    if (nv.Numel == 1) nv = TakeRows(nv, new int[Height(r)]);
                    else throw new OctaveException($"to assign to or create a variable in a table, the number of rows must match the height of the table ({Height(r)})");
                }
                if (nv is MatrixValue dm && dm.Numel == 0 && dm.Kind == NumKind.Double && r.HasField(name))
                {
                    r.Fields.Remove(name); r.Elems[0].Remove(name); return r;
                }
                if (r.Fields.Count == 0) r.Meta!["H"] = Rows(nv);
                r.Set(name, nv);
                return r;
            }
        }
        if (s.Tag == "datetime" && name == "Format") { var r = s.Copy(); r.Meta!["fmt"] = ((MatrixValue)apply(null)).AsString(); return r; }
        if (s.Tag == "duration" && name == "Format") { var r = s.Copy(); r.Meta!["fmt"] = ((MatrixValue)apply(null)).AsString(); return r; }
        throw new OctaveException($"invalid assignment to property '{name}' of class '{s.Tag}'");
    }

    /// <summary>s(args) = rhs and s{args} = rhs for tagged values.</summary>
    public static Value ParenAssign(Interpreter it, StructValue s, Value[] args, Value rhs, bool brace)
    {
        if (s.Tag is "table" or "timetable")
        {
            if (args.Length != 2) throw new OctaveException("table: subscripted assignment requires two subscripts");
            bool del = !brace && rhs is MatrixValue em && em.Numel == 0 && em.Kind == NumKind.Double && !IsAny(rhs);
            var r = s.Copy();
            if (del)
            {
                if (args[0] is MagicColon)
                {
                    foreach (var vi in VarSel(s, args[1]).OrderByDescending(x => x)) { var f = s.Fields[vi]; r.Fields.Remove(f); r.Elems[0].Remove(f); }
                    return r;
                }
                var rows = RowSel(s, args[0]).ToHashSet();
                var keep = Enumerable.Range(0, Height(s)).Where(i => !rows.Contains(i)).ToArray();
                return TableParen(s, new Value[] { IdxVec(keep), MagicColon.Instance });
            }
            var rr = RowSel(s, args[0], true);
            int[] vv;
            if (args[1] is MatrixValue { IsChar: true } vn && !s.HasField(vn.AsString())) { r.Set(vn.AsString(), DefaultCol(rhs, Height(s))); vv = new[] { r.Fields.Count - 1 }; }
            else vv = VarSel(r, args[1]);
            Value[] parts;
            if (IsTable(rhs)) { var rt = (StructValue)rhs; parts = rt.Fields.Select(f => rt.Elems[0][f]).ToArray(); }
            else if (vv.Length == 1) parts = new[] { rhs };
            else if (rhs is CellValue rc && rc.Numel == rr.Length * vv.Length && !brace)
                parts = Enumerable.Range(0, vv.Length).Select(j => (Value)Indexing.Read(rc, new Value[] { MagicColon.Instance, MatrixValue.Scalar(j + 1) })).ToArray();
            else if (rhs is ArrayValue ra && ra.Dims[^1] == vv.Length && !(rhs.Numel == 1))
                parts = Enumerable.Range(0, vv.Length).Select(j => (Value)Indexing.Read(ra, new Value[] { MagicColon.Instance, MatrixValue.Scalar(j + 1) })).ToArray();
            else parts = vv.Select(_ => rhs).ToArray();
            if (parts.Length != vv.Length) throw new OctaveException("table: right-hand side has the wrong number of variables");
            int newH = Math.Max(Height(s), rr.Length == 0 ? 0 : rr.Max() + 1);
            for (int j = 0; j < vv.Length; j++)
            {
                var f = r.Fields[vv[j]]; var col = r.Elems[0][f];
                var val = parts[j];
                if (col is CellValue cc && !(val is CellValue)) val = new CellValue(1, 1, new[] { val });
                r.Elems[0][f] = AssignRows(col, rr, val);
            }
            r.Meta!["H"] = newH;
            return r;
        }
        if (brace) throw new OctaveException($"'{{' undefined for arguments of type '{s.Tag}'");
        s = Own(s);
        var inner = Inner(s);
        if (rhs is MatrixValue dm && dm.Numel == 0 && dm.Kind == NumKind.Double && !IsAny(rhs)) return WithInner(s, (MatrixValue)Indexing.Delete(inner, args));
        var rv = Coerce(it, s, rhs);
        return WithInner(s, (MatrixValue)Indexing.Assign(inner, args, rv));
    }

    static Value DefaultCol(Value rhs, int h)
        => rhs is CellValue ? new CellValue(h, 1) : IsAny(rhs) ? TakeRows(rhs, new int[h]) : new MatrixValue(h, Math.Max(1, rhs.Dims[^1] == 0 ? 1 : (rhs.Numel == 1 ? 1 : rhs.Dims[^1])));

    static Value AssignRows(Value col, int[] rows, Value val)
    {
        if (col is StructValue cs0 && cs0.Tag is "categorical" or "datetime" or "duration")
        {
            var cs = Own(cs0);
            var rv = Coerce(null, cs, val);
            return WithInner(cs, (MatrixValue)Indexing.Assign(Inner(cs), new Value[] { IdxVec(rows), MagicColon.Instance }, rv));
        }
        if (col is ArrayValue a && val is ArrayValue v)
            return Indexing.Assign(a, new Value[] { IdxVec(rows), MagicColon.Instance }, v);
        throw new OctaveException($"table: cannot assign '{val.ClassName}' into variable of class '{col.ClassName}'");
    }

    /// <summary>Convert an assigned value to the inner representation of s.</summary>
    static MatrixValue Coerce(Interpreter? it, StructValue s, Value rhs)
    {
        switch (s.Tag)
        {
            case "categorical":
            {
                var cats = Cats(s);
                if (rhs is StructValue rs && rs.Tag == "categorical")
                {
                    var rc = Cats(rs); var m = Inner(rs).Copy();
                    for (int i = 0; i < m.Numel; i++) if (m.Re[i] > 0) { var nm = rc[(int)m.Re[i] - 1]; int k = cats.IndexOf(nm); if (k < 0) { cats.Add(nm); k = cats.Count - 1; } m.Re[i] = k + 1; }
                    return m;
                }
                var strs = CellStrs(rhs);
                var res = new MatrixValue(1, strs.Count);
                for (int i = 0; i < strs.Count; i++)
                {
                    if (strs[i] == "" || strs[i] == "<undefined>") { res.Re[i] = 0; continue; }
                    int k = cats.IndexOf(strs[i]);
                    if (k < 0) { cats.Add(strs[i]); k = cats.Count - 1; }
                    res.Re[i] = k + 1;
                }
                return res;
            }
            case "datetime": return ToDatenum(rhs);
            default:
                if (rhs is StructValue d && d.Tag == "duration") return Inner(d);
                if (rhs is MatrixValue n && !n.IsChar) return n.AsDouble();
                throw new OctaveException("duration: right-hand side must be a duration");
        }
    }

    public static MatrixValue ToDatenum(Value v)
    {
        if (v is StructValue d && d.Tag == "datetime") return Inner(d);
        if (v is MatrixValue m && !m.IsChar) return m.AsDouble();
        var strs = CellStrs(v);
        var r = new MatrixValue(strs.Count == 1 ? 1 : strs.Count, 1);
        for (int i = 0; i < strs.Count; i++) r.Re[i] = ParseDate(strs[i]);
        return r;
    }

    // ---------------------------------------------------------------- helpers
    public static List<string> CellStrs(Value v)
    {
        if (v is MatrixValue m && m.IsChar) { if (m.R <= 1) return new() { m.AsString() }; return Enumerable.Range(0, m.R).Select(i => m.RowString(i).TrimEnd()).ToList(); }
        if (v is CellValue c) return c.Data.Select(x => x is MatrixValue xm ? xm.IsChar ? xm.AsString() : Display.FormatScalar(xm) : throw new OctaveException("expected a cell array of strings")).ToList();
        if (v is StructValue s && s.Tag == "categorical") { var cats = Cats(s); return Inner(s).Re.Select(x => x > 0 ? cats[(int)x - 1] : "<undefined>").ToList(); }
        if (v is MatrixValue n) return n.Re.Select(x => x.ToString("G15", Inv)).ToList();
        throw new OctaveException($"expected text, got '{v.ClassName}'");
    }
    static CellValue ColCell(CellValue c) => new(c.Numel, 1, c.Data);

    public static DateTime FromDatenum(double dn)
    {
        // datenum 1 = 1-Jan-0000; .NET DateTime starts 1-Jan-0001 = datenum 367
        double ticks = (dn - 367) * TimeSpan.TicksPerDay;
        if (ticks < 0 || ticks > DateTime.MaxValue.Ticks) return DateTime.MinValue;
        return new DateTime((long)Math.Round(ticks / 10000.0) * 10000);
    }
    public static double ToDatenumD(DateTime d) => d.Ticks / (double)TimeSpan.TicksPerDay + 367;
    public static double ParseDate(string s)
    {
        s = s.Trim();
        if (s is "now") return ToDatenumD(DateTime.Now);
        if (s is "today") return ToDatenumD(DateTime.Today);
        if (s is "tomorrow") return ToDatenumD(DateTime.Today.AddDays(1));
        if (s is "yesterday") return ToDatenumD(DateTime.Today.AddDays(-1));
        if (s is "" or "NaT") return double.NaN;
        string[] fm = { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "dd-MMM-yyyy", "dd-MMM-yyyy HH:mm:ss", "yyyy/MM/dd", "yyyy/MM/dd HH:mm:ss", "MM/dd/yyyy", "MM/dd/yyyy HH:mm:ss", "yyyyMMdd" };
        if (DateTime.TryParseExact(s, fm, Inv, DateTimeStyles.AllowWhiteSpaces, out var d)) return ToDatenumD(d);
        if (DateTime.TryParse(s, Inv, DateTimeStyles.AllowWhiteSpaces, out d)) return ToDatenumD(d);
        throw new OctaveException($"datetime: unable to parse date/time text '{s}'");
    }

    static string DTFmt(StructValue s)
    {
        var f = (string)s.Meta!["fmt"];
        if (f != "") return f;
        var t = Inner(s);
        bool time = t.Re.Any(x => !double.IsNaN(x) && Math.Abs(x - Math.Round(x)) > 1e-9);
        return time ? "dd-MMM-yyyy HH:mm:ss" : "dd-MMM-yyyy";
    }
    /// <summary>MATLAB datetime format (yyyy MMM dd HH mm ss SSS eee a) to .NET.</summary>
    static string NetFmt(string f) => f.Replace("SSS", "fff").Replace("SS", "ff").Replace("eeee", "dddd").Replace("eee", "ddd").Replace("a", "tt").Replace("'T'", "\\T");
    public static string FormatDT(StructValue s, int k)
    {
        double x = Inner(s).Re[k];
        if (double.IsNaN(x)) return "NaT";
        return FromDatenum(x).ToString(NetFmt(DTFmt(s)), Inv);
    }
    public static string FormatDur(StructValue s, int k)
    {
        double d = Inner(s).Re[k]; var f = (string)s.Meta!["fmt"];
        if (double.IsNaN(d)) return "NaN";
        string G(double v, string u) => v.ToString("0.####", Inv) + " " + u;
        switch (f)
        {
            case "y": return G(d / 365.2425, "yrs");
            case "d": return G(d, d == 1 ? "day" : "days");
            case "h": return G(d * 24, "hr");
            case "m": return G(d * 1440, "min");
            case "s": return G(d * 86400, "sec");
        }
        double secs = d * 86400; string sign = secs < 0 ? "-" : ""; secs = Math.Abs(secs);
        long h = (long)(secs / 3600); double rem = secs - h * 3600; long mi = (long)(rem / 60); double se = rem - mi * 60;
        if (f.StartsWith("dd:")) { long dd = h / 24; h %= 24; return $"{sign}{dd:00}:{h:00}:{mi:00}:{se:00}"; }
        if (f == "mm:ss") return $"{sign}{h * 60 + mi:00}:{se:00}";
        return f.EndsWith(".SSS") ? $"{sign}{h:00}:{mi:00}:{se:00.000}" : $"{sign}{h:00}:{mi:00}:{Math.Floor(se + 1e-9):00}";
    }

    public static string ElemString(Value v, int k)
    {
        switch (v)
        {
            case StructValue s when s.Tag == "categorical": { var c = Inner(s).Re[k]; return c > 0 ? Cats(s)[(int)c - 1] : "<undefined>"; }
            case StructValue s when s.Tag == "datetime": return FormatDT(s, k);
            case StructValue s when s.Tag == "duration": return FormatDur(s, k);
            case CellValue c:
            {
                var e = c.Data[k];
                if (e is MatrixValue em && em.IsChar && em.R <= 1) return "{'" + em.AsString() + "'}";
                if (e is MatrixValue en && en.Numel == 1) return "{[" + Display.FormatScalar(en) + "]}";
                return "{" + e.DimString + " " + e.ClassName + "}";
            }
            case MatrixValue m when m.IsChar: return "'" + m.RowString(k % Math.Max(m.R, 1)) + "'";
            case MatrixValue m:
            {
                var one = (MatrixValue)m.Gather(new[] { k }, 1, 1);
                if (m.IsLogical) return m.Re[k] != 0 ? "true" : "false";
                if (!m.IsInteger && !m.IsComplex && !AllIntegers(m)) { double x = m.Re[k]; return double.IsNaN(x) ? "NaN" : double.IsInfinity(x) ? (x > 0 ? "Inf" : "-Inf") : Math.Abs(x) >= 1e5 || (Math.Abs(x) < 1e-4 && x != 0) ? x.ToString("0.####e+00", Inv) : x.ToString("0.0000", Inv); }
                return Display.FormatScalar(one);
            }
        }
        return v.ClassName;
    }
    static bool AllIntegers(MatrixValue m) => m.Re.All(x => double.IsNaN(x) || double.IsInfinity(x) || x == Math.Floor(x));

    // ---------------------------------------------------------------- display
    public static string Body(StructValue s)
    {
        var sb = new StringBuilder();
        if (s.Tag is "table" or "timetable")
        {
            int h = Height(s);
            var headers = new List<string>(); var colsTxt = new List<string[]>();
            if (s.Meta!.TryGetValue("RowTimes", out var rto)) { var rt = (Value)rto; headers.Add("Time"); colsTxt.Add(Enumerable.Range(0, h).Select(i => ElemString(rt, i)).ToArray()); }
            else if (s.Meta.TryGetValue("RowNames", out var rno)) { var rn = (CellValue)rno; headers.Add(""); colsTxt.Add(rn.Data.Select(x => ((MatrixValue)x).AsString()).ToArray()); }
            foreach (var f in s.Fields)
            {
                var v = s.Elems[0][f];
                int w = v is ArrayValue av && !(v is MatrixValue { IsChar: true }) ? Math.Max(1, av.Numel / Math.Max(1, h)) : 1;
                headers.Add(f);
                colsTxt.Add(Enumerable.Range(0, h).Select(i => w == 1 ? ElemString(v, i) : string.Join("  ", Enumerable.Range(0, w).Select(j => ElemString(v, i + j * h)))).ToArray());
            }
            sb.Append($"  {h}x{s.Fields.Count} {s.Tag}\n\n");
            var widths = headers.Select((hh, j) => Math.Max(hh.Length, colsTxt[j].Length == 0 ? 0 : colsTxt[j].Max(x => x.Length))).ToArray();
            sb.Append("    ").Append(string.Join("    ", headers.Select((hh, j) => Center(hh, widths[j])))).Append('\n');
            sb.Append("    ").Append(string.Join("    ", headers.Select((hh, j) => hh == "" ? new string(' ', widths[j]) : Center(new string('_', Math.Max(hh.Length, Math.Min(widths[j], hh.Length + 4))), widths[j])))).Append("\n\n");
            for (int i = 0; i < h; i++)
                sb.Append("    ").Append(string.Join("    ", colsTxt.Select((c, j) => j == 0 && headers[0] == "" ? c[i].PadRight(widths[j]) : c[i].PadLeft(widths[j])))).Append('\n');
            return sb.ToString();
        }
        var inner = Inner(s);
        if (inner.Numel == 0) return $"  {inner.DimString} empty {s.Tag} array\n";
        var strs = Enumerable.Range(0, inner.Numel).Select(k => ElemString(s, k)).ToArray();
        int wd = strs.Max(x => x.Length);
        int R = inner.R, C = inner.Numel / Math.Max(1, inner.R);
        for (int i = 0; i < R; i++)
        {
            sb.Append("  ");
            for (int j = 0; j < C; j++) sb.Append("  ").Append(strs[i + j * R].PadLeft(wd));
            sb.Append('\n');
        }
        return sb.ToString();
    }
    static string Center(string s, int w) { int l = (w - s.Length) / 2; return (new string(' ', Math.Max(0, l)) + s).PadRight(w); }

    public static string Show(string name, StructValue s, string ind)
    {
        var inner = s.Tag is "table" or "timetable" ? null : Inner(s);
        if (inner != null && inner.Numel == 1) return $"{ind}{name} = {ElemString(s, 0)}\n";
        if (inner != null) return $"{ind}{name} =\n\n  {inner.DimString} {s.Tag} array\n\n{Body(s)}\n";
        return $"{ind}{name} =\n\n{Body(s)}\n";
    }

    // ---------------------------------------------------------------- operators
    public static Value Binary(Interpreter? it, string op, Value a, Value b)
    {
        var sa = a as StructValue; var sb = b as StructValue;
        string? ta = sa?.Tag, tb = sb?.Tag;
        bool cmp = op is "==" or "!=" or "<" or "<=" or ">" or ">=";
        if (ta == "categorical" || tb == "categorical")
        {
            if (!(op is "==" or "!=") && !(cmp && ((bool)(sa ?? sb)!.Meta!["ordinal"])))
                throw new OctaveException($"relational operator '{op}' is only defined for ordinal categorical arrays");
            var cs = (ta == "categorical" ? sa : sb)!;
            var cats = Cats(cs);
            MatrixValue Codes(Value v) => v is StructValue q && q.Tag == "categorical"
                ? Ops.Map(Inner(q), x => x > 0 ? cats.IndexOf(Cats(q)[(int)x - 1]) + 1 : 0)
                : MatrixValue.Row(CellStrs(v).Select(x => (double)(cats.IndexOf(x) + 1)));
            var ca = Codes(a); var cb = Codes(b);
            var r = (MatrixValue)Ops.Binary(op, ca, cb);
            // undefined never equals anything
            if (op == "==") return Ops.Binary("&", r, Ops.Binary("&", Ops.Binary(">", ca, MatrixValue.Scalar(0)), Ops.Binary(">", cb, MatrixValue.Scalar(0))));
            return r;
        }
        if (ta == "datetime" || tb == "datetime")
        {
            if (ta == "datetime" && tb == "datetime" || cmp)
            {
                var r = Ops.Binary(op, ToDatenum(a), ToDatenum(b));
                if (op == "-") return MakeDur((MatrixValue)r);
                if (cmp) return r;
                throw new OctaveException($"datetime: operator '{op}' not defined for two datetimes");
            }
            if (op is "+" or "-")
            {
                var dt = ta == "datetime" ? sa! : sb!;
                var other = ta == "datetime" ? b : a;
                if (op == "-" && tb == "datetime") throw new OctaveException("datetime: cannot subtract a datetime from a non-datetime");
                var od = other is StructValue os && os.Tag == "duration" ? Inner(os) : ((MatrixValue)other).AsDouble();
                return WithInner(dt, (MatrixValue)Ops.Binary(op, Inner(dt), od));
            }
            throw new OctaveException($"datetime: operator '{op}' is not supported");
        }
        // duration
        MatrixValue D(Value v) => v is StructValue q && q.Tag == "duration" ? Inner(q) : v is MatrixValue m && !m.IsChar ? m.AsDouble() : throw new OctaveException($"duration: invalid operand of class '{v.ClassName}'");
        var ds = (ta == "duration" ? sa : sb)!;
        if (cmp) return Ops.Binary(op, D(a), D(b));
        if (op is "+" or "-") return WithInner(ds, (MatrixValue)Ops.Binary(op, D(a), D(b)));
        if (op is "*" or ".*") { if (ta == "duration" && tb == "duration") throw new OctaveException("duration: cannot multiply two durations"); return WithInner(ds, (MatrixValue)Ops.Binary(op, D(a), D(b))); }
        if (op is "/" or "./")
        {
            var r = (MatrixValue)Ops.Binary(op, D(a), D(b));
            return ta == "duration" && tb == "duration" ? r : WithInner(ds, r);
        }
        throw new OctaveException($"duration: operator '{op}' is not supported");
    }
    public static Value Unary(string op, StructValue s)
    {
        if (s.Tag == "duration" && op is "-" or "+") return WithInner(s, (MatrixValue)Ops.Unary(op, Inner(s)));
        throw new OctaveException($"unary operator '{op}' not implemented for '{s.Tag}' operations");
    }
    public static Value Transpose(StructValue s)
    {
        if (s.Tag is "table" or "timetable") throw new OctaveException("table: transpose is not supported");
        return WithInner(s, (MatrixValue)Ops.Transpose(Inner(s), false));
    }

    public static Value Concat(List<Value> items, bool horiz)
    {
        var parts = items.Where(v => !(v is MatrixValue em && em.Numel == 0 && !IsAny(v))).ToList();
        var first = parts.FirstOrDefault(IsAny) as StructValue;
        if (first == null) return MatrixValue.Empty();
        if (first.Tag is "table" or "timetable")
        {
            var tabs = parts.Cast<StructValue>().ToList();
            if (horiz)
            {
                var names = new List<string>(); var cols = new List<Value>();
                foreach (var t in tabs) foreach (var f in t.Fields)
                    { if (names.Contains(f)) throw new OctaveException($"table: duplicate variable name '{f}'"); names.Add(f); cols.Add(t.Elems[0][f]); }
                var r = MakeTable(names, cols, first.Meta!.TryGetValue("RowNames", out var rn) ? (CellValue)rn : null, first.Tag);
                r.Meta!["H"] = Height(first);
                return r;
            }
            var cols2 = first.Fields.Select(f => (Value)Runtime.Concat.Vert(tabs.Select(t => t.Elems[0][t.Fields.Contains(f) ? f : throw new OctaveException("table: vertical concatenation requires the same variable names")]).ToList())).ToList();
            Value? rt = first.Meta!.ContainsKey("RowTimes") ? Concat(tabs.Select(t => (Value)t.Meta!["RowTimes"]).ToList(), false) : null;
            var v = MakeTable(first.Fields.ToList(), cols2, null, first.Tag, rt);
            v.Meta!["H"] = tabs.Sum(Height);
            return v;
        }
        first = Own(first);
        var inner = parts.Select(p => (Value)(p is StructValue ps && ps.Tag == first.Tag && ps.Tag != "categorical" ? Inner(ps) : Coerce(null, first, p))).ToList();
        var joined = (MatrixValue)(horiz ? Runtime.Concat.Horz(inner) : Runtime.Concat.Vert(inner));
        return WithInner(first, joined);
    }
}
