// MatRewrite - GPL-3.0-or-later
using System.Globalization;
using System.Text;
using MatRewrite.Core.Runtime;
using static MatRewrite.Core.Runtime.Tagged;
namespace MatRewrite.Core.Builtins;

/// <summary>Phase 13: table/timetable, categorical, datetime, duration builtins.</summary>
public static partial class BuiltinLibrary
{
    static readonly CultureInfo InvC = CultureInfo.InvariantCulture;
    static Value[] One(Value v) => new[] { v };
    static StructValue? TS(Value v) => v is StructValue s && s.Tag != null ? s : null;
    static Value CallF(Interpreter it, Value f, Value[] args, int n = 1)
    {
        if (f is FunctionHandleValue fh) { var r = it.CallHandle(fh, args, n); return r.Length > 0 ? r[0] : MatrixValue.Empty(); }
        return it.CallFunction(S(f, "feval"), args, n)[0];
    }
    static string FnName(Value f) => f is FunctionHandleValue fh ? fh.Name ?? "Fun" : S(f, "varfun");
    static CellValue CellRow(IEnumerable<string> s) => CellValue.RowOf(s.Select(x => (Value)MatrixValue.Str(x)));
    static CellValue TCol(IEnumerable<string> s) => CellValue.ColOf(s.Select(x => (Value)MatrixValue.Str(x)));
    static (List<Value> pos, Dictionary<string, Value> opts) SplitOpts(Value[] a, params string[] names)
    {
        var pos = new List<Value>(); var opts = new Dictionary<string, Value>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < a.Length; i++)
        {
            if (IsString(a[i]) && i + 1 < a.Length && names.Any(n => n.Equals(S(a[i], "opt"), StringComparison.OrdinalIgnoreCase)))
            { opts[S(a[i], "opt")] = a[i + 1]; i++; }
            else pos.Add(a[i]);
        }
        return (pos, opts);
    }
    static Value AsCol(Value v)
    {
        if (v is MatrixValue m && m.R == 1 && m.C > 1 && !m.IsChar) return Ops.Transpose(m, false);
        if (v is CellValue c && c.R == 1 && c.C > 1) return new CellValue(c.C, 1, c.Data);
        if (v is StructValue s && s.Tag is "categorical" or "datetime" or "duration" && s.Dims[0] == 1 && s.Dims[1] > 1) return Tagged.Transpose(s);
        return v;
    }
    static string ValidName(string s, int i)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.Trim()) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        var r = sb.ToString().Trim('_');
        if (r == "") return "Var" + (i + 1);
        if (!char.IsLetter(r[0])) r = "x" + r;
        return r;
    }
    static StructValue TableOf(Value v, string fn) => v is StructValue s && s.Tag is "table" or "timetable" ? s : throw new OctaveException($"{fn}: argument must be a table");

    static void RegisterPhase13Tables()
    {
        var B = cur!.Builtins;
        // ------------------------------------------------------------ table
        Reg("table", (it, a, n) =>
        {
            var names0 = it.CallArgNames; it.CallArgNames = null;
            var (pos, opts) = SplitOpts(a, "VariableNames", "RowNames", "Size", "VariableTypes", "DimensionNames");
            var cols = pos.Select(AsCol).ToList();
            List<string> names;
            if (opts.TryGetValue("VariableNames", out var vn)) names = CellStrs(vn);
            else names = cols.Select((c, i) => names0 != null && i < names0.Length && names0[i] != "" && names0.Take(i).All(x => x != names0[i]) ? names0[i] : "Var" + (i + 1)).ToList();
            if (opts.TryGetValue("Size", out var sz))
            {
                var d = M(sz, "table"); int h = (int)d.Re[0], w = (int)d.Re[1];
                var types = opts.TryGetValue("VariableTypes", out var vt) ? CellStrs(vt) : Enumerable.Repeat("double", w).ToList();
                if (!opts.ContainsKey("VariableNames")) names = Enumerable.Range(1, w).Select(i => "Var" + i).ToList();
                cols = types.Select(t => t switch
                {
                    "cell" or "cellstr" or "string" => (Value)new CellValue(h, 1),
                    "logical" => new MatrixValue(h, 1, NumKind.Logical),
                    "datetime" => MakeDT(Fill(h, double.NaN)),
                    "duration" => MakeDur(new MatrixValue(h, 1)),
                    "categorical" => MakeCat(new MatrixValue(h, 1), new()),
                    _ => NumTypes.FromName(t) is NumKind k ? new MatrixValue(h, 1, k) : new MatrixValue(h, 1)
                }).ToList();
            }
            if (names.Count != cols.Count) throw new OctaveException("table: the number of VariableNames must match the number of variables");
            if (names.Distinct().Count() != names.Count) throw new OctaveException("table: duplicate variable names");
            int hh = cols.Count > 0 ? Tagged.Rows(cols[0]) : 0;
            CellValue? rn = opts.TryGetValue("RowNames", out var rno) ? TCol(CellStrs(rno)) : null;
            for (int i = 0; i < cols.Count; i++)
                if (Tagged.Rows(cols[i]) != hh)
                {
                    if (cols[i].Numel == 1 && !(cols[i] is MatrixValue { IsChar: true })) cols[i] = TakeRows(cols[i], new int[hh]);
                    else throw new OctaveException("table: all variables must have the same number of rows");
                }
                else if (cols[i] is MatrixValue { IsChar: true } cm && cm.R > 1) cols[i] = TCol(CellStrs(cm));
            var t = MakeTable(names, cols, rn);
            if (cols.Count == 0 && rn != null) t.Meta!["H"] = rn.Numel;
            return One(t);
        });
        Reg("timetable", (it, a, n) =>
        {
            var names0 = it.CallArgNames; it.CallArgNames = null;
            var (pos, opts) = SplitOpts(a, "VariableNames", "RowTimes", "SampleRate", "TimeStep", "StartTime");
            Value rt; int off = 0;
            if (opts.TryGetValue("RowTimes", out var r0)) rt = AsCol(r0);
            else if (pos.Count > 0 && TS(pos[0])?.Tag is "datetime" or "duration") { rt = AsCol(pos[0]); off = 1; }
            else if (opts.TryGetValue("TimeStep", out var ts) || opts.TryGetValue("SampleRate", out _))
            {
                int h = pos.Count > 0 ? Tagged.Rows(AsCol(pos[0])) : 0;
                double step = ts != null ? Inner((StructValue)ts).Re[0] : 1.0 / D(opts["SampleRate"], "timetable") / 86400;
                double st = opts.TryGetValue("StartTime", out var s0) ? Inner((StructValue)s0).Re[0] : 0;
                var m = new MatrixValue(h, 1); for (int i = 0; i < h; i++) m.Re[i] = st + i * step;
                rt = s0 is StructValue { Tag: "datetime" } ? MakeDT(m) : MakeDur(m, "s");
            }
            else throw new OctaveException("timetable: row times must be the first argument or given with 'RowTimes'");
            var cols = pos.Skip(off).Select(AsCol).ToList();
            var names = opts.TryGetValue("VariableNames", out var vn) ? CellStrs(vn)
                : cols.Select((c, i) => names0 != null && i + off < names0.Length && names0[i + off] != "" ? names0[i + off] : "Var" + (i + 1)).ToList();
            return One(MakeTable(names, cols, null, "timetable", rt));
        });
        Reg1("istable", a => Bool(Is(a[0], "table")));
        Reg1("istimetable", a => Bool(Is(a[0], "timetable")));
        Reg1("istabular", a => Bool(IsTable(a[0])));
        Reg1("height", a => Num(a[0].Dims[0]));
        Reg1("width", a => Num(IsTable(a[0]) ? ((StructValue)a[0]).Fields.Count : a[0].Dims.Length > 1 ? a[0].Dims[1] : 1));
        Reg("array2table", (it, a, n) =>
        {
            var (pos, opts) = SplitOpts(a, "VariableNames", "RowNames");
            var m = (ArrayValue)pos[0]; int w = m.Dims[1];
            var cols = Enumerable.Range(0, w).Select(j => (Value)Indexing.Read(m, new Value[] { MagicColon.Instance, Num(j + 1) })).ToList();
            var names = opts.TryGetValue("VariableNames", out var vn) ? CellStrs(vn) : Enumerable.Range(1, w).Select(i => (it.CallArgNames?.FirstOrDefault() is string nm && nm != "" ? nm : "Var") + i).ToList();
            if (!opts.ContainsKey("VariableNames")) names = Enumerable.Range(1, w).Select(i => "Var" + i).ToList();
            return One(MakeTable(names, cols, opts.TryGetValue("RowNames", out var rn) ? TCol(CellStrs(rn)) : null));
        });
        Reg("cell2table", (it, a, n) =>
        {
            var (pos, opts) = SplitOpts(a, "VariableNames", "RowNames");
            var c = (CellValue)pos[0]; int h = c.R, w = c.C;
            var cols = new List<Value>();
            for (int j = 0; j < w; j++)
            {
                var items = Enumerable.Range(0, h).Select(i => c.Data[i + j * h]).ToList();
                bool num = items.All(x => x is MatrixValue m && !m.IsChar && m.Numel == 1);
                cols.Add(num ? Runtime.Concat.Vert(items) : new CellValue(h, 1, items.ToArray()));
            }
            var names = opts.TryGetValue("VariableNames", out var vn) ? CellStrs(vn) : Enumerable.Range(1, w).Select(i => "Var" + i).ToList();
            return One(MakeTable(names, cols, opts.TryGetValue("RowNames", out var rn) ? TCol(CellStrs(rn)) : null));
        });
        Reg1("struct2table", a =>
        {
            var s = (StructValue)a[0];
            if (s.Numel == 1) return MakeTable(s.Fields.ToList(), s.Fields.Select(f => AsCol(s.Elems[0][f])).ToList());
            var cols = s.Fields.Select(f =>
            {
                var items = s.Elems.Select(e => e[f]).ToList();
                bool num = items.All(x => x is MatrixValue m && !m.IsChar && m.Numel == 1);
                return num ? Runtime.Concat.Vert(items) : (Value)new CellValue(items.Count, 1, items.ToArray());
            }).ToList();
            return MakeTable(s.Fields.ToList(), cols);
        });
        Reg1("table2array", a => Variables(TableOf(a[0], "table2array")));
        Reg1("table2cell", a =>
        {
            var t = TableOf(a[0], "table2cell"); int h = Height(t), w = t.Fields.Count;
            var c = new CellValue(h, w);
            for (int j = 0; j < w; j++)
            {
                var col = t.Elems[0][t.Fields[j]];
                for (int i = 0; i < h; i++)
                    c.Data[i + j * h] = col is CellValue cc && cc.Dims[1] == 1 ? cc.Data[i] : TakeRows(col, new[] { i });
            }
            return c;
        });
        Reg1("table2struct", a =>
        {
            var t = TableOf(a[0], "table2struct"); int h = Height(t);
            bool scalar = a.Length >= 3 && S(a[1], "table2struct").Equals("ToScalar", StringComparison.OrdinalIgnoreCase) && Ops.IsTrue(a[2]);
            if (scalar) { var s1 = new StructValue(); foreach (var f in t.Fields) s1.Set(f, t.Elems[0][f]); return s1; }
            var s = new StructValue(h, 1, t.Fields);
            for (int i = 0; i < h; i++) foreach (var f in t.Fields)
            {
                var col = t.Elems[0][f];
                s.Elems[i][f] = col is CellValue cc && cc.Dims[1] == 1 ? cc.Data[i] : TakeRows(col, new[] { i });
            }
            return s;
        });
        Reg1("removevars", a =>
        {
            var t = TableOf(a[0], "removevars").Copy();
            var drop = a[1] is MatrixValue m && !m.IsChar ? m.Re.Select(x => t.Fields[(int)x - 1]).ToList() : CellStrs(a[1]);
            foreach (var f in drop) { VarIndex(t, f); t.Fields.Remove(f); t.Elems[0].Remove(f); }
            return t;
        });
        Reg1("renamevars", a =>
        {
            var t = TableOf(a[0], "renamevars");
            var olds = a[1] is MatrixValue m && !m.IsChar ? m.Re.Select(x => t.Fields[(int)x - 1]).ToList() : CellStrs(a[1]);
            var news = CellStrs(a[2]);
            var names = t.Fields.Select(f => olds.IndexOf(f) is int k && k >= 0 ? news[k] : f).ToList();
            var r = MakeTable(names, t.Fields.Select(f => t.Elems[0][f]).ToList(), null, t.Tag!);
            r.Meta = new Dictionary<string, object>(t.Meta!);
            return r;
        });
        Reg("addvars", (it, a, n) =>
        {
            var t = TableOf(a[0], "addvars");
            var (pos, opts) = SplitOpts(a.Skip(1).ToArray(), "NewVariableNames", "Before", "After");
            var names = t.Fields.ToList(); var cols = names.Select(f => t.Elems[0][f]).ToList();
            var nn = opts.TryGetValue("NewVariableNames", out var nv) ? CellStrs(nv) : pos.Select((_, i) => "Var" + (names.Count + i + 1)).ToList();
            int at = names.Count;
            if (opts.TryGetValue("Before", out var bf)) at = bf is MatrixValue bm && !bm.IsChar ? (int)bm.Re[0] - 1 : names.IndexOf(S(bf, "addvars"));
            if (opts.TryGetValue("After", out var af)) at = (af is MatrixValue am && !am.IsChar ? (int)am.Re[0] - 1 : names.IndexOf(S(af, "addvars"))) + 1;
            for (int i = 0; i < pos.Count; i++) { names.Insert(at + i, nn[i]); cols.Insert(at + i, AsCol(pos[i])); }
            var r = MakeTable(names, cols, null, t.Tag!, t.Meta!.TryGetValue("RowTimes", out var rt) ? (Value)rt : null);
            if (t.Meta.TryGetValue("RowNames", out var rn)) r.Meta!["RowNames"] = rn;
            return One(r);
        });
        Reg("movevars", (it, a, n) =>
        {
            var t = TableOf(a[0], "movevars");
            var mv = a[1] is MatrixValue m && !m.IsChar ? m.Re.Select(x => t.Fields[(int)x - 1]).ToList() : CellStrs(a[1]);
            var names = t.Fields.Where(f => !mv.Contains(f)).ToList();
            var (_, opts) = SplitOpts(a.Skip(2).ToArray(), "Before", "After");
            int at = names.Count;
            if (opts.TryGetValue("Before", out var bf)) at = names.IndexOf(bf is MatrixValue bm && !bm.IsChar ? t.Fields[(int)bm.Re[0] - 1] : S(bf, "movevars"));
            if (opts.TryGetValue("After", out var af)) at = names.IndexOf(af is MatrixValue am && !am.IsChar ? t.Fields[(int)am.Re[0] - 1] : S(af, "movevars")) + 1;
            names.InsertRange(at, mv);
            var r = t.Copy(); r.Fields = names; return One(r);
        });
        // head / tail
        foreach (var (nm, tail) in new[] { ("head", false), ("tail", true) })
        {
            Reg1(nm, a =>
            {
                int k = a.Length > 1 ? I(a[1], nm) : 8; int h = a[0].Dims[0]; k = Math.Min(k, h);
                var rows = Enumerable.Range(tail ? h - k : 0, k).ToArray();
                return TakeRows(a[0], rows);
            });
        }
        // sortrows
        var oldSortrows = B["sortrows"];
        Reg("sortrows", (it, a, n) =>
        {
            if (!IsTable(a[0])) return oldSortrows(it, a, n);
            var t = (StructValue)a[0]; int h = Height(t);
            var keys = new List<(Value col, bool desc)>();
            bool allDesc = a.Skip(1).Any(v => IsString(v) && S(v, "sortrows").Equals("descend", StringComparison.OrdinalIgnoreCase));
            var spec = a.Length > 1 && !(IsString(a[1]) && S(a[1], "") is "descend" or "ascend") ? a[1] : null;
            if (spec == null) keys.AddRange(t.Fields.Select(f => (t.Elems[0][f], allDesc)));
            else if (spec is MatrixValue sm && !sm.IsChar) keys.AddRange(sm.Re.Select(x => (t.Elems[0][t.Fields[(int)Math.Abs(x) - 1]], x < 0 || allDesc)));
            else
            {
                var dirs = a.Length > 2 && a[2] is CellValue dc ? CellStrs(dc) : null;
                var names = CellStrs(spec);
                for (int i = 0; i < names.Count; i++)
                {
                    bool d = dirs != null ? dirs[i] == "descend" : allDesc;
                    keys.Add((names[i] is "RowNames" or "Row" && t.Meta!.ContainsKey("RowNames") ? (Value)t.Meta["RowNames"] : names[i] == "Time" && t.Meta!.ContainsKey("RowTimes") ? (Value)t.Meta["RowTimes"] : t.Elems[0][names[i]], d));
                }
            }
            if (Is(t, "timetable") && spec == null) keys.Insert(0, ((Value)t.Meta!["RowTimes"], allDesc));
            var idx = Enumerable.Range(0, h).ToArray();
            var cmps = keys.Select(k => RowComparer(k.col, k.desc)).ToList();
            var sorted = idx.OrderBy(i => i, Comparer<int>.Create((x, y) => { foreach (var c in cmps) { int r = c(x, y); if (r != 0) return r; } return 0; })).ToArray();
            var res = Tagged.Paren(t, new Value[] { MatrixValue.Col(sorted.Select(i => (double)i + 1)), MagicColon.Instance });
            return n > 1 ? new[] { res, MatrixValue.Col(sorted.Select(i => (double)i + 1)) } : One(res);
        });
        // varfun
        Reg("varfun", (it, a, n) =>
        {
            var f = a[0]; var t = TableOf(a[1], "varfun");
            var (_, opts) = SplitOpts(a.Skip(2).ToArray(), "InputVariables", "OutputFormat", "GroupingVariables");
            var vars = opts.TryGetValue("InputVariables", out var iv) ? (iv is MatrixValue im && !im.IsChar ? im.Re.Select(x => t.Fields[(int)x - 1]).ToList() : CellStrs(iv)) : t.Fields.ToList();
            bool uniform = opts.TryGetValue("OutputFormat", out var of) && S(of, "varfun") == "uniform";
            bool cellOut = opts.TryGetValue("OutputFormat", out var of2) && S(of2, "varfun") == "cell";
            string fn = FnName(f);
            if (opts.TryGetValue("GroupingVariables", out var gv))
            {
                var gvars = CellStrs(gv);
                var gkeys = Enumerable.Range(0, Height(t)).Select(i => string.Join("\u0001", gvars.Select(g => ElemString(t.Elems[0][g], i)))).ToList();
                var groups = gkeys.Select((k, i) => (k, i)).GroupBy(x => x.k).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
                var names = new List<string>(); var cols = new List<Value>();
                foreach (var g in gvars) { names.Add(g); cols.Add(TakeRows(t.Elems[0][g], groups.Select(q => q.First().i).ToArray())); }
                names.Add("GroupCount"); cols.Add(MatrixValue.Col(groups.Select(q => (double)q.Count())));
                foreach (var v in vars.Where(v => !gvars.Contains(v)))
                {
                    names.Add(fn + "_" + v);
                    cols.Add(Runtime.Concat.Vert(groups.Select(q => CallF(it, f, One(TakeRows(t.Elems[0][v], q.Select(x => x.i).ToArray())))).ToList()));
                }
                return One(MakeTable(names, cols));
            }
            var outs = vars.Select(v => CallF(it, f, One(t.Elems[0][v]))).ToList();
            if (uniform) return One(Runtime.Concat.Horz(outs));
            if (cellOut) return One(CellValue.RowOf(outs));
            return One(MakeTable(vars.Select(v => fn + "_" + v).ToList(), outs));
        });
        Reg("rowfun", (it, a, n) =>
        {
            var f = a[0]; var t = TableOf(a[1], "rowfun"); int h = Height(t);
            var (_, opts) = SplitOpts(a.Skip(2).ToArray(), "InputVariables", "OutputFormat", "OutputVariableNames");
            var vars = opts.TryGetValue("InputVariables", out var iv) ? CellStrs(iv) : t.Fields.ToList();
            var outs = Enumerable.Range(0, h).Select(i => CallF(it, f, vars.Select(v => { var c = t.Elems[0][v]; return c is CellValue cc ? cc.Data[i] : TakeRows(c, new[] { i }); }).ToArray())).ToList();
            var col = Runtime.Concat.Vert(outs);
            if (opts.TryGetValue("OutputFormat", out var of) && S(of, "rowfun") == "uniform") return One(col);
            return One(MakeTable(opts.TryGetValue("OutputVariableNames", out var on) ? CellStrs(on) : new() { "Var1" }, new() { col }));
        });
        // summary
        Reg0("summary", (it, a) =>
        {
            var sb = new StringBuilder();
            if (Is(a[0], "categorical"))
            {
                var c = (StructValue)a[0]; var cats = Cats(c); var codes = Inner(c);
                for (int k = 0; k < cats.Count; k++) sb.Append($"     {cats[k]}  {codes.Re.Count(x => x == k + 1)}\n");
                int u = codes.Re.Count(x => x == 0); if (u > 0) sb.Append($"     <undefined>  {u}\n");
                it.Out.Write(sb.ToString()); return;
            }
            var t = TableOf(a[0], "summary");
            sb.Append("\nVariables:\n\n");
            foreach (var f in t.Fields)
            {
                var v = t.Elems[0][f];
                sb.Append($"    {f}: {v.DimString} {v.ClassName}\n\n");
                if (v is MatrixValue m && !m.IsChar && m.Numel > 0)
                {
                    var xs = m.Re.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
                    if (xs.Length > 0)
                    {
                        double Med(double[] q) => q.Length % 2 == 1 ? q[q.Length / 2] : (q[q.Length / 2 - 1] + q[q.Length / 2]) / 2;
                        sb.Append($"        Values:\n\n            Min       {xs[0].ToString("G6", InvC)}\n            Median    {Med(xs).ToString("G6", InvC)}\n            Max       {xs[^1].ToString("G6", InvC)}\n");
                        int nan = m.Re.Length - xs.Length; if (nan > 0) sb.Append($"            NumMissing  {nan}\n");
                        sb.Append('\n');
                    }
                }
                else if (Is(v, "categorical"))
                {
                    var c = (StructValue)v; var cats = Cats(c); var codes = Inner(c);
                    for (int k = 0; k < cats.Count; k++) sb.Append($"        {cats[k]}  {codes.Re.Count(x => x == k + 1)}\n");
                    sb.Append('\n');
                }
            }
            it.Out.Write(sb.ToString());
        });

        // ------------------------------------------------------------ categorical
        Reg("categorical", (it, a, n) =>
        {
            var (pos, opts) = SplitOpts(a, "Ordinal", "Protected");
            bool ordinal = opts.TryGetValue("Ordinal", out var ov) && Ops.IsTrue(ov);
            var data = pos.Count > 0 ? pos[0] : new CellValue(0, 0);
            if (Is(data, "categorical")) return One(data);
            int[] dims = data is MatrixValue dm && dm.IsChar ? new[] { 1, 1 } : data.Dims;
            List<string> strs; List<string> valueset;
            if (data is MatrixValue nm && !nm.IsChar)
            {
                var vals = nm.Re.ToArray();
                var vs = pos.Count > 1 ? M(pos[1], "categorical").Re.ToArray() : vals.Where(x => !double.IsNaN(x)).Distinct().OrderBy(x => x).ToArray();
                valueset = pos.Count > 2 ? CellStrs(pos[2]) : vs.Select(x => N2S(MatrixValue.Scalar(x))).ToList();
                var codes = new MatrixValue(1, vals.Length);
                for (int i = 0; i < vals.Length; i++) codes.Re[i] = Array.IndexOf(vs, vals[i]) + 1;
                codes.SetDims(dims);
                return One(MakeCat(codes, valueset, ordinal));
            }
            strs = CellStrs(data);
            var vset = pos.Count > 1 ? CellStrs(pos[1]) : strs.Where(s => s != "").Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
            var names = pos.Count > 2 ? CellStrs(pos[2]) : vset;
            var c2 = new MatrixValue(1, strs.Count);
            for (int i = 0; i < strs.Count; i++) c2.Re[i] = vset.IndexOf(strs[i]) + 1;
            c2.SetDims(dims);
            return One(MakeCat(c2, names.Distinct().ToList(), ordinal));
        });
        Reg1("iscategorical", a => Bool(Is(a[0], "categorical")));
        Reg1("categories", a => TCol(Cats(Cat(a[0], "categories"))));
        Reg1("countcats", a =>
        {
            var c = Cat(a[0], "countcats"); var codes = Inner(c); var cats = Cats(c);
            var r = new MatrixValue(cats.Count, 1);
            foreach (var x in codes.Re) if (x > 0) r.Re[(int)x - 1]++;
            return codes.R == 1 ? Ops.Transpose(r, false) : r;
        });
        Reg1("isundefined", a => { var c = Cat(a[0], "isundefined"); return Ops.Map(Inner(c), x => x == 0 ? 1 : 0, null, null, NumKind.Logical); });
        Reg1("isordinal", a => Bool((bool)Cat(a[0], "isordinal").Meta!["ordinal"]));
        Reg1("addcats", a => { var c = Cat(a[0], "addcats").Copy(); var cats = new List<string>(Cats(c)); foreach (var s in CellStrs(a[1])) if (!cats.Contains(s)) cats.Add(s); c.Meta!["cats"] = cats; return c; });
        Reg1("renamecats", a =>
        {
            var c = Cat(a[0], "renamecats").Copy(); var cats = new List<string>(Cats(c));
            if (a.Length == 2) cats = CellStrs(a[1]);
            else { var o = CellStrs(a[1]); var nw = CellStrs(a[2]); for (int i = 0; i < o.Count; i++) cats[cats.IndexOf(o[i])] = nw[i]; }
            c.Meta!["cats"] = cats; return c;
        });
        Reg1("removecats", a =>
        {
            var c = Cat(a[0], "removecats").Copy(); var cats = Cats(c); var codes = Inner(c).Copy();
            var used = codes.Re.Where(x => x > 0).Select(x => (int)x - 1).ToHashSet();
            var keep = a.Length > 1 ? cats.Except(CellStrs(a[1])).ToList() : cats.Where((s, i) => used.Contains(i)).ToList();
            for (int i = 0; i < codes.Numel; i++) codes.Re[i] = codes.Re[i] > 0 ? keep.IndexOf(cats[(int)codes.Re[i] - 1]) + 1 : 0;
            c.Meta!["cats"] = keep; c.Meta["codes"] = codes; return c;
        });
        Reg1("mergecats", a =>
        {
            var c = Cat(a[0], "mergecats").Copy(); var cats = Cats(c); var codes = Inner(c).Copy();
            var olds = CellStrs(a[1]); var nw = a.Length > 2 ? S(a[2], "mergecats") : olds[0];
            var keep = cats.Where(s => !olds.Contains(s) || s == olds[0]).Select(s => s == olds[0] ? nw : s).ToList();
            for (int i = 0; i < codes.Numel; i++) if (codes.Re[i] > 0) { var s = cats[(int)codes.Re[i] - 1]; codes.Re[i] = keep.IndexOf(olds.Contains(s) ? nw : s) + 1; }
            c.Meta!["cats"] = keep; c.Meta["codes"] = codes; return c;
        });

        // ------------------------------------------------------------ datetime / duration
        Reg("datetime", (it, a, n) =>
        {
            var (pos, opts) = SplitOpts(a, "InputFormat", "Format", "ConvertFrom", "Locale", "TimeZone");
            string? fmt = opts.TryGetValue("Format", out var fv) ? S(fv, "datetime") : null;
            MatrixValue t;
            if (pos.Count == 0) t = MatrixValue.Scalar(ToDatenumD(DateTime.Now));
            else if (opts.TryGetValue("ConvertFrom", out var cf))
            {
                var kind = S(cf, "datetime").ToLowerInvariant(); var x = M(pos[0], "datetime").AsDouble();
                t = kind switch
                {
                    "datenum" => x,
                    "posixtime" => Ops.Map(x, v => v / 86400 + 719529),
                    "excel" => Ops.Map(x, v => v + 693960),
                    "juliandate" => Ops.Map(x, v => v - 1721058.5),
                    "yyyymmdd" => Ops.Map(x, v => ToDatenumD(new DateTime((int)(v / 10000), (int)(v / 100 % 100), (int)(v % 100)))),
                    _ => throw new OctaveException($"datetime: unsupported ConvertFrom '{kind}'")
                };
            }
            else if (pos[0] is MatrixValue { IsChar: true } || pos[0] is CellValue)
            {
                if (opts.TryGetValue("InputFormat", out var inf))
                {
                    var nf = S(inf, "datetime").Replace("SSS", "fff").Replace("'T'", "\\T");
                    var strs = CellStrs(pos[0]);
                    t = new MatrixValue(strs.Count, 1);
                    for (int i = 0; i < strs.Count; i++) t.Re[i] = DateTime.TryParseExact(strs[i], nf, InvC, DateTimeStyles.AllowWhiteSpaces, out var d) ? ToDatenumD(d) : double.NaN;
                    fmt ??= S(inf, "datetime");
                }
                else t = ToDatenum(pos[0]);
                if (pos[0] is CellValue pc) t.SetDims(pc.Dims);
            }
            else if (pos.Count >= 3)
            {
                var ps = pos.Select(p => M(p, "datetime")).ToList();
                if (ps.Count == 3 && ps[0].Numel == 1 && false) { }
                int cnt = ps.Max(p => p.Numel); var src = ps.First(p => p.Numel == cnt);
                t = new MatrixValue(src.R, src.C); t.ND = src.ND;
                double G(int k, int i) => k < ps.Count ? ps[k].Re[ps[k].Numel == 1 ? 0 : i] : 0;
                for (int i = 0; i < cnt; i++)
                {
                    var d = new DateTime((int)G(0, i), 1, 1).AddMonths((int)G(1, i) - 1).AddDays(G(2, i) - 1);
                    t.Re[i] = ToDatenumD(d) + (G(3, i) * 3600 + G(4, i) * 60 + G(5, i) + G(6, i) / 1000) / 86400;
                }
            }
            else if (pos[0] is MatrixValue dv && dv.Dims[^1] is 3 or 6)
            {
                int r = dv.R; t = new MatrixValue(r, 1);
                for (int i = 0; i < r; i++)
                {
                    var d = new DateTime((int)dv[i, 0], 1, 1).AddMonths((int)dv[i, 1] - 1).AddDays(dv[i, 2] - 1);
                    t.Re[i] = ToDatenumD(d) + (dv.C == 6 ? (dv[i, 3] * 3600 + dv[i, 4] * 60 + dv[i, 5]) / 86400 : 0);
                }
            }
            else if (Is(pos[0], "datetime")) t = Inner((StructValue)pos[0]);
            else throw new OctaveException("datetime: invalid input");
            return One(MakeDT(t, fmt));
        });
        Reg1("NaT", a => { var m = a.Length >= 2 ? new MatrixValue(I(a[0], "NaT"), I(a[1], "NaT")) : a.Length == 1 ? new MatrixValue(I(a[0], "NaT"), I(a[0], "NaT")) : new MatrixValue(1, 1); for (int i = 0; i < m.Numel; i++) m.Re[i] = double.NaN; return MakeDT(m); });
        Reg1("isdatetime", a => Bool(Is(a[0], "datetime")));
        Reg1("isduration", a => Bool(Is(a[0], "duration")));
        Reg1("isnat", a => Ops.Map(Inner(DT(a[0], "isnat")), x => double.IsNaN(x) ? 1 : 0, null, null, NumKind.Logical));
        Reg1("posixtime", a => Ops.Map(Inner(DT(a[0], "posixtime")), x => (x - 719529) * 86400));
        Reg1("juliandate", a => Ops.Map(Inner(DT(a[0], "juliandate")), x => x + 1721058.5));
        Reg1("exceltime", a => Ops.Map(Inner(DT(a[0], "exceltime")), x => x - 693960));
        Reg1("yyyymmdd", a => Ops.Map(Inner(DT(a[0], "yyyymmdd")), x => { var d = FromDatenum(x); return d.Year * 10000 + d.Month * 100 + d.Day; }));
        foreach (var (nm, prop) in new[] { ("year", "Year"), ("month", "Month"), ("day", "Day"), ("hour", "Hour"), ("minute", "Minute"), ("second", "Second") })
        {
            var p = prop;
            Reg1(nm, a =>
            {
                if (Is(a[0], "datetime")) { Dot((StructValue)a[0], p, out var v); return v; }
                if (Is(a[0], "duration"))
                {
                    var d = Inner((StructValue)a[0]);
                    return p switch { "Year" => Ops.Map(d, x => x / 365.2425), "Day" => d.Copy(), "Hour" => Ops.Map(d, x => x * 24), "Minute" => Ops.Map(d, x => x * 1440), "Second" => Ops.Map(d, x => x * 86400), _ => throw new OctaveException($"{nm}: not defined for durations") };
                }
                var m = ToDatenum(a[0]);
                return Ops.Map(m, x => { var d = FromDatenum(x); return p switch { "Year" => d.Year, "Month" => d.Month, "Day" => d.Day, "Hour" => d.Hour, "Minute" => d.Minute, _ => d.Second + d.Millisecond / 1000.0 }; });
            });
        }
        Reg1("quarter", a => Ops.Map(ToDatenum(a[0]), x => (FromDatenum(x).Month - 1) / 3 + 1));
        Reg1("ymd", a => { var m = ToDatenum(a[0]); var d = FromDatenum(m.Re[0]); return MatrixValue.Row(new double[] { d.Year, d.Month, d.Day }); });
        Reg("dateshift", (it, a, n) =>
        {
            var dt = DT(a[0], "dateshift"); var how = S(a[1], "dateshift"); var unit = S(a[2], "dateshift").ToLowerInvariant().TrimEnd('s');
            var r = Ops.Map(Inner(dt), x =>
            {
                var d = FromDatenum(x);
                DateTime s0 = unit switch { "year" => new DateTime(d.Year, 1, 1), "quarter" => new DateTime(d.Year, (d.Month - 1) / 3 * 3 + 1, 1), "month" => new DateTime(d.Year, d.Month, 1), "week" => d.Date.AddDays(-(int)d.DayOfWeek), "day" => d.Date, "hour" => d.Date.AddHours(d.Hour), "minute" => d.Date.AddHours(d.Hour).AddMinutes(d.Minute), _ => d };
                DateTime e0 = unit switch { "year" => s0.AddYears(1), "quarter" => s0.AddMonths(3), "month" => s0.AddMonths(1), "week" => s0.AddDays(7), "day" => s0.AddDays(1), "hour" => s0.AddHours(1), "minute" => s0.AddMinutes(1), _ => s0 };
                var res = how switch { "start" => s0, "end" => e0.AddTicks(-TimeSpan.TicksPerDay < 0 ? 0 : 0).AddDays(unit is "day" or "hour" or "minute" ? 0 : -1), _ => s0 };
                if (how == "end" && unit is "day" or "hour" or "minute") res = e0;
                return ToDatenumD(res);
            });
            return One(MakeDT(r, (string)dt.Meta!["fmt"]));
        });
        Reg1("between", a =>
        {
            var x = ToDatenum(a[0]); var y = ToDatenum(a[1]);
            return MakeDur((MatrixValue)Ops.Binary("-", y, x), "d");
        });
        Reg1("duration", a =>
        {
            var (pos, opts) = SplitOpts(a, "Format", "InputFormat");
            MatrixValue d;
            if (pos.Count == 1 && pos[0] is MatrixValue m && !m.IsChar && m.Dims[^1] == 3)
            { d = new MatrixValue(m.R, 1); for (int i = 0; i < m.R; i++) d.Re[i] = (m[i, 0] * 3600 + m[i, 1] * 60 + m[i, 2]) / 86400; }
            else if (pos.Count >= 1 && (pos[0] is MatrixValue { IsChar: true } || pos[0] is CellValue))
            {
                var strs = CellStrs(pos[0]); d = new MatrixValue(strs.Count, 1);
                for (int i = 0; i < strs.Count; i++) d.Re[i] = TimeSpan.TryParse(strs[i], InvC, out var ts) ? ts.TotalDays : double.NaN;
            }
            else
            {
                var ps = pos.Select(p => M(p, "duration")).ToList();
                int cnt = ps.Count == 0 ? 1 : ps.Max(p => p.Numel); var src = ps.Count == 0 ? MatrixValue.Scalar(0) : ps.First(p => p.Numel == cnt);
                d = new MatrixValue(src.R, src.C); d.ND = src.ND;
                double G(int k, int i) => k < ps.Count ? ps[k].Re[ps[k].Numel == 1 ? 0 : i] : 0;
                for (int i = 0; i < cnt; i++) d.Re[i] = (G(0, i) * 3600 + G(1, i) * 60 + G(2, i) + G(3, i) / 1000) / 86400;
            }
            return MakeDur(d, opts.TryGetValue("Format", out var f) ? S(f, "duration") : "hh:mm:ss");
        });
        foreach (var (nm, scale, fmt) in new[] { ("years", 365.2425, "y"), ("days", 1.0, "d"), ("hours", 1 / 24.0, "h"), ("minutes", 1 / 1440.0, "m"), ("seconds", 1 / 86400.0, "s"), ("milliseconds", 1 / 86400000.0, "s") })
        {
            double sc = scale; string ff = fmt; string fname = nm;
            var old = B.TryGetValue(nm, out var o0) ? o0 : null;
            Reg(nm, (it, a, n) =>
            {
                if (a.Length > 0 && Is(a[0], "duration")) return One(Ops.Map(Inner((StructValue)a[0]), x => x / sc));
                var m = a.Length == 0 ? MatrixValue.Scalar(1) : M(a[0], fname).AsDouble();
                return One(MakeDur(Ops.Map(m, x => x * sc), ff));
            });
        }
        Reg1("caldays", a => MakeDur(M(a[0], "caldays").AsDouble(), "d"));
        Reg1("calmonths", a => MakeDur(Ops.Map(M(a[0], "calmonths").AsDouble(), x => x * 30.436875), "d"));
        Reg1("calyears", a => MakeDur(Ops.Map(M(a[0], "calyears").AsDouble(), x => x * 365.2425), "y"));

        // ------------------------------------------------------------ wrap existing builtins for tagged values
        Wrap("isstruct", (it, a, n, old) => a.Length > 0 && TS(a[0]) != null ? One(Bool(false)) : old(it, a, n));
        Wrap("numel", (it, a, n, old) => a.Length == 1 && TS(a[0]) is StructValue s ? One(Num(s.Dims.Aggregate(1, (x, y) => x * y))) : old(it, a, n));
        Wrap("isempty", (it, a, n, old) => a.Length == 1 && TS(a[0]) is StructValue s ? One(Bool(s.Dims.Any(d => d == 0))) : old(it, a, n));
        Wrap("length", (it, a, n, old) => a.Length == 1 && TS(a[0]) is StructValue s ? One(Num(s.Dims.Any(d => d == 0) ? 0 : s.Dims.Max())) : old(it, a, n));
        Wrap("fieldnames", (it, a, n, old) => a.Length >= 1 && TS(a[0]) is StructValue s ? One(IsTable(s) ? TCol(s.Fields) : new CellValue(0, 1)) : old(it, a, n));
        Wrap("double", (it, a, n, old) =>
        {
            if (a.Length == 1 && Is(a[0], "categorical")) return One(Inner((StructValue)a[0]).Copy());
            if (a.Length == 1 && Is(a[0], "duration")) return One(Inner((StructValue)a[0]).Copy());
            return old(it, a, n);
        });
        Wrap("datenum", (it, a, n, old) => a.Length == 1 && Is(a[0], "datetime") ? One(Inner((StructValue)a[0]).Copy()) : old(it, a, n));
        Wrap("datestr", (it, a, n, old) => a.Length >= 1 && Is(a[0], "datetime") ? old(it, new[] { (Value)Inner((StructValue)a[0]) }.Concat(a.Skip(1)).ToArray(), n) : old(it, a, n));
        Wrap("datevec", (it, a, n, old) => a.Length >= 1 && Is(a[0], "datetime") ? old(it, new[] { (Value)Inner((StructValue)a[0]) }.Concat(a.Skip(1)).ToArray(), n) : old(it, a, n));
        Wrap("weekday", (it, a, n, old) => a.Length >= 1 && Is(a[0], "datetime") ? old(it, new[] { (Value)Inner((StructValue)a[0]) }.Concat(a.Skip(1)).ToArray(), n) : old(it, a, n));
        Wrap("char", (it, a, n, old) => a.Length == 1 && TS(a[0]) is StructValue s && !IsTable(s) ? One(CharMat(Strs(s))) : old(it, a, n));
        Wrap("cellstr", (it, a, n, old) => a.Length == 1 && TS(a[0]) is StructValue s && !IsTable(s) ? One(Reshape(CellRow(Strs(s)), s.Dims)) : old(it, a, n));
        Wrap("sort", (it, a, n, old) =>
        {
            if (a.Length >= 1 && TS(a[0]) is StructValue s && !IsTable(s))
            {
                var r = old(it, new[] { (Value)Inner(s) }.Concat(a.Skip(1)).ToArray(), Math.Max(n, 1));
                var o = new List<Value> { Rewrap(s, (MatrixValue)r[0]) };
                if (n > 1) o.Add(r[1]);
                return o.ToArray();
            }
            return old(it, a, n);
        });
        Wrap("unique", (it, a, n, old) =>
        {
            if (a.Length >= 1 && TS(a[0]) is StructValue s && !IsTable(s))
            {
                var inner = Inner(s);
                if (s.Tag == "categorical") inner = Ops.Map(inner, x => x == 0 ? double.NaN : x);
                var r = old(it, new[] { (Value)inner }.Concat(a.Skip(1)).ToArray(), Math.Max(n, 1));
                var u = (MatrixValue)r[0];
                if (s.Tag == "categorical") u = Ops.Map(u, x => double.IsNaN(x) ? 0 : x);
                var o = new List<Value> { Rewrap(s, u) }; o.AddRange(r.Skip(1));
                return o.ToArray();
            }
            return old(it, a, n);
        });
        foreach (var fn in new[] { "min", "max", "mean", "median" })
        {
            var f0 = fn;
            Wrap(fn, (it, a, n, old) =>
            {
                if (a.Length >= 1 && TS(a[0]) is StructValue s && s.Tag is "datetime" or "duration")
                {
                    var args = a.Select(x => x is StructValue q && q.Tag == s.Tag ? (Value)Inner(q) : x).ToArray();
                    var r = old(it, args, Math.Max(n, 1));
                    var o = new List<Value> { Rewrap(s, (MatrixValue)r[0]) }; o.AddRange(r.Skip(1));
                    return o.ToArray();
                }
                return old(it, a, n);
            });
        }
        Wrap("sum", (it, a, n, old) => a.Length >= 1 && Is(a[0], "duration") ? One(Rewrap((StructValue)a[0], (MatrixValue)old(it, new[] { (Value)Inner((StructValue)a[0]) }.Concat(a.Skip(1)).ToArray(), 1)[0])) : old(it, a, n));
        Wrap("diff", (it, a, n, old) =>
        {
            if (a.Length >= 1 && TS(a[0]) is StructValue s && s.Tag is "datetime" or "duration")
            {
                var r = (MatrixValue)old(it, new[] { (Value)Inner(s) }.Concat(a.Skip(1)).ToArray(), 1)[0];
                return One(s.Tag == "datetime" ? MakeDur(r) : Rewrap(s, r));
            }
            return old(it, a, n);
        });
        Wrap("ismember", (it, a, n, old) =>
        {
            if (a.Length >= 2 && (Is(a[0], "categorical") || Is(a[1], "categorical")))
                return old(it, new[] { (Value)CellRow(CellStrs(a[0])), CellRow(CellStrs(a[1])) }, n);
            if (a.Length >= 2 && (Is(a[0], "datetime") || Is(a[1], "datetime")))
                return old(it, new[] { (Value)ToDatenum(a[0]), ToDatenum(a[1]) }, n);
            return old(it, a, n);
        });
        Wrap("isequal", (it, a, n, old) =>
        {
            if (a.Length >= 2 && a.Any(v => TS(v) != null))
            {
                Value Norm(Value v) => v is StructValue s && s.Tag != null ? (IsTable(s) ? Runtime.Concat.Horz(new List<Value> { CellRow(s.Fields), CellValue.RowOf(s.Fields.Select(f => s.Elems[0][f])) }) : s.Tag == "categorical" ? CellRow(CellStrs(s)) : Inner(s)) : v;
                return old(it, a.Select(Norm).ToArray(), n);
            }
            return old(it, a, n);
        });
        Wrap("repmat", (it, a, n, old) => a.Length >= 2 && TS(a[0]) is StructValue s && !IsTable(s) ? One(Rewrap(s, (MatrixValue)old(it, new[] { (Value)Inner(s) }.Concat(a.Skip(1)).ToArray(), 1)[0])) : old(it, a, n));
        Wrap("reshape", (it, a, n, old) => a.Length >= 2 && TS(a[0]) is StructValue s && !IsTable(s) ? One(Rewrap(s, (MatrixValue)old(it, new[] { (Value)Inner(s) }.Concat(a.Skip(1)).ToArray(), 1)[0])) : old(it, a, n));
        Wrap("colon", (it, a, n, old) =>
        {
            if (a.Length >= 2 && Is(a[0], "datetime"))
            {
                var s = (StructValue)a[0]; double st = Inner(s).Re[0]; double en = ToDatenum(a[^1]).Re[0];
                double step = a.Length == 3 ? (Is(a[1], "duration") ? Inner((StructValue)a[1]).Re[0] : D(a[1], "colon")) : 1;
                var xs = new List<double>(); for (double x = st; x <= en + 1e-9; x += step) xs.Add(x);
                return One(Rewrap(s, MatrixValue.Row(xs)));
            }
            return old(it, a, n);
        });
    }

    static StructValue Cat(Value v, string fn) => Is(v, "categorical") ? (StructValue)v : throw new OctaveException($"{fn}: argument must be a categorical array");
    static StructValue DT(Value v, string fn) => Is(v, "datetime") ? (StructValue)v : throw new OctaveException($"{fn}: argument must be a datetime array");
    static MatrixValue Fill(int h, double v) { var m = new MatrixValue(h, 1); Array.Fill(m.Re, v); return m; }
    static StructValue Rewrap(StructValue s, MatrixValue inner)
    {
        var r = s.Copy();
        r.Meta![s.Tag switch { "categorical" => "codes", "datetime" => "t", _ => "d" }] = inner;
        return r;
    }
    static List<string> Strs(StructValue s) => Enumerable.Range(0, Inner(s).Numel).Select(k => ElemString(s, k)).ToList();
    static Value CharMat(List<string> rows)
    {
        if (rows.Count == 1) return MatrixValue.Str(rows[0]);
        int w = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
        var m = new MatrixValue(rows.Count, w, NumKind.Char);
        for (int i = 0; i < rows.Count; i++) for (int j = 0; j < w; j++) m.Re[i + j * rows.Count] = j < rows[i].Length ? rows[i][j] : ' ';
        return m;
    }
    static CellValue Reshape(CellValue c, int[] dims) { c.SetDims(dims); return c; }
    static void Wrap(string name, Func<Interpreter, Value[], int, BuiltinFn, Value[]> f)
    {
        if (!cur!.Builtins.TryGetValue(name, out var old)) old = (it, a, n) => throw new OctaveException($"'{name}' undefined");
        cur.Builtins[name] = (it, a, n) => f(it, a, n, old);
    }
    static Func<int, int, int> RowComparer(Value col, bool desc)
    {
        Func<int, int, int> c;
        if (col is CellValue cc) { var s = CellStrs(cc); c = (x, y) => string.CompareOrdinal(s[x], s[y]); }
        else if (col is StructValue ts && ts.Tag != null) { var m = Inner(ts); c = (x, y) => m.Re[x].CompareTo(m.Re[y]); }
        else if (col is MatrixValue m && m.IsChar) { c = (x, y) => string.CompareOrdinal(m.RowString(x), m.RowString(y)); }
        else
        {
            var mm = (MatrixValue)col; int h = mm.R;
            c = (x, y) => { for (int j = 0; j < Math.Max(1, mm.C); j++) { var r = mm.Re[x + j * h].CompareTo(mm.Re[y + j * h]); if (r != 0) return r; } return 0; };
        }
        return desc ? (x, y) => -c(x, y) : c;
    }
}
