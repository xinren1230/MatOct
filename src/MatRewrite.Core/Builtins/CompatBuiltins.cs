// MatRewrite - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Small core functions that GNU Octave implements in C++ and its m-file library relies on.</summary>
public static partial class BuiltinLibrary
{
    static Value[] CallCallable(Interpreter it, Value f, Value[] args, int nargout) => f switch
    {
        FunctionHandleValue fh => it.CallHandle(fh, args, nargout),
        _ when IsString(f) => it.CallFunction(S(f, "feval"), args, nargout),
        _ => throw new OctaveException("invalid function handle"),
    };

    static void RegisterCompat()
    {
        // isa (x, {"cls1", "cls2"}) returns a logical array
        var isa = cur!.Builtins["isa"];
        Reg("isa", (it, a, n) =>
        {
            NArgs(a, 2, 2, "isa");
            if (a[1] is CellValue cc)
            {
                var r = new MatrixValue(cc.R, cc.C, NumKind.Logical);
                for (int k = 0; k < cc.Data.Length; k++) r.Re[k] = ((MatrixValue)isa(it, new[] { a[0], cc.Data[k] }, 1)[0]).Re[0];
                return Out(r);
            }
            return isa(it, a, n);
        });
        // [f, e] = log2 (x)
        var log2 = cur.Builtins["log2"];
        Reg("log2", (it, a, n) =>
        {
            if (n < 2) return log2(it, a, n);
            var x = M(a[0], "log2");
            var f = new MatrixValue(x.R, x.C) { ND = x.ND }; var e = new MatrixValue(x.R, x.C) { ND = x.ND };
            for (int k = 0; k < x.Numel; k++)
            {
                double v = x.Re[k];
                if (v == 0 || double.IsNaN(v) || double.IsInfinity(v)) { f.Re[k] = v; e.Re[k] = 0; continue; }
                int ex = (int)Math.Floor(Math.Log2(Math.Abs(v))) + 1;
                double fr = v / Math.Pow(2, ex);
                if (Math.Abs(fr) >= 1) { fr /= 2; ex++; } else if (Math.Abs(fr) < 0.5) { fr *= 2; ex--; }
                f.Re[k] = fr; e.Re[k] = ex;
            }
            return Out(f, e);
        });
        Reg("ishghandle", (it, a, n) => it.Builtins["ishandle"](it, a, n));
        Reg("isgraphics", (it, a, n) => it.Builtins["ishandle"](it, a, n));
        BuiltinFn merge = (it, a, n) =>
        {
            NArgs(a, 3, 3, "merge");
            var m = M(a[0], "merge");
            if (m.Numel == 1) return Out(m.Re[0] != 0 ? a[1] : a[2]);
            var t = M(a[1], "merge"); var f = M(a[2], "merge");
            var r = new MatrixValue(m.R, m.C, t.Kind) { ND = m.ND };
            for (int k = 0; k < m.Numel; k++)
            {
                var src = m.Re[k] != 0 ? t : f;
                r.Re[k] = src.Numel == 1 ? src.Re[0] : src.Re[k];
            }
            return Out(r);
        };
        Reg("merge", merge); Reg("ifelse", merge);
        Reg1("typeinfo", a => Str(a.Length == 0 ? "" : a[0] switch
        {
            CellValue => "cell",
            StructValue s => s.Numel == 1 ? "scalar struct" : "struct",
            FunctionHandleValue => "function handle",
            MatrixValue m when m.IsChar => m.Numel == 1 ? "char string" : "string",
            MatrixValue m when m.IsLogical => m.Numel == 1 ? "bool" : "bool matrix",
            MatrixValue m when m.IsInteger => m.ClassName + (m.Numel == 1 ? " scalar" : " matrix"),
            MatrixValue m when m.IsComplex => m.Numel == 1 ? "complex scalar" : "complex matrix",
            MatrixValue m => m.Numel == 1 ? "scalar" : "matrix",
            var v => v.ClassName,
        }));
        Reg1("size_equal", a =>
        {
            for (int k = 1; k < a.Length; k++)
                if (!a[0].Dims.SequenceEqual(a[k].Dims)) return Bool(false);
            return Bool(true);
        });
        Reg0("mlock", (it, a) => { }); Reg0("munlock", (it, a) => { });
        Reg1("mislocked", a => Bool(false));
        Reg("evalin", (it, a, n) =>
        {
            NArgs(a, 2, 3, "evalin");
            var ctx = S(a[0], "evalin");
            var target = ctx == "base" ? it.Base : ctx == "caller" ? (it.CallStack.Count >= 2 ? it.CallStack[^2] : it.Base)
                : throw new OctaveException("evalin: CONTEXT must be \"caller\" or \"base\"");
            var save = it.Current; it.Current = target;
            try { return it.Builtins["eval"](it, a.Skip(1).ToArray(), n); }
            finally { it.Current = save; }
        });
        Reg0("assignin", (it, a) =>
        {
            NArgs(a, 3, 3, "assignin");
            var ctx = S(a[0], "assignin");
            var target = ctx == "base" ? it.Base : ctx == "caller" ? (it.CallStack.Count >= 2 ? it.CallStack[^2] : it.Base)
                : throw new OctaveException("assignin: CONTEXT must be \"caller\" or \"base\"");
            target.Vars[S(a[1], "assignin")] = a[2];
        });
        // idx = lookup (table, y): index i such that table(i) <= y < table(i+1)
        Reg1("lookup", a =>
        {
            NArgs(a, 2, 3, "lookup");
            var opt = a.Length > 2 ? S(a[2], "lookup") : "";
            if (a[0] is CellValue tc)
            {
                var tbl = tc.Data.Select(v => S(v, "lookup")).ToArray();
                var ys = a[1] is CellValue yc ? yc.Data.Select(v => S(v, "lookup")).ToArray() : new[] { S(a[1], "lookup") };
                var r = new MatrixValue(a[1] is CellValue yc2 ? yc2.R : 1, a[1] is CellValue yc3 ? yc3.C : 1);
                for (int k = 0; k < ys.Length; k++)
                {
                    if (opt.Contains('m') || opt.Contains('b')) { int i = Array.IndexOf(tbl, ys[k]); r.Re[k] = opt.Contains('b') ? (i >= 0 ? 1 : 0) : i + 1; }
                    else { int c = 0; while (c < tbl.Length && string.CompareOrdinal(tbl[c], ys[k]) <= 0) c++; r.Re[k] = c; }
                }
                if (opt.Contains('b')) r.Kind = NumKind.Logical;
                return r;
            }
            var t = M(a[0], "lookup"); var y = M(a[1], "lookup");
            if ((t.Kind == NumKind.Single) != (y.Kind == NumKind.Single)) { y = y.Copy(); for (int q = 0; q < y.Re.Length; q++) y.Re[q] = (float)y.Re[q]; if (t.Kind != NumKind.Single) { t = t.Copy(); for (int q = 0; q < t.Re.Length; q++) t.Re[q] = (float)t.Re[q]; } }
            int n = t.Numel; bool desc = n > 1 && t.Re[0] > t.Re[n - 1];
            var res = new MatrixValue(y.R, y.C) { ND = y.ND };
            for (int k = 0; k < y.Numel; k++)
            {
                double v = y.Re[k]; int lo = 0, hi = n;
                while (lo < hi) { int mid = (lo + hi) / 2; bool le = desc ? t.Re[mid] >= v : t.Re[mid] <= v; if (le) lo = mid + 1; else hi = mid; }
                int idx = lo;
                if (opt.Contains('l') && idx < 1) idx = 1;
                if (opt.Contains('r') && idx > n - 1) idx = n - 1;
                if (opt.Contains('m')) res.Re[k] = idx > 0 && t.Re[idx - 1] == v ? idx : 0;
                else if (opt.Contains('b')) res.Re[k] = idx > 0 && t.Re[idx - 1] == v ? 1 : 0;
                else res.Re[k] = idx;
            }
            if (opt.Contains('b')) res.Kind = NumKind.Logical;
            return res;
        });
        RegI("cellslices", (it, a) =>
        {
            NArgs(a, 3, 4, "cellslices");
            var x = a[0]; var lb = M(a[1], "cellslices"); var ub = M(a[2], "cellslices");
            int dim = a.Length > 3 ? I(a[3], "cellslices") : (x.Dims[0] == 1 ? 2 : 1);
            var outv = new Value[lb.Numel];
            for (int k = 0; k < lb.Numel; k++)
            {
                var rng = MatrixValue.Row(Enumerable.Range((int)lb.Re[k], Math.Max(0, (int)ub.Re[k] - (int)lb.Re[k] + 1)).Select(i => (double)i));
                var idx = dim == 1 ? new Value[] { rng, MatrixValue.Str(":") } : new Value[] { MatrixValue.Str(":"), rng };
                outv[k] = it.CallFunction("subsref_paren__", new[] { x }.Concat(idx).ToArray(), 1)[0];
            }
            return new CellValue(1, outv.Length, outv);
        });
        Reg("subsref_paren__", (it, a, n) =>
        {
            var save = it.Current; var sc = new Scope();
            sc.Vars["x__"] = a[0];
            var parts = new List<string>();
            for (int k = 1; k < a.Length; k++)
            {
                if (IsString(a[k]) && S(a[k], "subsref") == ":") { parts.Add(":"); continue; }
                sc.Vars[$"i{k}__"] = a[k]; parts.Add($"i{k}__");
            }
            it.Current = sc;
            try
            {
                it.EvalInCurrentScope($"r__ = x__({string.Join(", ", parts)});");
                return Out(sc.Vars["r__"]);
            }
            finally { it.Current = save; }
        });
        Reg1("issorted", a =>
        {
            NArgs(a, 1, 3, "issorted");
            string mode = a.Length > 1 && IsString(a[^1]) ? S(a[^1], "issorted").ToLowerInvariant() : "ascend";
            if (a[0] is CellValue c)
            {
                var s = c.Data.Select(v => S(v, "issorted")).ToArray();
                for (int k = 1; k < s.Length; k++)
                {
                    int cmp = string.CompareOrdinal(s[k - 1], s[k]);
                    if (mode.StartsWith("a") ? cmp > 0 : mode.StartsWith("d") ? cmp < 0 : false) return Bool(false);
                }
                return Bool(true);
            }
            var m = M(a[0], "issorted");
            if (a.Length > 1 && IsString(a[1]) && S(a[1], "issorted") == "rows")
            {
                for (int i = 1; i < m.R; i++)
                    for (int j = 0; j < m.C; j++)
                    {
                        double p = m.Re[i - 1 + j * m.R], q = m.Re[i + j * m.R];
                        if (p < q) break; if (p > q) return Bool(false);
                    }
                return Bool(true);
            }
            if (m.R > 1 && m.C > 1) throw new OctaveException("issorted: needs a vector");
            bool asc = true, desc = true;
            for (int k = 1; k < m.Numel; k++)
            {
                double p = m.Re[k - 1], q = m.Re[k];
                if (double.IsNaN(q) && !double.IsNaN(p)) { desc = false; continue; }
                if (double.IsNaN(p) && !double.IsNaN(q)) { asc = false; continue; }
                if (p > q) asc = false; if (p < q) desc = false;
            }
            return Bool(mode.StartsWith("d") ? desc : mode.StartsWith("e") || mode.StartsWith("m") ? asc || desc : asc);
        });
        Reg1("isindex", a =>
        {
            NArgs(a, 1, 2, "isindex");
            double max = a.Length > 1 ? D(a[1], "isindex") : double.PositiveInfinity;
            if (IsString(a[0])) return Bool(S(a[0], "isindex") == ":");
            if (a[0] is not MatrixValue m) return Bool(false);
            if (m.IsLogical) return Bool(m.Numel == 0 || !(max < double.PositiveInfinity) || LastTrue(m) <= max);
            if (m.IsComplex) return Bool(false);
            foreach (var v in m.Re) if (!(v >= 1 && v == Math.Floor(v) && v <= max)) return Bool(false);
            return Bool(true);
            static int LastTrue(MatrixValue m) { for (int k = m.Numel - 1; k >= 0; k--) if (m.Re[k] != 0) return k + 1; return 0; }
        });
        Reg1("signbit", a =>
        {
            var m = M(a[0], "signbit");
            var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = m.ND };
            for (int k = 0; k < m.Numel; k++) r.Re[k] = double.IsNegative(m.Re[k]) ? 1 : 0;
            return r;
        });
        void CharClass(string name, Func<char, bool> pred)
        {
            Value One(Value v)
            {
                if (v is CellValue c) return new CellValue(c.R, c.C, c.Data.Select(One).ToArray());
                if (v is not MatrixValue m || !m.IsChar) { var d = v.Dims; return new MatrixValue(d[0], d.Length > 1 ? d[1] : 1, NumKind.Logical); }
                var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = m.ND };
                for (int k = 0; k < m.Numel; k++) r.Re[k] = pred((char)m.Re[k]) ? 1 : 0;
                return r;
            }
            Reg1(name, a => One(a[0]));
        }
        foreach (var (nm, pr) in new (string, Func<char, bool>)[] {
            ("isalnum", ch => ch < 128 && char.IsLetterOrDigit(ch)), ("isxdigit", Uri.IsHexDigit),
            ("ispunct", ch => ch > 32 && ch < 127 && !char.IsLetterOrDigit(ch)), ("iscntrl", ch => ch < 32 || ch == 127),
            ("isgraph", ch => ch > 32 && ch < 127), ("isprint", ch => ch >= 32 && ch < 127), ("isascii", ch => ch < 128) })
            if (!cur.Builtins.ContainsKey(nm)) CharClass(nm, pr);
        Reg("bsxfun", (it, a, n) =>
        {
            NArgs(a, 3, 3, "bsxfun");
            return CallCallable(it, a[0], new[] { a[1], a[2] }, Math.Max(1, n));
        });
        Reg("unlink", (it, a, n) =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to unlink");
            var f = S(a[0], "unlink");
            string msg = "";
            try { if (File.Exists(f)) File.Delete(f); else msg = "No such file or directory"; } catch (Exception e) { msg = e.Message; }
            if (n == 0 && msg.Length > 0 && false) throw new OctaveException("unlink: operation failed: " + msg);
            return new Value[] { Num(msg.Length > 0 ? -1 : 0), Str(msg) };
        });
        Reg("isargout", (it, a, n) =>
        {
            var m = M(a[0], "isargout");
            int no = Math.Max(1, it.Current.Nargout);
            var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = (int[]?)m.ND?.Clone() };
            for (int k = 0; k < m.Numel; k++) r.Re[k] = m.Re[k] >= 1 && m.Re[k] <= no ? 1 : 0;
            return Out(r);
        });
        // time structures
        static Value TmStruct(DateTime t, bool utc)
        {
            var s = new StructValue();
            void F(string k, double v) { s.Fields.Add(k); s.Elems[0][k] = Num(v); }
            F("usec", (t.Ticks % TimeSpan.TicksPerSecond) / 10.0); F("sec", t.Second); F("min", t.Minute); F("hour", t.Hour);
            F("mday", t.Day); F("mon", t.Month - 1); F("year", t.Year - 1900); F("wday", (int)t.DayOfWeek); F("yday", t.DayOfYear - 1);
            F("isdst", !utc && TimeZoneInfo.Local.IsDaylightSavingTime(t) ? 1 : 0);
            F("gmtoff", utc ? 0 : TimeZoneInfo.Local.GetUtcOffset(t).TotalSeconds);
            s.Fields.Add("zone"); s.Elems[0]["zone"] = Str(utc ? "GMT" : TimeZoneInfo.Local.StandardName);
            return s;
        }
        static DateTime FromEpoch(double secs) => DateTime.UnixEpoch.AddTicks((long)Math.Round(secs * TimeSpan.TicksPerSecond));
        Reg1("localtime", a => TmStruct(FromEpoch(D(a[0], "localtime")).ToLocalTime(), false));
        Reg1("gmtime", a => TmStruct(FromEpoch(D(a[0], "gmtime")), true));
        Reg1("mktime", a =>
        {
            if (a[0] is not StructValue s) throw new OctaveException("mktime: TM_STRUCT argument must be a structure");
            double G(string k) => s.Elems[0].TryGetValue(k, out var v) ? D(v, "mktime") : 0;
            var t = new DateTime((int)G("year") + 1900, 1, 1, 0, 0, 0, DateTimeKind.Local)
                .AddMonths((int)G("mon")).AddDays(G("mday") - 1).AddHours(G("hour")).AddMinutes(G("min")).AddSeconds(G("sec") + G("usec") / 1e6);
            return Num((t.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
        });
        Reg1("strftime", a =>
        {
            var fmt = S(a[0], "strftime");
            if (a[1] is not StructValue s) throw new OctaveException("strftime: TM_STRUCT must be a structure");
            double G(string k) => s.Elems[0].TryGetValue(k, out var v) ? D(v, "strftime") : 0;
            var t = new DateTime((int)G("year") + 1900, 1, 1).AddMonths((int)G("mon")).AddDays(G("mday") - 1).AddHours(G("hour")).AddMinutes(G("min")).AddSeconds(G("sec"));
            var sb = new System.Text.StringBuilder();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < fmt.Length; i++)
            {
                if (fmt[i] != '%' || i + 1 >= fmt.Length) { sb.Append(fmt[i]); continue; }
                char c = fmt[++i];
                if (c == '-' && i + 1 < fmt.Length) c = fmt[++i];
                sb.Append(c switch
                {
                    'Y' => t.Year.ToString(), 'y' => (t.Year % 100).ToString("00"), 'm' => t.Month.ToString("00"), 'd' => t.Day.ToString("00"),
                    'e' => t.Day.ToString().PadLeft(2), 'H' => t.Hour.ToString("00"), 'I' => ((t.Hour + 11) % 12 + 1).ToString("00"),
                    'M' => t.Minute.ToString("00"), 'S' => t.Second.ToString("00"), 'p' => t.Hour < 12 ? "AM" : "PM",
                    'b' or 'h' => t.ToString("MMM", ci), 'B' => t.ToString("MMMM", ci), 'a' => t.ToString("ddd", ci), 'A' => t.ToString("dddd", ci),
                    'j' => t.DayOfYear.ToString("000"), 'D' => t.ToString("MM/dd/yy", ci), 'T' => t.ToString("HH:mm:ss", ci),
                    'R' => t.ToString("HH:mm", ci), 'n' => "\n", 't' => "\t", '%' => "%", 'Z' => "", 'z' => "+0000",
                    'C' => (t.Year / 100).ToString("00"), 'u' => (((int)t.DayOfWeek + 6) % 7 + 1).ToString(), 'w' => ((int)t.DayOfWeek).ToString(),
                    'F' => t.ToString("yyyy-MM-dd", ci), 'c' => t.ToString("ddd MMM dd HH:mm:ss yyyy", ci), 'x' => t.ToString("MM/dd/yy", ci), 'X' => t.ToString("HH:mm:ss", ci),
                    _ => "%" + c,
                });
            }
            return Str(sb.ToString());
        });
    }
}

public static partial class BuiltinLibrary
{
    static void RegisterCompat2()
    {
        // [m, im] = cummax/cummin (x, dim)
        foreach (var (name, better) in new (string, Func<double, double, bool>)[] { ("cummax", (a, b) => a > b), ("cummin", (a, b) => a < b) })
        {
            Reg(name, (it, a, n) =>
            {
                NArgs(a, 1, 2, name);
                var x = M(a[0], name);
                var d = x.Dims;
                int dim = a.Length > 1 ? I(a[1], name) : Array.FindIndex(d, z => z != 1) + 1;
                if (dim <= 0) dim = 1;
                int len = dim <= d.Length ? d[dim - 1] : 1;
                int stride = 1; for (int k = 0; k < dim - 1 && k < d.Length; k++) stride *= d[k];
                var m = new MatrixValue(x.R, x.C, x.Kind) { ND = x.ND }; var im = new MatrixValue(x.R, x.C) { ND = x.ND };
                int total = x.Numel; if (len == 0) return Out(m, im);
                int outer = total / (len * stride);
                for (int o = 0; o < outer; o++)
                    for (int s = 0; s < stride; s++)
                    {
                        double best = double.NaN; int bi = 0;
                        for (int k = 0; k < len; k++)
                        {
                            int idx = o * len * stride + k * stride + s;
                            double v = x.Re[idx];
                            if (!double.IsNaN(v) && (double.IsNaN(best) || better(v, best) || v == best)) { best = v; bi = k + 1; }
                            if (bi == 0) { m.Re[idx] = double.NaN; im.Re[idx] = 1; continue; }
                            m.Re[idx] = best; im.Re[idx] = bi;
                        }
                    }
                return Out(m, im);
            });
        }
        Reg1("isna", a => { var d = a[0].Dims; var r = new MatrixValue(1, NDArray.Prod(d), NumKind.Logical); r.SetDims(d); return r; });
        // index permutation used by Octave's sortrows.m
        RegI("__sort_rows_idx__", (it, a) =>
        {
            var x = M(a[0], "__sort_rows_idx__");
            bool desc = a.Length > 1 && S(a[1], "__sort_rows_idx__").StartsWith("d", StringComparison.OrdinalIgnoreCase);
            var idx = Enumerable.Range(0, x.R).ToArray();
            Array.Sort(idx, (p, q) =>
            {
                for (int j = 0; j < x.C; j++)
                {
                    int c = x.Re[p + j * x.R].CompareTo(x.Re[q + j * x.R]);
                    if (c != 0) return desc ? -c : c;
                }
                return p.CompareTo(q);
            });
            // stable sort
            idx = idx.Select((v, k) => (v, k)).OrderBy(t => 0).Select(t => t.v).ToArray();
            return new MatrixValue(x.R, 1, idx.Select(i => i + 1.0).ToArray());
        });
    }
}
