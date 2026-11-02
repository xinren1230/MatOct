// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Numerics;
using System.Text;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Phase 5: core functions (Octave C++ builtins) needed by Octave's m-file library.</summary>
public static partial class BuiltinLibrary
{
    const double Tiny = 1e-300;   // close to realmin^(1/2)... small enough for Lentz
    static readonly double Eps = Math.Pow(2, -52);

    static Value Paren(Interpreter it, Value x, Value[] idx) => it.CallFunction("subsref_paren__", new[] { x }.Concat(idx).ToArray(), 1)[0];
    static MatrixValue Like(MatrixValue x) { var r = new MatrixValue(x.R, x.C) { ND = x.ND }; return r; }

    static void RegisterCompat3()
    {
        Reg1("pow2", a =>
        {
            if (a.Length < 1 || a.Length > 2) throw new OctaveException("Invalid call to pow2");
            if (a.Length == 1) return Ops.Binary(".^", MatrixValue.Scalar(2), a[0]);
            return Ops.Binary(".*", a[0], Ops.Binary(".^", MatrixValue.Scalar(2), a[1]));
        });
        Reg("filter", (it, a, nout) =>
        {
            if (a.Length < 3 || a.Length > 5) throw new OctaveException("Invalid call to filter");
            var b = M(a[0], "filter"); var av = M(a[1], "filter"); var x = M(a[2], "filter");
            if (av.Numel == 0 || av.Re[0] == 0) throw new OctaveException("filter: a(1) must be nonzero");
            int nb = b.Numel, na = av.Numel, n = Math.Max(nb, na);
            double a0 = av.Re[0];
            var bb = new double[n]; var aa = new double[n];
            for (int k = 0; k < nb; k++) bb[k] = b.Re[k] / a0;
            for (int k = 0; k < na; k++) aa[k] = av.Re[k] / a0;
            var d = x.Dims;
            int dim = a.Length > 4 ? I(a[4], "filter") : Array.FindIndex(d, v => v != 1) + 1;
            if (dim <= 0) dim = 1;
            int len = dim <= d.Length ? d[dim - 1] : 1;
            int stride = 1; for (int k = 0; k < dim - 1 && k < d.Length; k++) stride *= d[k];
            int total = x.Numel, nvec = len == 0 ? 0 : total / len;
            MatrixValue? zi = a.Length > 3 && a[3] is MatrixValue zz && zz.Numel > 0 ? zz : null;
            if (zi != null && zi.Numel != (n - 1) * Math.Max(nvec, 1)) throw new OctaveException("filter: first dimension of SI must be of length max (length (a), length (b)) - 1");
            var y = new MatrixValue(1, total); y.SetDims(d);
            var zf = new MatrixValue(n - 1, Math.Max(nvec, 1));
            int v = 0;
            for (int outer = 0; outer < total; outer += stride * len)
                for (int inner = 0; inner < stride; inner++, v++)
                {
                    var z = new double[n];
                    if (zi != null) for (int k = 0; k < n - 1; k++) z[k] = zi.Re[v * (n - 1) + k];
                    for (int t = 0; t < len; t++)
                    {
                        int idx = outer + inner + t * stride;
                        double xi = x.Re[idx];
                        double yi = bb[0] * xi + z[0];
                        for (int k = 1; k < n; k++) z[k - 1] = bb[k] * xi + (k < n - 1 ? z[k] : 0) - aa[k] * yi;
                        y.Re[idx] = yi;
                    }
                    for (int k = 0; k < n - 1; k++) zf.Re[v * (n - 1) + k] = z[k];
                }
            return nout > 1 ? new Value[] { y, zf } : new Value[] { y };
        });
        RegI("nth_element", (it, a) =>
        {
            if (a.Length < 2 || a.Length > 3) throw new OctaveException("Invalid call to nth_element");
            var x = a[0]; var d = x.Dims;
            int dim = a.Length > 2 ? I(a[2], "nth_element") : Array.FindIndex(d, v => v != 1) + 1;
            if (dim <= 0) dim = 1;
            int len = dim <= d.Length ? d[dim - 1] : 1;
            var nv = M(a[1], "nth_element");
            for (int k = 0; k < nv.Numel; k++)
                if (nv.Re[k] < 1 || nv.Re[k] > len || nv.Re[k] != Math.Floor(nv.Re[k]))
                    throw new OctaveException("nth_element: n must be valid index");
            var srt = it.CallFunction("sort", new Value[] { x, MatrixValue.Scalar(dim) }, 1)[0];
            int nd = Math.Max(d.Length, dim);
            var idx = new Value[nd];
            for (int k = 0; k < nd; k++) idx[k] = k == dim - 1 ? nv : MagicColon.Instance;
            return Paren(it, srt, idx);
        });
        // ---------------- indexing helpers
        Reg("sub2ind", (it, a, n) =>
        {
            if (a.Length < 2) throw new OctaveException("Invalid call to sub2ind", "Octave:invalid-fun-call");
            var dm = M(a[0], "sub2ind");
            if (dm.Numel < 1 || dm.Re.Any(v => v < 0 || v != Math.Floor(v))) throw new OctaveException("sub2ind: DIMS must contain integers");
            var dims = dm.Re.Select(v => (int)v).ToList();
            int ns = a.Length - 1;
            // collapse trailing dimensions
            while (dims.Count < ns) dims.Add(1);
            if (ns < dims.Count) { int p = 1; for (int k = ns - 1; k < dims.Count; k++) p *= dims[k]; dims = dims.Take(ns - 1).Append(p).ToList(); }
            var subs = a.Skip(1).Select(v => M(v, "sub2ind")).ToArray();
            var shape = subs[0];
            foreach (var s in subs)
                if (s.Numel != shape.Numel || (s.Numel != 1 && !s.Dims.SequenceEqual(shape.Dims))) throw new OctaveException("sub2ind: all subscripts must be of the same size");
            var r = Like(shape);
            for (int e = 0; e < shape.Numel; e++)
            {
                double idx = 0, mult = 1;
                for (int k = 0; k < ns; k++)
                {
                    double v = subs[k].Re[e];
                    if (v != Math.Floor(v)) throw new OctaveException("sub2ind: subscripts must be either integers 1 to (2^63)-1 or logicals", "Octave:index-out-of-bounds");
                    if (v < 1 || v > dims[k]) throw new OctaveException("sub2ind: index out of range", "Octave:index-out-of-bounds");
                    idx += (v - 1) * mult; mult *= dims[k];
                }
                r.Re[e] = idx + 1;
            }
            return Out(r);
        });
        Reg("ind2sub", (it, a, n) =>
        {
            if (a.Length != 2) throw new OctaveException("Invalid call to ind2sub", "Octave:invalid-fun-call");
            var dm = M(a[0], "ind2sub");
            if (dm.Numel < 1 || dm.Re.Any(v => v < 0 || v != Math.Floor(v))) throw new OctaveException("ind2sub: DIMS must contain integers");
            var dims = dm.Re.Select(v => (int)v).ToList();
            int no = Math.Max(n, 1);
            while (dims.Count < no) dims.Add(1);
            if (no < dims.Count) { int p = 1; for (int k = no - 1; k < dims.Count; k++) p *= dims[k]; dims = dims.Take(no - 1).Append(p).ToList(); }
            double total = dims.Aggregate(1.0, (x, y) => x * y);
            var ind = M(a[1], "ind2sub");
            var outs = Enumerable.Range(0, no).Select(_ => Like(ind)).ToArray();
            for (int e = 0; e < ind.Numel; e++)
            {
                double v = ind.Re[e];
                if (v < 1 || v != Math.Floor(v)) throw new OctaveException("ind2sub: invalid index " + v, "Octave:index-out-of-bounds");
                if (v > total) throw new OctaveException("ind2sub: index out of range", "Octave:index-out-of-bounds");
                long r = (long)v - 1;
                for (int k = 0; k < no; k++) { outs[k].Re[e] = r % dims[k] + 1; r /= dims[k]; }
            }
            return outs;
        });
        RegI("vec", (it, a) =>
        {
            var x = a[0];
            var r = Paren(it, x, new Value[] { MagicColon.Instance });
            if (a.Length > 1) { int d = I(a[1], "vec"); var dd = Enumerable.Repeat(1, Math.Max(2, d)).ToArray(); dd[d - 1] = ((ArrayValue)r).Numel; ((ArrayValue)r).SetDims(dd); }
            return r;
        });
        Reg1("cbrt", a => { var x = M(a[0], "cbrt"); var r = Like(x); for (int k = 0; k < x.Numel; k++) r.Re[k] = Math.Cbrt(x.Re[k]); return r; });
        Reg1("repelems", a =>
        {
            var x = M(a[0], "repelems"); var r = M(a[1], "repelems");
            if (r.R != 2) throw new OctaveException("repelems: R must be a matrix with two rows");
            var o = new List<double>();
            for (int j = 0; j < r.C; j++) { int i = (int)r.Re[2 * j] - 1; for (int k = 0; k < (int)r.Re[2 * j + 1]; k++) o.Add(x.Re[i]); }
            var m = new MatrixValue(1, o.Count, o.ToArray(), null, x.Kind); return m;
        });
        Reg1("resize", a =>
        {
            var x = (ArrayValue)a[0];
            int[] nd = a.Length == 2 ? (M(a[1], "resize").Numel == 1 ? new[] { I(a[1], "resize"), I(a[1], "resize") } : M(a[1], "resize").Re.Select(v => (int)v).ToArray())
                                     : a.Skip(1).Select(v => I(v, "resize")).ToArray();
            if (x is MatrixValue m)
            {
                var r = new MatrixValue(nd[0], nd.Skip(1).Aggregate(1, (p, q) => p * q), m.Kind, m.IsComplex); r.SetDims(nd);
                var od = m.Dims; var rd = r.Dims;
                for (int k = 0; k < r.Numel; k++)
                {
                    int rem = k, src = 0, mul = 1; bool ok = true;
                    for (int d = 0; d < rd.Length; d++)
                    {
                        int s = rem % rd[d]; rem /= rd[d];
                        int odd = d < od.Length ? od[d] : 1;
                        if (s >= odd) { ok = false; break; }
                        src += s * mul; mul *= odd;
                    }
                    if (ok && src < m.Numel) { r.Re[k] = m.Re[src]; if (r.Im != null && m.Im != null) r.Im[k] = m.Im[src]; }
                }
                return r;
            }
            {
                var od = x.Dims; var lin = new List<int>(); int tot = nd.Aggregate(1, (p, q) => p * q);
                for (int k = 0; k < tot; k++)
                {
                    int rem = k, src = 0, mul = 1;
                    for (int d = 0; d < nd.Length; d++)
                    {
                        int s = rem % nd[d]; rem /= nd[d]; int odd = d < od.Length ? od[d] : 1;
                        if (s >= odd) throw new OctaveException("resize: invalid argument");
                        src += s * mul; mul *= odd;
                    }
                    lin.Add(src);
                }
                var g = x.Gather(lin.ToArray(), nd[0], tot / Math.Max(1, nd[0]));
                if (nd[0] == 0) g = x.Gather(Array.Empty<int>(), 0, nd.Skip(1).Aggregate(1, (p, q) => p * q));
                g.SetDims(nd); return g;
            }
        });
        Reg1("typecast", a =>
        {
            var x = M(a[0], "typecast"); var cls = S(a[1], "typecast").ToLowerInvariant();
            byte[] bytes = ToBytes(x);
            NumKind k = cls switch
            {
                "double" => NumKind.Double, "single" => NumKind.Single, "int8" => NumKind.Int8, "uint8" => NumKind.UInt8, "int16" => NumKind.Int16,
                "uint16" => NumKind.UInt16, "int32" => NumKind.Int32, "uint32" => NumKind.UInt32, "int64" => NumKind.Int64, "uint64" => NumKind.UInt64,
                "char" => NumKind.Char, "logical" => NumKind.Logical,
                _ => throw new OctaveException("typecast: TYPE name not valid")
            };
            int sz = KindSize(k);
            if (bytes.Length % sz != 0) throw new OctaveException("typecast: incorrect number of input values to make output value");
            int cnt = bytes.Length / sz; var v = new double[cnt];
            for (int i = 0; i < cnt; i++)
                v[i] = k switch
                {
                    NumKind.Double => BitConverter.ToDouble(bytes, i * 8), NumKind.Single => BitConverter.ToSingle(bytes, i * 4),
                    NumKind.Int8 => (sbyte)bytes[i], NumKind.UInt8 or NumKind.Char or NumKind.Logical => bytes[i],
                    NumKind.Int16 => BitConverter.ToInt16(bytes, i * 2), NumKind.UInt16 => BitConverter.ToUInt16(bytes, i * 2),
                    NumKind.Int32 => BitConverter.ToInt32(bytes, i * 4), NumKind.UInt32 => BitConverter.ToUInt32(bytes, i * 4),
                    NumKind.Int64 => BitConverter.ToInt64(bytes, i * 8), _ => BitConverter.ToUInt64(bytes, i * 8),
                };
            return x.C == 1 && x.R > 1 ? new MatrixValue(cnt, 1, v, null, k) : new MatrixValue(1, cnt, v, null, k);
        });
        // base64 helpers used by matlab.net.base64encode/decode and base64_decode
        Reg1("base64_encode", a =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to base64_encode");
            if (a[0] is not MatrixValue x || x.Kind == NumKind.Char || x.IsComplex) throw new OctaveException("base64_encode: encoding is supported only for numeric arrays");
            return Str(Convert.ToBase64String(ToBytes(x)));
        });
        Reg1("__base64_decode_bytes__", a =>
        {
            var b = Convert.FromBase64String(S(a[0], "__base64_decode_bytes__"));
            var v = b.Select(x => (double)x).ToArray();
            return new MatrixValue(1, b.Length, v, null, NumKind.UInt8);
        });
        Reg1("sizeof", a => Num(a[0] is MatrixValue m ? m.Numel * KindSize(m.Kind) * (m.IsComplex ? 2 : 1) : 0));
        Reg("mat2cell", (it, a, n) =>
        {
            if (a.Length < 2) throw new OctaveException("Invalid call to mat2cell", "Octave:invalid-fun-call");
            var x = (ArrayValue)a[0];
            var xd = x.Dims;
            var parts = new List<double[]>();
            for (int d = 0; d < Math.Max(2, a.Length - 1); d++)
            {
                if (d + 1 < a.Length) parts.Add(M(a[d + 1], "mat2cell").Re);
                else parts.Add(new double[] { d < xd.Length ? xd[d] : 1 });
                int dimLen = d < xd.Length ? xd[d] : 1;
                if (d + 1 < a.Length && Math.Abs(parts[d].Sum() - dimLen) > 0)
                    throw new OctaveException($"mat2cell: dimension vectors must add up to the size of the input (dim {d + 1}: {parts[d].Sum()} != {dimLen})");
            }
            var cd = parts.Select(p => p.Length).ToArray();
            var c = new CellValue(cd[0], cd.Skip(1).Aggregate(1, (p, q) => p * q)); c.SetDims(cd);
            int total = c.Data.Length;
            for (int k = 0; k < total; k++)
            {
                int rem = k; var idx = new Value[cd.Length];
                for (int d = 0; d < cd.Length; d++)
                {
                    int s = rem % cd[d]; rem /= cd[d];
                    double off = parts[d].Take(s).Sum(); double len = parts[d][s];
                    idx[d] = MatrixValue.Row(Enumerable.Range(0, (int)len).Select(q => off + q + 1.0));
                }
                c.Data[k] = Paren(it, x, idx);
            }
            return Out(c);
        });

        // ---------------- interpreter info / settings
        var settings = new Dictionary<string, Value>();
        void Setting(string name, Value init)
        {
            settings[name] = init;
            Reg(name, (it, a, n) =>
            {
                var old = settings[name];
                if (a.Length > 0 && !(a.Length == 1 && IsString(a[0]) && S(a[0], name) == "local")) settings[name] = a[0];
                return n > 0 || a.Length == 0 ? Out(old) : Array.Empty<Value>();
            });
        }
        Setting("debug_on_error", MatrixValue.Bool(false));
        Setting("debug_on_warning", MatrixValue.Bool(false));
        Setting("page_screen_output", MatrixValue.Bool(false));
        Setting("confirm_recursive_rmdir", MatrixValue.Bool(false));
        Setting("save_default_options", Str("-text"));
        Setting("missing_component_hook", Str(""));
        Setting("texi_macros_file", Str("macros.texi"));
        Setting("print_empty_dimensions", MatrixValue.Bool(true));
        Reg1("isguirunning", a => MatrixValue.Bool(false));
        Reg1("user_config_dir", a => Str(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
        Reg1("user_data_dir", a => Str(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        Reg1("get_home_directory", a => Str(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Reg1("OCTAVE_HOME", a => Str(AppContext.BaseDirectory));
        Reg1("P_tmpdir", a => Str(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
        Reg1("tilde_expand", a =>
        {
            string T(string s) => s.StartsWith("~") ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + s[1..] : s;
            if (a[0] is CellValue c) return new CellValue(c.R, c.C, c.Data.Select(v => (Value)Str(T(S(v, "tilde_expand")))).ToArray());
            return Str(T(S(a[0], "tilde_expand")));
        });
        Reg1("canonicalize_file_name", a =>
        {
            var p = S(a[0], "canonicalize_file_name");
            return File.Exists(p) || Directory.Exists(p) ? Str(Path.GetFullPath(p)) : Str("");
        });
        Reg("__octave_config_info__", (it, a, n) =>
        {
            var s = new StructValue();
            void F(string k, Value v) { s.Fields.Add(k); s.Elems[0][k] = v; }
            F("ENABLE_64", MatrixValue.Bool(true)); F("build_environment", new StructValue()); F("build_features", new StructValue());
            F("canonical_host_type", Str(OperatingSystem.IsWindows() ? "x86_64-w64-mingw32" : "x86_64-pc-linux-gnu"));
            F("float_format", Str("ieee-le")); F("words_big_endian", MatrixValue.Bool(false)); F("words_little_endian", MatrixValue.Bool(true));
            F("release_date", Str("2026-11-01")); F("version", Str("9.0.0")); F("major_version", Num(9)); F("minor_version", Num(0));
            F("unix", MatrixValue.Bool(!OperatingSystem.IsWindows())); F("windows", MatrixValue.Bool(OperatingSystem.IsWindows()));
            F("mac", MatrixValue.Bool(OperatingSystem.IsMacOS()));
            if (a.Length > 0)
            {
                var k = S(a[0], "__octave_config_info__");
                if (s.Elems[0].TryGetValue(k, out var v)) return Out(v);
                return Out(Str(""));
            }
            return Out(s);
        });
        Reg("file_in_loadpath", (it, a, n) =>
        {
            var f = a[0] is CellValue c ? c.Data.Select(v => S(v, "file_in_loadpath")).ToArray() : new[] { S(a[0], "file_in_loadpath") };
            var dirs = new List<string> { Directory.GetCurrentDirectory() }; dirs.AddRange(it.SearchPath);
            bool all = a.Length > 1 && S(a[1], "file_in_loadpath") == "all";
            var hits = new List<string>();
            foreach (var d in dirs) foreach (var x in f) { var p = Path.IsPathRooted(x) ? x : Path.Combine(d, x); if (File.Exists(p)) { hits.Add(p); if (!all) return Out(Str(p)); } }
            foreach (var x in f) if (!Path.IsPathRooted(x) && x.EndsWith(".m") && !x.Contains('/') && CoreMLib.Get(x[..^2]) != null) { hits.Add("<core>/" + x); if (!all) return Out(Str("<core>/" + x)); }
            if (all) return Out(new CellValue(hits.Count, 1, hits.Select(h => (Value)Str(h)).ToArray()));
            return Out(new MatrixValue(0, 0));
        });
        Reg("__which__", (it, a, n) =>
        {
            var names = a.Select(v => S(v, "__which__")).ToArray();
            var s = new StructValue(1, names.Length, new List<string> { "name", "file", "type" });
            for (int k = 0; k < names.Length; k++)
            {
                bool ex = it.FunctionExists(names[k], out bool bi);
                s.Elems[k]["name"] = Str(names[k]);
                s.Elems[k]["file"] = Str(ex && !bi ? (it.FindFile(names[k]) ?? "") : "");
                s.Elems[k]["type"] = Str(!ex ? "" : bi ? "built-in function" : "function");
            }
            return Out(s);
        });
        Reg("unsetenv", (it, a, n) => { Environment.SetEnvironmentVariable(S(a[0], "unsetenv"), null); return Out(Num(0)); });
        Reg("chdir", (it, a, n) => it.Builtins["cd"](it, a, n));
        Reg("glob", (it, a, n) =>
        {
            var pats = a[0] is CellValue c ? c.Data.Select(v => S(v, "glob")).ToArray() : Enumerable.Range(0, M(a[0], "glob").R).Select(r => RowStr(M(a[0], "glob"), r).TrimEnd()).ToArray();
            var res = new List<string>();
            foreach (var pat in pats)
            {
                var dir = Path.GetDirectoryName(pat); var fp = Path.GetFileName(pat);
                var d = string.IsNullOrEmpty(dir) ? "." : dir;
                if (!Directory.Exists(d)) continue;
                foreach (var e in Directory.EnumerateFileSystemEntries(d, fp).OrderBy(x => x, StringComparer.Ordinal))
                    res.Add(string.IsNullOrEmpty(dir) ? Path.GetFileName(e) : Path.Combine(dir, Path.GetFileName(e)));
            }
            return Out(new CellValue(res.Count, 1, res.Select(r => (Value)Str(r)).ToArray()));
        });
        Reg("lasterror", (it, a, n) =>
        {
            var s = new StructValue(1, 1, new List<string> { "message", "identifier", "stack" });
            s.Elems[0]["message"] = Str(it.LastError ?? ""); s.Elems[0]["identifier"] = Str(it.LastErrorId ?? "");
            s.Elems[0]["stack"] = new StructValue(0, 1, new List<string> { "file", "name", "line", "column" });
            if (a.Length > 0 && IsString(a[0]) && S(a[0], "lasterror") == "reset") { it.LastError = ""; it.LastErrorId = ""; }
            return Out(s);
        });
        Reg("nargin", (it, a, n) =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to nargin", "Octave:invalid-fun-call");
            var f = a[0] is FunctionHandleValue fh ? fh.Name ?? "" : S(a[0], "nargin");
            var uf = it.FindUserFunction(f) ?? throw new OctaveException($"nargin: invalid function name: {f}");
            return Out(Num(uf.Params.Count > 0 && uf.Params[^1] == "varargin" ? -uf.Params.Count : uf.Params.Count));
        });
        Reg("nargout", (it, a, n) =>
        {
            if (a.Length != 1) throw new OctaveException("Invalid call to nargout", "Octave:invalid-fun-call");
            var f = a[0] is FunctionHandleValue fh ? fh.Name ?? "" : S(a[0], "nargout");
            var uf = it.FindUserFunction(f) ?? throw new OctaveException($"nargout: invalid function name: {f}");
            return Out(Num(uf.Outputs.Count > 0 && uf.Outputs[^1] == "varargout" ? -uf.Outputs.Count : uf.Outputs.Count));
        });
        Reg("__make_valid_name__", (it, a, n) =>
        {
            if (a.Length == 0) throw new OctaveException("Invalid call to makeValidName", "Octave:invalid-fun-call");
            bool Valid(string s) => s.Length > 0 && (char.IsLetter(s[0])) && s.All(ch => char.IsLetterOrDigit(ch) || ch == '_') && !Lexing.Lexer.IsKeyword(s);
            if (!(a[0] is CellValue c0 && c0.Data.All(v => v is MatrixValue { Kind: NumKind.Char } || v is MatrixValue { Numel: 0 })) && !(a[0] is MatrixValue { Kind: NumKind.Char }))
                throw new OctaveException("makeValidName: STR must be a string or cellstr");
            if ((a.Length - 1) % 2 != 0) throw new OctaveException("makeValidName: property/value options must occur in pairs");
            string style = "underscore", prefix = "x";
            for (int q = 1; q < a.Length; q += 2)
            {
                if (a[q] is not MatrixValue { Kind: NumKind.Char }) throw new OctaveException("makeValidName: option argument must be a string");
                var key = S(a[q], "makeValidName");
                if (key.Equals("ReplacementStyle", StringComparison.OrdinalIgnoreCase))
                {
                    if (a[q + 1] is not MatrixValue { Kind: NumKind.Char }) throw new OctaveException("makeValidName: 'ReplacementStyle' value must be a string");
                    style = S(a[q + 1], "makeValidName").ToLowerInvariant();
                    if (style is not ("underscore" or "delete" or "hex")) throw new OctaveException($"makeValidName: invalid 'ReplacementStyle' value '{style}'");
                }
                else if (key.Equals("Prefix", StringComparison.OrdinalIgnoreCase))
                {
                    if (a[q + 1] is not MatrixValue { Kind: NumKind.Char }) throw new OctaveException("makeValidName: 'Prefix' value must be a string");
                    prefix = S(a[q + 1], "makeValidName");
                    if (!Valid(prefix)) throw new OctaveException($"makeValidName: invalid 'Prefix' value '{prefix}'");
                }
                else throw new OctaveException($"makeValidName: unknown property '{key}'");
            }
            string Fix(string s, out bool mod)
            {
                mod = !Valid(s);
                if (!mod) return s;
                if (Lexing.Lexer.IsKeyword(s)) return prefix + char.ToUpperInvariant(s[0]) + s[1..];
                s = s.Trim();
                var sb = new StringBuilder();
                bool up = false;
                foreach (var ch in s)
                {
                    if (char.IsWhiteSpace(ch)) { up = sb.Length > 0; continue; }
                    sb.Append(up ? char.ToUpperInvariant(ch) : ch); up = false;
                }
                var t = new StringBuilder();
                foreach (var ch in sb.ToString())
                {
                    if (char.IsLetterOrDigit(ch) && ch < 128 || ch == '_') t.Append(ch);
                    else if (style == "underscore") t.Append('_');
                    else if (style == "hex") t.Append("0x").Append(((int)ch).ToString("X2"));
                }
                var r = t.ToString();
                if (r.Length == 0 || !char.IsLetter(r[0])) r = prefix + r;
                return r;
            }
            if (a[0] is CellValue c)
            {
                var r = new CellValue(c.R, c.C); var m = new MatrixValue(c.R, c.C, NumKind.Logical); r.SetDims(c.Dims); m.SetDims(c.Dims);
                for (int k = 0; k < c.Data.Length; k++) { r.Data[k] = Str(Fix(c.Data[k] is MatrixValue { Numel: 0 } ? "" : S(c.Data[k], "makeValidName"), out bool md)); m.Re[k] = md ? 1 : 0; }
                return Out(r, m);
            }
            var o = Fix(S(a[0], "makeValidName"), out bool mm);
            return Out(Str(o), MatrixValue.Bool(mm));
        });

        // ---------------- special functions (continued fractions as in Octave's C++)
        Reg1("__gammainc__", a =>
        {
            var x = M(a[0], "__gammainc__"); var av = M(a[1], "__gammainc__");
            var r = Like(x);
            for (int k = 0; k < x.Numel; k++)
            {
                double xj = x.Re[k], aj = av.Numel == 1 ? av.Re[0] : av.Re[k];
                double y = Tiny, Cj = y, Dj = 0, bj = xj - aj + 1, Deltaj = 0, alpha = 1; int j = 1;
                while (Math.Abs((Deltaj - 1) / y) > Eps && j < 200)
                {
                    Dj = bj + alpha * Dj; if (Dj == 0) Dj = Tiny;
                    Cj = bj + alpha / Cj; if (Cj == 0) Cj = Tiny;
                    Dj = 1 / Dj; Deltaj = Cj * Dj; y *= Deltaj;
                    alpha = -j * (j - aj); bj += 2; j++;
                }
                r.Re[k] = y;
            }
            return r;
        });
        Reg1("__betainc__", a =>
        {
            var x = M(a[0], "__betainc__"); var A = M(a[1], "__betainc__"); var B = M(a[2], "__betainc__");
            var r = Like(x);
            for (int k = 0; k < x.Numel; k++)
            {
                double xj = x.Re[k], aj = A.Numel == 1 ? A.Re[0] : A.Re[k], bj = B.Numel == 1 ? B.Re[0] : B.Re[k];
                double alpha = 1, beta = aj - (aj * (aj + bj)) / (aj + 1) * xj, x2 = xj * xj;
                double f = Tiny, C = f, D = 0, Delta = 0; int j = 1;
                while (Math.Abs(Delta - 1) > Eps && j < 200)
                {
                    D = beta + alpha * D; if (D == 0) D = Tiny;
                    C = beta + alpha / C; if (C == 0) C = Tiny;
                    D = 1 / D; Delta = C * D; f *= Delta;
                    alpha = ((aj + j - 1) * (aj + bj + j - 1) * (bj - j) * j) / ((aj + 2 * j - 1) * (aj + 2 * j - 1)) * x2;
                    beta = aj + 2 * j + ((j * (bj - j)) / (aj + 2 * j - 1) - ((aj + j) * (aj + bj + j)) / (aj + 2 * j + 1)) * xj;
                    j++;
                }
                r.Re[k] = f;
            }
            return r;
        });
        Reg1("__expint__", a =>
        {
            var x = M(a[0], "__expint__");
            var r = new MatrixValue(x.R, x.C, NumKind.Double, x.IsComplex) { ND = x.ND };
            for (int k = 0; k < x.Numel; k++)
            {
                var xj = new Complex(x.Re[k], x.Im?[k] ?? 0);
                Complex f = Tiny, C = f, D = 0, alpha = 1, beta = xj, Delta = 0; int j = 1;
                while (Complex.Abs(Delta - 1) > Eps && j < 1000)
                {
                    D = beta + alpha * D; if (D == 0) D = Tiny;
                    C = beta + alpha / C; if (C == 0) C = Tiny;
                    D = 1 / D; Delta = C * D; f *= Delta;
                    alpha = Math.Floor((j + 1) / 2.0);
                    beta = beta == xj ? 1 : xj;
                    j++;
                }
                r.Re[k] = f.Real; if (r.Im != null) r.Im[k] = f.Imaginary;
            }
            return r;
        });
        Reg1("__pchip_deriv__", a =>
        {
            var x = M(a[0], "__pchip_deriv__").Re; var y = M(a[1], "__pchip_deriv__"); int dim = a.Length > 2 ? I(a[2], "__pchip_deriv__") : 1;
            var r = new MatrixValue(y.R, y.C);
            int nx = x.Length;
            int lines = dim == 1 ? y.C : y.R;
            for (int l = 0; l < lines; l++)
            {
                double Y(int i) => dim == 1 ? y.Re[i + l * y.R] : y.Re[l + i * y.R];
                void Set(int i, double v) { if (dim == 1) r.Re[i + l * y.R] = v; else r.Re[l + i * y.R] = v; }
                var d = PchipSlopes(x, Enumerable.Range(0, nx).Select(Y).ToArray());
                for (int i = 0; i < nx; i++) Set(i, d[i]);
            }
            return r;
        });

        // ---------------- linear algebra
        Reg("schur", (it, a, n) =>
        {
            var m = M(a[0], "schur");
            if (m.R != m.C) throw new OctaveException("schur: argument must be a square matrix");
            int N = m.R;
            if (N == 0) return Out(new MatrixValue(0, 0), new MatrixValue(0, 0));
            var A = MathNet.Numerics.LinearAlgebra.Matrix<Complex>.Build.Dense(N, N, (i, j) => new Complex(m.Re[i + j * N], m.Im?[i + j * N] ?? 0));
            var (U, T) = ComplexSchur(A);
            bool real = !m.IsComplex && Enumerable.Range(0, N).All(i => Math.Abs(T[i, i].Imaginary) < 1e-12 * Math.Max(1, T.FrobeniusNorm()));
            var Um = FromCM(U, real); var Tm = FromCM(T, real);
            if (real) for (int i = 0; i < N; i++) for (int j = 0; j < i; j++) Tm.Re[i + j * N] = 0;
            return n <= 1 ? Out(Tm) : Out(Um, Tm);
        });
        Reg("rcond", (it, a, n) =>
        {
            var m = M(a[0], "rcond");
            if (m.R != m.C) throw new OctaveException("rcond: A must be a square matrix");
            if (m.Numel == 0) return Out(Num(double.PositiveInfinity));
            if (m.Re.Any(double.IsInfinity)) return Out(Num(0));
            if (m.Re.Any(double.IsNaN)) return Out(Num(double.NaN));
            var A = Ops.ToMathNet(m);
            double n1 = A.L1Norm();
            try
            {
                var inv = A.Inverse();
                double r = 1 / (n1 * inv.L1Norm());
                return Out(Num(double.IsNaN(r) || double.IsInfinity(1 / r) ? 0 : r));
            }
            catch { return Out(Num(0)); }
        });
        Reg("givens", (it, a, n) =>
        {
            double x = D(a[0], "givens"), y = D(a[1], "givens");
            double rr = Math.Sqrt(x * x + y * y); double c = rr == 0 ? 1 : x / rr, s = rr == 0 ? 0 : y / rr;
            var G = new MatrixValue(2, 2, new[] { c, -s, s, c });
            return n >= 2 ? Out(Num(c), Num(s)) : Out(G);
        });

        // ---------------- convn
        Reg1("convn", a =>
        {
            var A = M(a[0], "convn"); var B = M(a[1], "convn");
            string shape = a.Length > 2 ? S(a[2], "convn").ToLowerInvariant() : "full";
            if (shape is not ("full" or "same" or "valid")) throw new OctaveException("convn: SHAPE type not valid");
            var ad = A.Dims; var bd = B.Dims; int nd = Math.Max(ad.Length, bd.Length);
            ad = ad.Concat(Enumerable.Repeat(1, nd - ad.Length)).ToArray(); bd = bd.Concat(Enumerable.Repeat(1, nd - bd.Length)).ToArray();
            var fd = ad.Zip(bd, (p, q) => p == 0 || q == 0 ? 0 : p + q - 1).ToArray();
            int fn = fd.Aggregate(1, (p, q) => p * q);
            var full = new double[fn];
            int[] Sub(int k, int[] d) { var s = new int[d.Length]; for (int i = 0; i < d.Length; i++) { s[i] = k % d[i]; k /= d[i]; } return s; }
            int Lin(int[] s, int[] d) { int k = 0, m = 1; for (int i = 0; i < d.Length; i++) { k += s[i] * m; m *= d[i]; } return k; }
            for (int i = 0; i < A.Numel; i++)
            {
                if (A.Re[i] == 0) continue;
                var si = Sub(i, ad);
                for (int j = 0; j < B.Numel; j++)
                {
                    var sj = Sub(j, bd); var s = new int[nd]; for (int q = 0; q < nd; q++) s[q] = si[q] + sj[q];
                    full[Lin(s, fd)] += A.Re[i] * B.Re[j];
                }
            }
            int[] lo, od;
            if (shape == "full") { lo = new int[nd]; od = fd; }
            else if (shape == "same") { lo = bd.Select(q => (q - 1) / 2 + ((q - 1) % 2)).Select((v, i) => (int)Math.Ceiling((bd[i] - 1) / 2.0)).ToArray(); od = ad; }
            else { lo = bd.Select(q => q - 1).ToArray(); od = ad.Zip(bd, (p, q) => Math.Max(p - q + 1, 0)).ToArray(); }
            int on = od.Aggregate(1, (p, q) => p * q);
            var r = new MatrixValue(od[0], on / Math.Max(od[0], 1) * (od[0] == 0 ? 0 : 1)); r = new MatrixValue(od[0], od.Skip(1).Aggregate(1, (p, q) => p * q)); r.SetDims(od);
            for (int k = 0; k < on; k++) { var s = Sub(k, od); for (int q = 0; q < nd; q++) s[q] += lo[q]; r.Re[k] = full[Lin(s, fd)]; }
            return r;
        });

        // ---------------- quadcc (adaptive Gauss-Kronrod, vectorised integrand)
        Reg("quadcc", (it, a, n) =>
        {
            if (a.Length < 3) throw new OctaveException("Invalid call to quadcc", "Octave:invalid-fun-call");
            var f = a[0];
            if (!(f is FunctionHandleValue || IsString(f))) throw new OctaveException("quadcc: integrand F must be a function handle");
            double lo = D(a[1], "quadcc"), hi = D(a[2], "quadcc");
            double abstol = 1e-10, reltol = 1e-6;
            if (a.Length > 3 && a[3] is MatrixValue tm && tm.Numel > 0)
            {
                if (tm.Numel == 1) { if (tm.Re[0] < 0) throw new OctaveException("quadcc: tolerance must be positive"); reltol = tm.Re[0]; }
                else if (tm.Numel == 2) { abstol = tm.Re[0]; reltol = tm.Re[1]; if (abstol < 0 || reltol < 0) throw new OctaveException("quadcc: tolerances must be positive"); }
                else throw new OctaveException("quadcc: TOL must be a 1- or 2-element vector [AbsTol RelTol]");
            }
            var sing = a.Length > 4 && a[4] is MatrixValue sm ? sm.Re : Array.Empty<double>();
            double sign = 1;
            if (hi < lo) { (lo, hi) = (hi, lo); sign = -1; }
            Func<double[], double[]> G;
            double ta, tb;
            Func<double[], double[]> Eval = xs =>
            {
                var r = CallCallable(it, f, new Value[] { new MatrixValue(1, xs.Length, xs) }, 1);
                var m = M(r[0], "quadcc");
                if (m.Numel != xs.Length) throw new OctaveException("quadcc: integrand F must return a single, real-valued vector of the same size as the input");
                return m.Re;
            };
            if (double.IsInfinity(lo) || double.IsInfinity(hi))
            {
                if (double.IsInfinity(lo) && double.IsInfinity(hi)) { ta = -1; tb = 1; G = t => { var x = t.Select(v => v / (1 - v * v)).ToArray(); var y = Eval(x); return y.Select((v, i) => v * (1 + t[i] * t[i]) / Math.Pow(1 - t[i] * t[i], 2)).ToArray(); }; }
                else if (double.IsInfinity(hi)) { ta = 0; tb = 1; G = t => { var x = t.Select(v => lo + v / (1 - v)).ToArray(); var y = Eval(x); return y.Select((v, i) => v / Math.Pow(1 - t[i], 2)).ToArray(); }; }
                else { ta = -1; tb = 0; G = t => { var x = t.Select(v => hi + v / (1 + v)).ToArray(); var y = Eval(x); return y.Select((v, i) => v / Math.Pow(1 + t[i], 2)).ToArray(); }; }
            }
            else { ta = lo; tb = hi; G = Eval; }
            var bounds = new List<double> { ta };
            foreach (var s in sing.OrderBy(v => v)) if (s > ta && s < tb) bounds.Add(s);
            bounds.Add(tb);
            var (q, err, nev) = AdaptiveGK(G, bounds, abstol, reltol);
            return Out(Num(sign * q), Num(err), Num(nev));
        });

        // ---------------- strptime
        Reg("strptime", (it, a, n) =>
        {
            if (a.Length != 2) throw new OctaveException("Invalid call to strptime", "Octave:invalid-fun-call");
            var str = S(a[0], "strptime"); var fmt = S(a[1], "strptime");
            var tm = new Dictionary<string, double> { ["usec"] = 0, ["sec"] = 0, ["min"] = 0, ["hour"] = 0, ["mday"] = 0, ["mon"] = 0, ["year"] = 0, ["wday"] = 0, ["yday"] = 0, ["isdst"] = 0, ["gmtoff"] = 0 };
            int pos = StrpTime(str, fmt, tm);
            var s = new StructValue();
            foreach (var k in new[] { "usec", "sec", "min", "hour", "mday", "mon", "year", "wday", "yday", "isdst", "gmtoff" }) { s.Fields.Add(k); s.Elems[0][k] = Num(tm[k]); }
            s.Fields.Add("zone"); s.Elems[0]["zone"] = Str("GMT");
            return Out(s, Num(pos < 0 ? 0 : pos + 1));
        });
    }

    static readonly string[] MonNames = { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };
    static readonly string[] DayNames = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };

    /// <summary>Returns the number of characters consumed, or -1 when the string does not match.</summary>
    static int StrpTime(string s, string fmt, Dictionary<string, double> tm)
    {
        int p = 0;
        bool Num(int maxd, out int v, bool allowSign = false)
        {
            v = 0; int st = p; bool neg = false;
            while (p < s.Length && s[p] == ' ') p++;
            if (allowSign && p < s.Length && (s[p] == '+' || s[p] == '-')) { neg = s[p] == '-'; p++; }
            int d0 = p;
            while (p < s.Length && char.IsDigit(s[p]) && p - d0 < maxd) { v = v * 10 + (s[p] - '0'); p++; }
            if (p == d0) { p = st; return false; }
            if (neg) v = -v;
            return true;
        }
        bool Name(string[] names, out int idx)
        {
            idx = -1;
            foreach (var (nm, i) in names.Select((x, i) => (x, i)))
            {
                if (p + nm.Length <= s.Length && string.Compare(s, p, nm, 0, nm.Length, StringComparison.OrdinalIgnoreCase) == 0) { idx = i; p += nm.Length; return true; }
            }
            foreach (var (nm, i) in names.Select((x, i) => (x, i)))
                if (p + 3 <= s.Length && string.Compare(s, p, nm, 0, 3, StringComparison.OrdinalIgnoreCase) == 0) { idx = i; p += 3; return true; }
            return false;
        }
        bool pm = false, havePm = false;
        for (int i = 0; i < fmt.Length; i++)
        {
            char c = fmt[i];
            if (char.IsWhiteSpace(c)) { while (p < s.Length && char.IsWhiteSpace(s[p])) p++; continue; }
            if (c != '%' || i + 1 >= fmt.Length)
            {
                if (p >= s.Length || s[p] != c) return -1;
                p++; continue;
            }
            c = fmt[++i];
            if (c == 'E' || c == 'O') c = fmt[++i];
            int v;
            switch (c)
            {
                case '%': if (p >= s.Length || s[p] != '%') return -1; p++; break;
                case 'n': case 't': while (p < s.Length && char.IsWhiteSpace(s[p])) p++; break;
                case 'Y': if (!Num(4, out v, true)) return -1; tm["year"] = v - 1900; break;
                case 'y': if (!Num(2, out v)) return -1; tm["year"] = v < 69 ? v + 100 : v; break;
                case 'C': if (!Num(2, out v)) return -1; tm["year"] = v * 100 - 1900; break;
                case 'm': if (!Num(2, out v) || v < 1 || v > 12) return -1; tm["mon"] = v - 1; break;
                case 'd': case 'e': if (!Num(2, out v) || v < 1 || v > 31) return -1; tm["mday"] = v; break;
                case 'H': case 'k': if (!Num(2, out v) || v > 23) return -1; tm["hour"] = v; break;
                case 'I': case 'l': if (!Num(2, out v) || v < 1 || v > 12) return -1; tm["hour"] = v % 12; break;
                case 'M': if (!Num(2, out v) || v > 59) return -1; tm["min"] = v; break;
                case 'S': if (!Num(2, out v) || v > 61) return -1; tm["sec"] = v; break;
                case 'j': if (!Num(3, out v) || v < 1 || v > 366) return -1; tm["yday"] = v - 1; break;
                case 'w': if (!Num(1, out v) || v > 6) return -1; tm["wday"] = v; break;
                case 'b': case 'B': case 'h': if (!Name(MonNames, out v)) return -1; tm["mon"] = v; break;
                case 'a': case 'A': if (!Name(DayNames, out v)) return -1; tm["wday"] = v; break;
                case 'p':
                    if (p + 2 <= s.Length && string.Compare(s, p, "AM", 0, 2, StringComparison.OrdinalIgnoreCase) == 0) { pm = false; havePm = true; p += 2; }
                    else if (p + 2 <= s.Length && string.Compare(s, p, "PM", 0, 2, StringComparison.OrdinalIgnoreCase) == 0) { pm = true; havePm = true; p += 2; }
                    else return -1;
                    break;
                case 'D': { int q = StrpTime(s[p..], "%m/%d/%y", tm); if (q < 0) return -1; p += q; break; }
                case 'T': { int q = StrpTime(s[p..], "%H:%M:%S", tm); if (q < 0) return -1; p += q; break; }
                case 'R': { int q = StrpTime(s[p..], "%H:%M", tm); if (q < 0) return -1; p += q; break; }
                case 'r': { int q = StrpTime(s[p..], "%I:%M:%S %p", tm); if (q < 0) return -1; p += q; break; }
                case 'F': { int q = StrpTime(s[p..], "%Y-%m-%d", tm); if (q < 0) return -1; p += q; break; }
                case 's': { if (!Num(12, out v)) return -1; var t = DateTime.UnixEpoch.AddSeconds(v); tm["year"] = t.Year - 1900; tm["mon"] = t.Month - 1; tm["mday"] = t.Day; tm["hour"] = t.Hour; tm["min"] = t.Minute; tm["sec"] = t.Second; break; }
                case 'z': { int st = p; if (!Num(4, out v, true)) return -1; tm["gmtoff"] = (v / 100) * 3600 + (v % 100) * 60; break; }
                case 'Z': while (p < s.Length && char.IsLetter(s[p])) p++; break;
                default: return -1;
            }
        }
        if (havePm && pm) tm["hour"] += 12;
        return p;
    }

    static double[] PchipSlopes(double[] x, double[] y)
    {
        int n = x.Length; var d = new double[n];
        if (n < 2) return d;
        var h = new double[n - 1]; var del = new double[n - 1];
        for (int i = 0; i < n - 1; i++) { h[i] = x[i + 1] - x[i]; del[i] = (y[i + 1] - y[i]) / h[i]; }
        if (n == 2) { d[0] = d[1] = del[0]; return d; }
        static double Sgn(double v) => v > 0 ? 1 : v < 0 ? -1 : 0;
        double End(double h1, double h2, double d1, double d2)
        {
            double w1 = (h1 + h1 + h2) / (h1 + h2), w2 = -h1 / (h1 + h2);
            double r = w1 * d1 + w2 * d2;
            if (Sgn(r) != Sgn(d1)) r = 0;
            else if (Sgn(d1) != Sgn(d2) && Math.Abs(r) > Math.Abs(3 * d1)) r = 3 * d1;
            return r;
        }
        d[0] = End(h[0], h[1], del[0], del[1]);
        d[n - 1] = End(h[n - 2], h[n - 3 < 0 ? 0 : n - 3], del[n - 2], del[n - 3 < 0 ? 0 : n - 3]);
        for (int k = 1; k < n - 1; k++)
        {
            double h1 = h[k - 1], h2 = h[k];
            if (Sgn(del[k - 1]) * Sgn(del[k]) > 0)
            {
                double hs = h1 + h2, w1 = (hs + h1) / (3 * hs), w2 = (hs + h2) / (3 * hs);
                double dmax = Math.Max(Math.Abs(del[k - 1]), Math.Abs(del[k])), dmin = Math.Min(Math.Abs(del[k - 1]), Math.Abs(del[k]));
                double r1 = del[k - 1] / dmax, r2 = del[k] / dmax;
                d[k] = dmin / (w1 * r1 + w2 * r2);
            }
            else d[k] = 0;
        }
        return d;
    }

    static readonly double[] GKx = { 0.991455371120812639206854697526329, 0.949107912342758524526189684047851, 0.864864423359769072789712788640926, 0.741531185599394439863864773280788, 0.586087235467691130294144845693013, 0.405845151377397166906606412076961, 0.207784955007898467600689403773245, 0.0 };
    static readonly double[] GKw = { 0.022935322010529224963732008058970, 0.063092092629978553290700663189204, 0.104790010322250183839876322541518, 0.140653259715525918745189590510238, 0.169004726639267902826583426598550, 0.190350578064785409913256402421014, 0.204432940075298892414161999234649, 0.209482141084727828012999174891714 };
    static readonly double[] Gw = { 0.129484966168869693270611432679082, 0.279705391489276667901467771423780, 0.381830050505118944950369775488975, 0.417959183673469387755102040816327 };

    static (double q, double err, int nev) AdaptiveGK(Func<double[], double[]> f, List<double> bounds, double abstol, double reltol)
    {
        int nev = 0;
        (double q, double e) Seg(double a, double b)
        {
            double c = (a + b) / 2, h = (b - a) / 2;
            var xs = new double[15];
            for (int i = 0; i < 7; i++) { xs[i] = c - h * GKx[i]; xs[14 - i] = c + h * GKx[i]; }
            xs[7] = c;
            var y = f(xs); nev += 15;
            double k = GKw[7] * y[7], g = Gw[3] * y[7];
            for (int i = 0; i < 7; i++)
            {
                k += GKw[i] * (y[i] + y[14 - i]);
                if (i % 2 == 1) g += Gw[i / 2] * (y[i] + y[14 - i]);
            }
            k *= h; g *= h;
            return (k, Math.Abs(k - g));
        }
        var segs = new List<(double a, double b, double q, double e)>();
        for (int i = 0; i + 1 < bounds.Count; i++) { var (q, e) = Seg(bounds[i], bounds[i + 1]); segs.Add((bounds[i], bounds[i + 1], q, e)); }
        for (int iter = 0; iter < 2000; iter++)
        {
            double Q = segs.Sum(s => s.q), E = segs.Sum(s => s.e);
            if (double.IsNaN(Q)) return (Q, E, nev);
            if (E <= Math.Max(abstol, reltol * Math.Abs(Q))) break;
            int w = 0; for (int i = 1; i < segs.Count; i++) if (segs[i].e > segs[w].e) w = i;
            var s0 = segs[w]; double m = (s0.a + s0.b) / 2;
            if (m <= s0.a || m >= s0.b) break;
            var l = Seg(s0.a, m); var r = Seg(m, s0.b);
            segs[w] = (s0.a, m, l.q, l.e); segs.Insert(w + 1, (m, s0.b, r.q, r.e));
        }
        return (segs.Sum(s => s.q), segs.Sum(s => s.e), nev);
    }

    static (MathNet.Numerics.LinearAlgebra.Matrix<Complex> U, MathNet.Numerics.LinearAlgebra.Matrix<Complex> T) ComplexSchur(MathNet.Numerics.LinearAlgebra.Matrix<Complex> A)
    {
        // Shifted QR iteration on the full matrix (adequate for the small matrices used in practice)
        int n = A.RowCount;
        var T = A.Clone(); var U = MathNet.Numerics.LinearAlgebra.Matrix<Complex>.Build.DenseIdentity(n);
        int hi = n - 1, it = 0;
        while (hi > 0 && it < 100 * n)
        {
            double scale = Complex.Abs(T[hi, hi]) + Complex.Abs(T[hi - 1, hi - 1]);
            if (Complex.Abs(T[hi, hi - 1]) <= 1e-15 * Math.Max(scale, 1e-300)) { T[hi, hi - 1] = 0; hi--; it = 0; continue; }
            // Wilkinson shift from trailing 2x2
            Complex a = T[hi - 1, hi - 1], b = T[hi - 1, hi], c = T[hi, hi - 1], d = T[hi, hi];
            Complex tr = a + d, det = a * d - b * c, disc = Complex.Sqrt(tr * tr / 4 - det);
            Complex mu1 = tr / 2 + disc, mu2 = tr / 2 - disc;
            Complex mu = Complex.Abs(mu1 - d) < Complex.Abs(mu2 - d) ? mu1 : mu2;
            if (it % 11 == 10) mu += Complex.Abs(T[hi, hi - 1]);   // exceptional shift
            var sub = T.SubMatrix(0, hi + 1, 0, hi + 1) - mu * MathNet.Numerics.LinearAlgebra.Matrix<Complex>.Build.DenseIdentity(hi + 1);
            var qr = sub.QR();
            var Q = MathNet.Numerics.LinearAlgebra.Matrix<Complex>.Build.DenseIdentity(n);
            Q.SetSubMatrix(0, 0, qr.Q);
            T = Q.ConjugateTranspose() * T * Q;
            U = U * Q;
            it++;
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < i; j++) T[i, j] = 0;
        return (U, T);
    }

    static MatrixValue FromCM(MathNet.Numerics.LinearAlgebra.Matrix<Complex> A, bool real)
    {
        var r = new MatrixValue(A.RowCount, A.ColumnCount, NumKind.Double, !real);
        for (int j = 0; j < A.ColumnCount; j++) for (int i = 0; i < A.RowCount; i++) { r.Re[i + j * A.RowCount] = A[i, j].Real; if (r.Im != null) r.Im[i + j * A.RowCount] = A[i, j].Imaginary; }
        return r;
    }

    static int KindSize(NumKind k) => k switch
    {
        NumKind.Double or NumKind.Int64 or NumKind.UInt64 => 8, NumKind.Single or NumKind.Int32 or NumKind.UInt32 => 4,
        NumKind.Int16 or NumKind.UInt16 => 2, NumKind.Char => 1, _ => 1,
    };

    static byte[] ToBytes(MatrixValue x)
    {
        var ms = new MemoryStream();
        foreach (var v in x.Re)
        {
            byte[] b = x.Kind switch
            {
                NumKind.Double => BitConverter.GetBytes(v), NumKind.Single => BitConverter.GetBytes((float)v),
                NumKind.Int8 => new[] { (byte)(sbyte)v }, NumKind.UInt8 or NumKind.Char or NumKind.Logical => new[] { (byte)v },
                NumKind.Int16 => BitConverter.GetBytes((short)v), NumKind.UInt16 => BitConverter.GetBytes((ushort)v),
                NumKind.Int32 => BitConverter.GetBytes((int)v), NumKind.UInt32 => BitConverter.GetBytes((uint)v),
                NumKind.Int64 => BitConverter.GetBytes((long)v), _ => BitConverter.GetBytes((ulong)v),
            };
            ms.Write(b);
        }
        return ms.ToArray();
    }

    static string RowStr(MatrixValue m, int r) => new string(Enumerable.Range(0, m.C).Select(c => (char)m.Re[r + c * m.R]).ToArray());
}
