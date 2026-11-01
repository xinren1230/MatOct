// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    public const string Version = "0.1.0";

    /// <summary>Build the message for error()/warning(): (id, fmt, args...) or (fmt, args...).</summary>
    static (string id, string msg) ErrorArgs(Value[] a, string fn)
    {
        if (a.Length == 0) throw new OctaveException($"Invalid call to {fn}");
        string first = S(a[0], fn);
        string id = "";
        int start = 0;
        if (a.Length > 1 && System.Text.RegularExpressions.Regex.IsMatch(first, @"^[A-Za-z][\w-]*(:[\w-]+)+$")) { id = first; start = 1; }
        string fmt = S(a[start], fn);
        var rest = a.Skip(start + 1).ToList();
        // single-argument form: the template is still processed for escapes but not for format conversions
        string msg = (rest.Count == 0 && start == 0 && a.Length == 1) ? Printf.Format(Printf.Escapes(fmt), rest) : Printf.Format(Printf.Escapes(fmt), rest);
        return (id, msg);
    }

    static void RegisterSystem()
    {
        Reg("error", (it, a, n) =>
        {
            if (a.Length == 1 && a[0] is StructValue es)
            {
                var m = es.HasField("message") ? S(es.Get("message"), "error") : "unspecified error";
                var id0 = es.HasField("identifier") ? S(es.Get("identifier"), "error") : "";
                throw new OctaveException(m, id0);
            }
            var (id, msg) = ErrorArgs(a, "error");
            throw new OctaveException(msg.TrimEnd('\n'), id);
        });
        Reg("warning", (it, a, n) =>
        {
            if (a.Length == 0) return Array.Empty<Value>();
            if (IsString(a[0]))
            {
                var s0 = S(a[0], "warning");
                if (s0 is "on" or "off" or "query" or "error")
                {
                    var id = a.Length > 1 && IsString(a[1]) ? S(a[1], "warning") : "all";
                    if (s0 == "off") { if (id == "all") { it.DisabledWarnings.Clear(); it.EnabledWarnings.Clear(); } it.DisabledWarnings.Add(id); it.EnabledWarnings.Remove(id); }
                    else if (s0 == "on") { if (id == "all") { it.DisabledWarnings.Clear(); it.EnabledWarnings.Clear(); } else { it.DisabledWarnings.Remove(id); it.EnabledWarnings.Add(id); } }
                    return Array.Empty<Value>();
                }
            }
            var (wid, msg) = ErrorArgs(a, "warning");
            it.Warning(msg.TrimEnd('\n'), wid ?? "");
            return Array.Empty<Value>();
        });
        Reg("lastwarn", (it, a, n) =>
        {
            var res = new Value[] { Str(it.LastWarning), Str(it.LastWarningId) };
            if (a.Length > 0) { it.LastWarning = S(a[0], "lastwarn"); it.LastWarningId = a.Length > 1 ? S(a[1], "lastwarn") : ""; }
            return n <= 1 ? new[] { res[0] } : res;
        });
        Reg("rethrow", (it, a, n) =>
        {
            var es = a[0] as StructValue ?? throw new OctaveException("rethrow: ERR must be a struct");
            throw new OctaveException(S(es.Get("message"), "rethrow"), es.HasField("identifier") ? S(es.Get("identifier"), "rethrow") : "");
        });
        Reg("lasterr", (it, a, n) => Out(Str(it.LastError)));
        Reg("print_usage", (it, a, n) => throw new OctaveException($"Invalid call to {it.Current.Func?.Name ?? "function"}", "Octave:invalid-fun-call"));
        Reg("eval", (it, a, n) =>
        {
            var code = S(a[0], "eval");
            try
            {
                if (n > 0)
                {
                    it.EvalInCurrentScope("__eval_tmp__ = " + code + ";");
                    var v = it.GetVar("__eval_tmp__")!; it.ClearVar("__eval_tmp__");
                    return Out(v);
                }
                it.EvalInCurrentScope(code);
            }
            catch (OctaveException ex) when (a.Length > 1 && ex is not InterruptException)
            {
                it.LastError = ex.Message;
                it.EvalInCurrentScope(S(a[1], "eval"));
            }
            return Array.Empty<Value>();
        });
        Reg("evalc", (it, a, n) =>
        {
            var save = it.Out; var sw = new StringWriter();
            it.Out = sw;
            try { it.EvalInCurrentScope(S(a[0], "evalc")); }
            finally { it.Out = save; }
            return Out(Str(sw.ToString()));
        });
        Reg("feval", (it, a, n) =>
        {
            if (a.Length == 0) throw new OctaveException("Invalid call to feval");
            return it.CallCallable(a[0], a.Skip(1).ToArray(), n);
        });
        Reg("builtin", (it, a, n) =>
        {
            var name = S(a[0], "builtin");
            if (!it.Builtins.TryGetValue(name, out var f)) throw new OctaveException($"builtin: lookup for symbol '{name}' failed");
            return f(it, a.Skip(1).ToArray(), n);
        });
        Reg1("func2str", a => a[0] is FunctionHandleValue fh ? Str(fh.IsAnonymous ? fh.ToString() : fh.Name!) : throw new OctaveException("func2str: FCN_HANDLE argument must be a valid function handle"));
        RegI("str2func", (it, a) =>
        {
            var s = S(a[0], "str2func").Trim();
            if (s.StartsWith("@"))
            {
                it.EvalInCurrentScope("__str2func_tmp__ = " + s + ";");
                var v = it.GetVar("__str2func_tmp__")!; it.ClearVar("__str2func_tmp__"); return v;
            }
            return new FunctionHandleValue { Name = s };
        });
        RegI("exist", (it, a) =>
        {
            var name = S(a[0], "exist");
            string type = a.Length > 1 ? S(a[1], "exist") : "";
            if ((type == "" || type == "var") && it.IsVariable(name)) return Num(1);
            if (type == "var") return Num(0);
            if (type is "" or "file" or "dir")
            {
                if (type != "dir" && it.FindFile(name) != null) return Num(2);
                if (type != "dir" && File.Exists(name)) return Num(2);
                if (Directory.Exists(name) && type != "file") return Num(7);
            }
            if ((type == "" || type == "builtin") && it.Builtins.ContainsKey(name)) return Num(type == "builtin" ? 5 : 5);
            if (type == "" && it.CommandLineFunctions.ContainsKey(name)) return Num(103);
            return Num(0);
        });
        Reg1("isvarname", a => Bool(IsString(a[0]) && System.Text.RegularExpressions.Regex.IsMatch(S(a[0], "isvarname"), @"^[A-Za-z_]\w*$") && !Lexing.Lexer.IsKeyword(S(a[0], "isvarname"))));
        Reg1("iskeyword", a => Bool(Lexing.Lexer.IsKeyword(S(a[0], "iskeyword"))));
        Reg("tic", (it, a, n) =>
        {
            double now = it.Clock.Elapsed.TotalSeconds;
            if (n > 0) return Out(Num(Math.Floor(now * 1e6)));
            it.TicTime = now; return Array.Empty<Value>();
        });
        Reg("toc", (it, a, n) =>
        {
            double now = it.Clock.Elapsed.TotalSeconds;
            double start = a.Length > 0 ? D(a[0], "toc") / 1e6 : it.TicTime;
            if (double.IsNaN(start)) throw new OctaveException("toc: function called before timer initialization with tic()");
            double el = now - start;
            if (n > 0) return Out(Num(el));
            it.Out.WriteLine($"Elapsed time is {Printf.FormatG(el, 6)} seconds.");
            return Array.Empty<Value>();
        });
        RegI("clock", (it, a) => { var t = DateTime.Now; return MatrixValue.Row(new double[] { t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second + t.Millisecond / 1000.0 }); });
        RegI("cputime", (it, a) => Num(System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds));
        RegI("time", (it, a) => Num((DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds));
        RegI("now", (it, a) => Num((DateTime.Now - new DateTime(1, 1, 1)).TotalDays + 367));
        RegI("date", (it, a) => Str(DateTime.Now.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture)));
        Reg0("pause", (it, a) =>
        {
            if (a.Length == 0) { it.InputReader(""); return; }
            var until = DateTime.UtcNow.AddSeconds(Math.Max(0, D(a[0], "pause")));
            while (DateTime.UtcNow < until) { it.CheckInterrupt(); if (!it.Graphics.PendingCallbacks.IsEmpty) DrainCallbacks(it); Thread.Sleep(Math.Max(1, Math.Min(20, (int)(until - DateTime.UtcNow).TotalMilliseconds))); }
            it.CheckInterrupt();
        });
        Reg0("format", (it, a) => Display.LongFormat = a.Length > 0 && S(a[0], "format").Equals("long", StringComparison.OrdinalIgnoreCase));
        Reg0("more", (it, a) => { });
        Reg0("beep", (it, a) => { });
        Reg0("clc", (it, a) => { try { if (!Console.IsOutputRedirected) Console.Clear(); } catch (IOException) { } });
        Reg0("fflush", (it, a) => it.Out.Flush());
        Reg1("stdout", a => Num(1));
        Reg1("stderr", a => Num(2));
        Reg("who", (it, a, n) =>
        {
            var names = it.VariableNames.ToList();
            if (n > 0) return Out(CellValue.ColOf(names.Select(Str)));
            if (names.Count == 0) return Array.Empty<Value>();
            it.Out.WriteLine("Variables visible from the current scope:\n");
            it.Out.WriteLine(string.Join("  ", names));
            it.Out.WriteLine();
            return Array.Empty<Value>();
        });
        Reg0("whos", (it, a) =>
        {
            var names = it.VariableNames.ToList();
            if (names.Count == 0) return;
            it.Out.WriteLine("Variables visible from the current scope:\n");
            it.Out.WriteLine("  Attr   Name        Size                     Bytes  Class");
            it.Out.WriteLine("  ====   ====        ====                     =====  =====");
            long total = 0;
            foreach (var nm in names)
            {
                var v = it.GetVar(nm)!;
                long bytes = v is MatrixValue m ? m.Numel * (m.IsChar || m.IsLogical ? 1 : 8) * (m.IsComplex ? 2 : 1) : 0;
                total += bytes;
                string attr = v is MatrixValue mm && mm.IsComplex ? "c" : " ";
                it.Out.WriteLine($"    {attr,-4} {nm,-11} {v.DimString,-24} {bytes,5}  {v.ClassName}");
            }
            it.Out.WriteLine($"\nTotal is {names.Sum(x => (long)it.GetVar(x)!.Numel)} elements using {total} bytes\n");
        });
        Reg0("clear", (it, a) =>
        {
            var names = a.Select(v => S(v, "clear")).ToList();
            if (names.Count == 0 || names.Any(x => x is "all" or "-all" or "-a" or "variables" or "-v" or "-variables"))
            {
                foreach (var nm in it.VariableNames.ToList()) it.ClearVar(nm);
                if (names.Any(x => x is "all" or "-all" or "-a")) it.CommandLineFunctions.Clear();
                return;
            }
            if (names.Any(x => x is "functions" or "-f" or "-functions")) { it.CommandLineFunctions.Clear(); return; }
            foreach (var pat in names)
            {
                if (pat.Contains('*') || pat.Contains('?'))
                {
                    var re = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$");
                    foreach (var nm in it.VariableNames.Where(x => re.IsMatch(x)).ToList()) it.ClearVar(nm);
                }
                else it.ClearVar(pat);
            }
        });
        Reg0("clearvars", (it, a) => { foreach (var nm in it.VariableNames.ToList()) it.ClearVar(nm); });
        Reg("input", (it, a, n) =>
        {
            it.Out.Flush();
            var line = it.InputReader(a.Length > 0 ? S(a[0], "input") : "") ?? "";
            if (a.Length > 1 && S(a[1], "input") == "s") return Out(Str(line));
            if (string.IsNullOrWhiteSpace(line)) return Out(new MatrixValue(0, 0));
            it.EvalInCurrentScope("__input_tmp__ = " + line + ";");
            var v = it.GetVar("__input_tmp__")!; it.ClearVar("__input_tmp__"); return Out(v);
        });
        Reg0("keyboard", (it, a) =>
        {
            if (a.Length > 0) { it.Out.Write(S(a[0], "keyboard")); }
            it.Pause(new PauseInfo(it.CurrentFile, it.Current.FrameName, it.Current.Line, "keyboard"));
        });
        Reg0("exit", (it, a) => it.Quit(a.Length > 0 ? I(a[0], "exit") : 0));
        Reg0("quit", (it, a) => it.Quit(a.Length > 0 ? I(a[0], "quit") : 0));
        Reg("addpath", (it, a, n) =>
        {
            foreach (var v in a)
            {
                if (!IsString(v)) continue;
                var s = S(v, "addpath");
                if (s is "-end" or "-begin") continue;
                foreach (var p in s.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    var full = Path.GetFullPath(p);
                    it.SearchPath.Remove(full);
                    if (a.Any(x => IsString(x) && S(x, "addpath") == "-end")) it.SearchPath.Add(full); else it.SearchPath.Insert(0, full);
                }
            }
            return Array.Empty<Value>();
        });
        Reg0("rmpath", (it, a) => { foreach (var v in a) it.SearchPath.Remove(Path.GetFullPath(S(v, "rmpath"))); });
        RegI("path", (it, a) => Str(string.Join(Path.PathSeparator, new[] { "." }.Concat(it.SearchPath))));
        RegI("pwd", (it, a) => Str(Directory.GetCurrentDirectory()));
        Reg("cd", (it, a, n) =>
        {
            if (a.Length > 0)
            {
                var d = S(a[0], "cd");
                if (!Directory.Exists(d)) throw new OctaveException($"cd: {d}: No such file or directory");
                Directory.SetCurrentDirectory(d);
            }
            return n > 0 || a.Length == 0 ? Out(Str(Directory.GetCurrentDirectory())) : Array.Empty<Value>();
        });
        Reg1("version", a => Str(Version));
        Reg1("OCTAVE_VERSION", a => Str(Version));
        Reg1("computer", a => Str(Environment.OSVersion.Platform == PlatformID.Win32NT ? "x86_64-w64-mingw32" : "x86_64-pc-linux-gnu"));
        Reg1("ispc", a => Bool(OperatingSystem.IsWindows()));
        Reg1("isunix", a => Bool(!OperatingSystem.IsWindows()));
        Reg1("ismac", a => Bool(OperatingSystem.IsMacOS()));
        Reg1("getenv", a => Str(Environment.GetEnvironmentVariable(S(a[0], "getenv")) ?? ""));
        Reg0("setenv", (it, a) => Environment.SetEnvironmentVariable(S(a[0], "setenv"), a.Length > 1 ? S(a[1], "setenv") : ""));
        Reg0("putenv", (it, a) => Environment.SetEnvironmentVariable(S(a[0], "putenv"), a.Length > 1 ? S(a[1], "putenv") : ""));
        Reg1("filesep", a => Str(Path.DirectorySeparatorChar.ToString()));
        Reg1("pathsep", a => Str(Path.PathSeparator.ToString()));
        Reg1("fullfile", a => Str(Path.Combine(a.Select(v => S(v, "fullfile")).ToArray())));
        Reg("fileparts", (it, a, n) =>
        {
            var p = S(a[0], "fileparts");
            var dir = Path.GetDirectoryName(p) ?? ""; var nm = Path.GetFileNameWithoutExtension(p); var ext = Path.GetExtension(p);
            return Out(Str(dir), Str(nm), Str(ext));
        });
        RegI("mfilename", (it, a) =>
        {
            var f = it.Current.Func?.File;
            if (f == null) return Str("");
            if (a.Length > 0 && S(a[0], "mfilename") == "fullpath") return Str(Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f)));
            return Str(Path.GetFileNameWithoutExtension(f));
        });
        Reg0("run", (it, a) => it.RunFileHere(S(a[0], "run")));
        Reg0("source", (it, a) => it.RunFileHere(S(a[0], "source")));
        Reg1("isdeployed", a => Bool(false));
        Reg1("is_valid_file_id", a => Bool(a[0] is MatrixValue m && m.Numel == 1 && m.Re[0] is 0 or 1 or 2));
        Reg0("assert", (it, a) =>
        {
            if (a.Length == 1)
            {
                if (!Ops.IsTrue(a[0])) throw new OctaveException("assert (cond) failed");
                return;
            }
            if (IsString(a[1]) && !(IsString(a[0])))
            {
                if (!Ops.IsTrue(a[0])) throw new OctaveException(Printf.Format(Printf.Escapes(S(a[1], "assert")), a.Skip(2).ToList()));
                return;
            }
            var obs = a[0]; var exp = a[1];
            double tol = a.Length > 2 ? D(a[2], "assert") : 0;
            if (tol == 0) { if (!ValuesEqual(obs, exp)) throw new OctaveException("ASSERT errors for:  assert (cond,expected,tol)\n\n  Observed and expected values differ"); return; }
            var mo = M(obs, "assert"); var me = M(exp, "assert");
            if (mo.Numel != me.Numel && me.Numel != 1) throw new OctaveException("ASSERT errors: dimensions mismatch");
            for (int k = 0; k < mo.Numel; k++)
            {
                double e = me.Re[me.Numel == 1 ? 0 : k], o = mo.Re[k];
                double err = Math.Abs(o - e), lim = tol < 0 ? Math.Abs(tol * e) : tol;
                if (err > lim) throw new OctaveException($"ASSERT errors for:  assert (cond,expected,tol)\n\n  Location  |  Observed  |  Expected  |  Reason\n    ({k + 1})       {o}        {e}       Abs err {err} exceeds tol {lim}");
            }
        });
        Reg("nthargout", (it, a, n) =>
        {
            int k = I(a[0], "nthargout");
            var r = it.CallCallable(a[1], a.Skip(2).ToArray(), k);
            return Out(r[k - 1]);
        });
        Reg1("validateattributes", a => MatrixValue.Empty());
        cur!.Builtins.Remove("validateattributes");
        Reg0("validateattributes", (it, a) => { });
        RegI("ans", (it, a) => it.GetVar("ans") ?? throw new OctaveException("'ans' undefined"));
        Reg1("inputname", a => Str(""));
        Reg1("nproc", a => Num(Environment.ProcessorCount));
    }
}
