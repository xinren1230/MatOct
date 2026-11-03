using MatRewrite.Core.Runtime;
using MatRewrite.Core.Graphics;

namespace MatRewrite.Core.Builtins;

/// <summary>Octave-compatible input validation layered over existing builtins (0.18).</summary>
public static partial class BuiltinLibrary
{
    static void Pre(string name, Action<Interpreter, Value[], int> check)
    {
        if (!cur!.Builtins.TryGetValue(name, out var f)) return;
        cur!.Builtins[name] = (it, a, n) => { check(it, a, n); return f(it, a, n); };
    }
    static void Remap(string name, Func<string, string?> map)
    {
        if (!cur!.Builtins.TryGetValue(name, out var f)) return;
        cur!.Builtins[name] = (it, a, n) =>
        {
            try { return f(it, a, n); }
            catch (OctaveException e) when (map(e.Message) is string m) { throw new OctaveException(m); }
        };
    }
    static bool VNum(Value v) => v is MatrixValue m && !m.IsChar || v is Sparse.SparseValue;
    static bool IsND(Value v) => v is ArrayValue av && av.ND != null && av.ND.Length > 2 && av.ND.Skip(2).Any(d => d != 1);
    static bool LineSpecOk(string s) => s.Length > 0 && s.All(ch => "-:.ox+*sdv^<>phrgbcmykw".Contains(ch));

    static void RegisterValidation()
    {
        RegisterPhase18();
        foreach (var bn in new[] { "bitand", "bitor", "bitxor", "bitshift" })
            if (cur!.Builtins.TryGetValue(bn, out var bf))
                cur!.Builtins[bn] = (it, a, n) =>
                {
                    var r = bf(it, a, n);
                    if (a.Any(v => v is MatrixValue m && m.Kind == NumKind.Single) && !a.Any(v => v is MatrixValue m && m.IsInteger) && r.Length > 0 && r[0] is MatrixValue rm && rm.Kind == NumKind.Double)
                    { rm = rm.Copy(); rm.Kind = NumKind.Single; r[0] = rm; }
                    else if (r.Length > 0 && r[0] is MatrixValue rd && rd.Kind == NumKind.Double && (bn == "bitshift" ? a.Take(1) : a).FirstOrDefault(v => v is MatrixValue m && m.IsInteger) is MatrixValue im)
                    {
                        string tn = NumTypes.Name(im.Kind);
                        rd = rd.Copy();
                        if (tn.StartsWith("uint") && tn != "uint64") { double mod = Math.Pow(2, int.Parse(tn.Substring(4))); for (int i = 0; i < rd.Re.Length; i++) { double v = rd.Re[i] % mod; rd.Re[i] = v < 0 ? v + mod : v; } }
                        r[0] = Fc(it, tn, rd);
                    }
                    return r;
                };
        Pre("toeplitz", (it, a, n) =>
        {
            static bool Vec(Value v) => v.Numel > 0 && v.Dims.Count(d => d != 1) <= 1;
            if (a.Length > 2) throw new OctaveException("toeplitz: function called with too many inputs", "Octave:invalid-fun-call");
            if (a.Length == 1 && !Vec(a[0])) throw new OctaveException("toeplitz: C must be a vector");
            if (a.Length == 2 && (!Vec(a[0]) || !Vec(a[1]))) throw new OctaveException("toeplitz: C and R must be vectors");
        });
        if (cur!.Builtins.TryGetValue("meansq", out var msqf))
            cur!.Builtins["meansq"] = (it, a, n) =>
            {
                var r = msqf(it, a, n);
                if (a.Length > 0 && a[0] is MatrixValue m0 && m0.Kind == NumKind.Single && r.Length > 0 && r[0] is MatrixValue rm && rm.Kind == NumKind.Double) r[0] = Fc(it, "single", rm);
                return r;
            };
        cur!.Builtins["dir_encoding"] = (it, a, n) => a.Length is < 1 or > 2 ? throw new OctaveException("Invalid call to dir_encoding") : new Value[] { MatrixValue.Str("utf-8") };
        cur!.Builtins["is_same_file"] = (it, a, n) =>
        {
            if (a.Length != 2) throw new OctaveException("Invalid call to is_same_file");
            static string[]? Names(Value v) => v is CellValue c ? c.Data.Select(x => x is MatrixValue m && m.IsChar ? m.AsString() : null).ToArray()! : (v is MatrixValue m2 && (m2.IsChar || m2.Numel == 0)) ? new[] { m2.Numel == 0 ? "" : m2.AsString() } : null;
            static string Canon(string p) { try { if (p == "" || !(File.Exists(p) || Directory.Exists(p))) return "\0" + p; return Path.GetFullPath(p).TrimEnd('/', '\\'); } catch { return "\0" + p; } }
            var x = Names(a[0]); var y = Names(a[1]);
            if (x == null || y == null || x.Contains(null) || y.Contains(null)) throw new OctaveException("is_same_file: FILEPATH1 and FILEPATH2 must be strings or cell arrays of strings");
            if (a[0] is CellValue && a[1] is CellValue && x.Length != y.Length) throw new OctaveException("is_same_file: cellstr arrays FILEPATH1 and FILEPATH2 must be the same size");
            bool Eq(string p, string q) { var cp = Canon(p); return !cp.StartsWith("\0") && cp == Canon(q); }
            int len = Math.Max(x.Length, y.Length);
            var res = new MatrixValue(1, len, NumKind.Logical);
            for (int i = 0; i < len; i++) res.Re[i] = Eq(x[x.Length == 1 ? 0 : i], y[y.Length == 1 ? 0 : i]) ? 1 : 0;
            if (a[0] is CellValue c0) res.SetDims(c0.Dims); else if (a[1] is CellValue c1) res.SetDims(c1.Dims);
            return new Value[] { res };
        };
        Pre("stem", (it, a, n) =>
        {
            int k = 0;
            if (a.Length > 1 && VNum(a[0]) && a[0].Numel == 1 && it.Graphics.Lookup(D(a[0], "stem")) is Axes) k = 1;
            var nums = new List<Value>();
            for (; k < a.Length && !IsString(a[k]); k++) nums.Add(a[k]);
            if (nums.Count >= 3) throw new OctaveException("stem: can not define Z for 2-D stem plot");
            if (nums.Any(v => !VNum(v))) throw new OctaveException("stem: X and Y must be numeric");
            if (nums.Any(IsND)) throw new OctaveException("stem: Y must be a vector or 2-D array");
            if (nums.Count == 2)
            {
                var x = nums[0]; var y = nums[1];
                bool xv = x.Rows == 1 || x.Cols == 1, yv = y.Rows == 1 || y.Cols == 1;
                bool ok = xv && yv ? x.Numel == y.Numel : xv ? x.Numel == y.Rows || x.Numel == y.Cols : x.Rows == y.Rows && x.Cols == y.Cols;
                if (!ok) throw new OctaveException("stem: inconsistent sizes for X and Y");
            }
            var rest = a.Skip(k).ToList();
            if (rest.Count % 2 == 1 && IsString(rest[0]))
            {
                var s0 = S(rest[0], "stem");
                if (!LineSpecOk(s0) && !s0.Equals("filled", StringComparison.OrdinalIgnoreCase) && rest.Count == 1)
                    throw new OctaveException($"stem: No value specified for property \"{s0}\"");
            }
        });
        Remap("patch", m => m == "patch: invalid color specification" ? "patch: invalid color specification C" : null);
        Pre("patch", (it, a, n) =>
        {
            if (a.Length == 3 && VNum(a[0]) && VNum(a[1]))
            {
                if (a[2] is CellValue) throw new OctaveException("patch: invalid color specification C");
                if (IsString(a[2]) && !LineSpecOk(S(a[2], "patch")) && !(S(a[2], "patch").ToLowerInvariant() is "black" or "white" or "red" or "green" or "blue" or "cyan" or "magenta" or "yellow")) throw new OctaveException("patch: invalid color specification C");
                if (VNum(a[2]) && !IsND(a[2]) && (a[0].Rows == 1 || a[0].Cols == 1))
                {
                    int nc = a[2].Numel, nx = a[0].Numel;
                    if (nc != 1 && nc != nx && !(nc == 3 && a[2].Rows == 1)) throw new OctaveException("patch: size of X, Y, and C must be equal");
                }
            }
        });
        Pre("saveas", (it, a, n) =>
        {
            if (a.Length < 2 || a.Length > 3) throw new OctaveException("Invalid call to saveas");
            if (!VNum(a[0]) || a[0].Numel != 1 || it.Graphics.Lookup(D(a[0], "saveas")) == null) throw new OctaveException("saveas: H must be a graphics handle");
            if (!IsString(a[1])) throw new OctaveException("saveas: FILENAME must be a string");
            if (a.Length > 2 && !IsString(a[2])) throw new OctaveException("saveas: FMT must be a string");
        });
        Pre("savefig", (it, a, n) =>
        {
            if (a.Length > 0 && !IsString(a[0]) && !VNum(a[0])) throw new OctaveException("savefig: first argument must be a figure handle or filename");
            if (a.Length > 0 && VNum(a[0]) && M(a[0], "savefig").Re.Any(h => it.Graphics.Lookup(h) is not Figure)) throw new OctaveException("savefig: H must be a valid figure handle");
            if (a.Length > 1 && !IsString(a[1])) throw new OctaveException("savefig: FILENAME must be a string");
        });
        Pre("hgsave", (it, a, n) =>
        {
            if (a.Length == 0) throw new OctaveException("Invalid call to hgsave");
            if (a.Length == 1 && IsString(a[0]) && it.Graphics.Figures.Count == 0) throw new OctaveException("hgsave: no current figure to save");
            if (VNum(a[0]) && M(a[0], "hgsave").Re.Any(h => it.Graphics.Lookup(h) == null || h == 0)) throw new OctaveException("hgsave: H must be a valid graphics handle");
        });
        Remap("openfig", m => m.StartsWith("openfig: unable to find file") ? m.Replace("unable to find file", "cannot find file") : null);
        Pre("figure", (it, a, n) =>
        {
            if (a.Length >= 1 && VNum(a[0]) && a[0].Numel == 1)
            {
                double h = D(a[0], "figure");
                if (it.Graphics.Lookup(h) is not Figure && (h <= 0 || h != Math.Floor(h))) throw new OctaveException("figure: N must be figure handle or figure number");
            }
        });
        foreach (var nm in new[] { "campos", "camtarget", "camup", "camva" })
            Pre(nm, (it, a, n) =>
            {
                int k = a.Length > 0 && VNum(a[0]) && a[0].Numel == 1 && it.Graphics.Lookup(D(a[0], nm)) is Axes ? 1 : 0;
                if (k < a.Length && IsString(a[k]) && S(a[k], nm).ToLowerInvariant() is not ("auto" or "manual")) throw new OctaveException($"{nm}: invalid value");
            });
        Pre("axis", (it, a, n) =>
        {
            int k = a.Length > 0 && VNum(a[0]) && a[0].Numel == 1 && it.Graphics.Lookup(D(a[0], "axis")) is Axes ? 1 : 0;
            if (k < a.Length && a[k] is CellValue) throw new OctaveException("axis: expecting no args, or a numeric vector with 2, 4, 6, or 8 elements");
            if (k < a.Length && a[k] is MatrixValue lm && !lm.IsChar && lm.Numel > 1)
            {
                if (lm.Numel is not (2 or 4 or 6 or 8)) throw new OctaveException("axis: LIMITS vector must have 2, 4, 6, or 8 elements");
                for (int i = 0; i + 1 < Math.Min(lm.Numel, 6); i += 2)
                    if (lm.Re[i] >= lm.Re[i + 1]) throw new OctaveException($"axis: LIMITS({i + 1}) must be less than LIMITS({i + 2})");
            }
        });
        Pre("isprop", (it, a, n) =>
        {
            if (a.Length != 2) throw new OctaveException("Invalid call to isprop");
            if (!IsString(a[1])) throw new OctaveException("isprop: PROP name must be a string");
        });
        Pre("fullfile", (it, a, n) =>
        {
            foreach (var v in a)
                if (!(IsString(v) || v is CellValue c && c.Data.All(IsString)) && !(v is MatrixValue m0 && m0.Numel == 0))
                    throw new OctaveException("fullfile: input must be strings or cell strings");
            var cells = a.OfType<CellValue>().Where(c => c.Numel != 1).ToList();
            if (cells.Count > 1 && cells.Any(c => c.Numel != cells[0].Numel)) throw new OctaveException("fullfile: cell arrays must be the same size");
        });
        if (cur!.Builtins.TryGetValue("fullfile", out var ff0))
            cur!.Builtins["fullfile"] = (it, a, n) =>
            {
                var cs = a.OfType<CellValue>().ToList();
                if (cs.Count == 0) return ff0(it, a, n);
                var big = cs.FirstOrDefault(c => c.Numel != 1) ?? cs[0];
                var res = new Value[big.Numel];
                for (int i = 0; i < res.Length; i++)
                    res[i] = ff0(it, a.Select(v => v is CellValue c ? c.Data[c.Numel == 1 ? 0 : i] : v).ToArray(), 1)[0];
                return new Value[] { new CellValue(big.Rows, big.Cols, res) };
            };
        Pre("trisurf", (it, a, n) =>
        {
            if (a.Length >= 5 && VNum(a[4]) && VNum(a[0]))
            {
                var c = a[4]; int nv = a[1].Numel, nt = a[0].Rows;
                bool vec = c.Rows == 1 || c.Cols == 1;
                if (vec ? c.Numel != nv && c.Numel != nt : c.Rows != nv && c.Rows != nt) throw new OctaveException("trisurf: the numbers of colors specified in C must equal the number of vertices in [X, Y, Z] or the number of triangles in TRI");
                if (!vec && c.Cols != 3) throw new OctaveException("trisurf: TrueColor C matrix must have 3 columns");
            }
        });
        if (cur!.Builtins.TryGetValue("isprop", out var ip0))
            cur!.Builtins["isprop"] = (it, a, n) =>
            {
                if (a.Length == 2 && a[0] is MapValue && IsString(a[1]))
                    return new Value[] { Bool(S(a[1], "isprop") is "Count" or "KeyType" or "ValueType") };
                if (a.Length == 2 && a[0] is MatrixValue hm && !hm.IsChar && IsString(a[1]))
                {
                    var r = new MatrixValue(hm.R, hm.C) { Kind = NumKind.Logical };
                    for (int i = 0; i < hm.Numel; i++)
                    {
                        if (it.Graphics.Lookup(hm.Re[i]) == null) continue;
                        try { it.CallFunction("get", new Value[] { Num(hm.Re[i]), a[1] }, 1); r.Re[i] = 1; } catch (OctaveException) { }
                    }
                    return new Value[] { r };
                }
                return ip0(it, a, n);
            };
        Pre("hgload", (it, a, n) => { if (a.Length >= 2 && a[1] is not StructValue) throw new OctaveException("hgload: PROP_STRUCT must be a struct"); });
        Remap("hgload", m => m.Contains("unable to find file") ? "hgload: unable to locate file " + m.Split("file ").Last() : null);
        foreach (var mm in new[] { "min", "max" })
        {
            if (!cur!.Builtins.TryGetValue(mm, out var mm0)) continue;
            var nm = mm;
            cur!.Builtins[nm] = (it, a, n) =>
            {
                int L = a.Length; bool inc = false, all = false;
                while (L > 1 && IsString(a[L - 1]))
                {
                    var o = S(a[L - 1], nm).ToLowerInvariant();
                    if (o == "includenan") inc = true; else if (o == "all") all = true; else if (o != "omitnan") break;
                    L--;
                }
                if (L == a.Length) return mm0(it, a, n);
                var b = a.Take(L).ToArray();
                if (all) { b[0] = it.CallFunction("reshape", new Value[] { b[0], Num(b[0].Numel), Num(1) }, 1)[0]; if (b.Length > 2) b = b.Take(2).ToArray(); }
                var r = mm0(it, b, n);
                if (inc && !(b.Length >= 2 && b[1].Numel > 0) && b[0].Numel > 0)
                {
                    var nan = it.CallFunction("isnan", new Value[] { b[0] }, 1)[0];
                    var args = b.Length >= 3 ? new Value[] { nan, b[2] } : new Value[] { nan };
                    if (b.Length < 3) { var d = b[0].Dims; int fd = 1; for (int i = 0; i < d.Length; i++) if (d[i] != 1) { fd = i + 1; break; } args = new Value[] { nan, Num(fd) }; }
                    var an = M(it.CallFunction("any", args, 1)[0], nm);
                    if (r[0] is MatrixValue v) { v = (MatrixValue)v.Gather(Enumerable.Range(0, v.Numel).ToArray(), v.R, v.C); v.SetDims(r[0].Dims); for (int i = 0; i < v.Numel && i < an.Numel; i++) if (an.Re[i] != 0) { v.Re[i] = double.NaN; if (v.Im != null) v.Im[i] = 0; } r = r.ToArray(); r[0] = v; }
                }
                return r;
            };
        }
        cur!.Builtins.Remove("range"); cur!.Builtins.Remove("bounds");
        Pre("computer", (it, a, n) =>
        {
            if (a.Length > 1) throw new OctaveException("Invalid call to computer");
            if (a.Length == 1 && !(IsString(a[0]) && S(a[0], "computer") == "arch")) throw new OctaveException("computer: \"arch\" is only valid argument");
        });
        Pre("gray2ind", (it, a, n) => { if (a.Length > 0 && a[0] is Sparse.SparseValue) throw new OctaveException("gray2ind: I must be a grayscale or binary image"); });
        Pre("ind2gray", (it, a, n) => { if (a.Length > 0 && a[0] is Sparse.SparseValue) throw new OctaveException("ind2gray: X must be an indexed image"); });
        Pre("ind2rgb", (it, a, n) => { if (a.Length > 0 && a[0] is Sparse.SparseValue) throw new OctaveException("ind2rgb: X must be an indexed image"); });
        foreach (var (nm, pos) in new[] { ("mean", 1), ("rmse", 2), ("mape", 2) })
            Pre(nm, (it, a, n) =>
            {
                if (a.Length > pos && VNum(a[pos]) && a[pos].Numel == 1)
                {
                    double d = D(a[pos], nm);
                    if (!(d >= 1 && d == Math.Floor(d) && !double.IsInfinity(d))) throw new OctaveException($"{nm}: DIM must be a positive integer scalar or vector");
                }
            });
        Pre("iqr", (it, a, n) =>
        {
            if (a.Length > 1 && VNum(a[1]) && M(a[1], "iqr").Re.Any(d => !(d >= 1 && d == Math.Floor(d) && !double.IsInfinity(d))))
                throw new OctaveException("iqr: DIM must be a positive integer scalar or vector");
        });
        Pre("rmappdata", (it, a, n) =>
        {
            if (a.Length >= 2 && it.Builtins.TryGetValue("isappdata", out var isa))
                for (int i = 1; i < a.Length; i++)
                    if (IsString(a[i]) && isa(it, new[] { a[0], a[i] }, 1) is { Length: > 0 } r && r[0] is MatrixValue rm && rm.Re.Length > 0 && rm.Re[0] == 0)
                        throw new OctaveException($"rmappdata: appdata '{S(a[i], "rmappdata")}' is not present");
        });
        Pre("blkdiag", (it, a, n) => { if (a.Any(IsND)) throw new OctaveException("blkdiag: all arguments must be two-dimensional matrices"); });
        Pre("pathdef", (it, a, n) => { if (a.Length > 0) throw new OctaveException("Invalid call to pathdef"); });
        foreach (var nm in new[] { "xtickangle", "ytickangle", "ztickangle" })
            Pre(nm, (it, a, n) =>
            {
                var v = a.Length == 2 ? a[1] : a.Length == 1 ? a[0] : null;
                if (v != null && !VNum(v)) throw new OctaveException($"{nm}: ANGLE must be a real numeric scalar");
            });
        Pre("gco", (it, a, n) => { if (a.Length > 0 && (!VNum(a[0]) || it.Graphics.Lookup(D(a[0], "gco")) == null)) throw new OctaveException("gco: HFIG must be a graphics handle"); });
        Pre("delete", (it, a, n) => { if (a.Length > 0 && !IsString(a[0]) && !VNum(a[0]) && a[0] is not CellValue && a[0] is not ObjectValue) throw new OctaveException("delete: first argument must be a filename or graphics handle"); });
        Pre("dir", (it, a, n) => { if (a.Length > 0 && !IsString(a[0])) throw new OctaveException("dir: DIRECTORY argument must be a string"); });
        Pre("help", (it, a, n) =>
        {
            if (a.Length > 1) throw new OctaveException("Invalid call to help.  help: called with too many inputs");
            if (a.Length == 1 && !IsString(a[0])) throw new OctaveException("help: invalid input");
        });
        Remap("nthargout", m => m.StartsWith("element number") ? "nthargout: too many output arguments: " + m.Split(' ')[2] : null);
        Pre("lin2mu", (it, a, n) => { if (a.Length > 0 && !VNum(a[0])) throw new OctaveException("lin2mu: X must be a real numeric matrix: invalid conversion"); });
    }
}
