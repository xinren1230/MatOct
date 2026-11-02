// MatRewrite - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>Phase 12: MATLAB-style search path management (addpath/rmpath/path/genpath/savepath/which ...).</summary>
public static partial class BuiltinLibrary
{
    static Value CellCol(IEnumerable<string> xs) { var l = xs.ToList(); return new CellValue(l.Count, 1, l.Select(h => (Value)Str(h)).ToArray()); }

    static IEnumerable<string> PathArgs(Value v, string fn)
    {
        if (v is CellValue c) return c.Data.SelectMany(x => PathManager.Split(S(x, fn)));
        return PathManager.Split(S(v, fn));
    }

    static void RegisterPaths()
    {
        Reg("addpath", (it, a, n) =>
        {
            var old = PathManager.ToPathString(it);
            bool atEnd = false; var dirs = new List<string>();
            foreach (var v in a)
            {
                if (v is MatrixValue m && !m.IsChar) { atEnd = m.Numel > 0 && m.Re[0] == 1; continue; }
                var s = v is CellValue ? null : S(v, "addpath");
                if (s is "-end" or "-END") { atEnd = true; continue; }
                if (s is "-begin" or "-BEGIN") { atEnd = false; continue; }
                if (s is "-frozen") continue;
                dirs.AddRange(PathArgs(v, "addpath"));
            }
            if (dirs.Count == 0) throw new OctaveException("Invalid call to addpath");
            var ok = new List<string>();
            foreach (var d in dirs)
            {
                if (!Directory.Exists(d)) { it.Warning($"addpath: {d}: No such file or directory"); continue; }
                ok.Add(d);
            }
            PathManager.Add(it, ok, atEnd);
            return n > 0 ? Out(Str(old)) : Array.Empty<Value>();
        });
        Reg("rmpath", (it, a, n) =>
        {
            var old = PathManager.ToPathString(it);
            if (a.Length == 0) throw new OctaveException("Invalid call to rmpath");
            foreach (var v in a)
                foreach (var d in PathArgs(v, "rmpath"))
                    if (PathManager.Remove(it, new[] { d }) == 0) it.Warning($"rmpath: {d}: not found");
            return n > 0 ? Out(Str(old)) : Array.Empty<Value>();
        });
        Reg("path", (it, a, n) =>
        {
            if (a.Length > 0)
                PathManager.Set(it, a.SelectMany(v => PathArgs(v, "path")).Where(d => d != "."));
            if (n > 0 || a.Length > 0) return n > 0 ? Out(Str(PathManager.ToPathString(it))) : Array.Empty<Value>();
            it.Out.WriteLine("\n\t\tMATLABPATH\n");
            it.Out.WriteLine("\t" + Directory.GetCurrentDirectory() + "  (current folder)");
            foreach (var d in it.SearchPath) it.Out.WriteLine("\t" + d);
            it.Out.WriteLine($"\t<matoct>/toolbox/builtin        ({it.Builtins.Count} built-in functions)");
            it.Out.WriteLine($"\t<matoct>/toolbox/core           ({CoreMLib.Names.Count()} library .m functions)\n");
            return Array.Empty<Value>();
        });
        Reg("genpath", (it, a, n) =>
        {
            if (a.Length < 1) throw new OctaveException("Invalid call to genpath");
            var root = S(a[0], "genpath");
            var skip = a.Skip(1).Select(v => S(v, "genpath"));
            return Out(Str(string.Join(Path.PathSeparator, PathManager.GenPath(root, skip))));
        });
        Reg("savepath", (it, a, n) =>
        {
            try { PathManager.Save(it, a.Length > 0 ? S(a[0], "savepath") : null); return n > 0 ? Out(Num(0)) : Array.Empty<Value>(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (n > 0) return Out(Num(1));
                throw new OctaveException("savepath: unable to save path: " + e.Message);
            }
        });
        Reg0("restoredefaultpath", (it, a) => PathManager.RestoreDefault(it));
        Reg("pathdef", (it, a, n) =>
        {
            var l = PathManager.ReadPathDef(PathManager.PathDefFile);
            if (l == null) { var up = PathManager.UserPath; l = new List<string>(); if (up.Length > 0 && Directory.Exists(up)) l.Add(up); l.AddRange(it.DefaultPath); }
            return Out(Str(string.Join(Path.PathSeparator, l)));
        });
        Reg("userpath", (it, a, n) =>
        {
            if (a.Length == 0) return Out(Str(PathManager.UserPath));
            var s = S(a[0], "userpath");
            var old = PathManager.UserPath;
            if (s == "clear")
            {
                PathManager.SetUserPath("");
                if (old.Length > 0) PathManager.Remove(it, new[] { old });
            }
            else if (s == "reset")
            {
                PathManager.SetUserPath(null); File.Delete(Path.Combine(PathManager.ConfigDir, "userpath.txt"));
                if (old.Length > 0) PathManager.Remove(it, new[] { old });
                var d = PathManager.DefaultUserPath; Directory.CreateDirectory(d); PathManager.Add(it, new[] { d }, false);
            }
            else
            {
                if (!Directory.Exists(s)) throw new OctaveException($"userpath: {s}: No such folder");
                var d = PathManager.Normalize(s);
                PathManager.SetUserPath(d);
                if (old.Length > 0) PathManager.Remove(it, new[] { old });
                PathManager.Add(it, new[] { d }, false);
            }
            return Array.Empty<Value>();
        });
        Reg0("rehash", (it, a) => it.Rehash());
        Reg0("pathtool", (it, a) => { if (it.PathToolRequested != null) it.PathToolRequested(); else Builtins_path(it); });
        Reg("which", (it, a, n) =>
        {
            bool all = a.Any(v => IsString(v) && S(v, "which") == "-all");
            var names = a.Where(v => !(IsString(v) && S(v, "which") == "-all")).Select(v => S(v, "which")).ToList();
            var results = new List<Value>();
            foreach (var name in names)
            {
                var hits = WhichAll(it, name);
                if (n > 0) { results.Add(all ? CellCol(hits.Select(h => h.file)) : Str(hits.Count > 0 ? hits[0].file : "")); continue; }
                if (hits.Count == 0) { it.Out.WriteLine($"'{name}' not found."); continue; }
                foreach (var (file, desc) in all ? hits : hits.Take(1))
                    it.Out.WriteLine(desc);
            }
            return n > 0 ? results.Take(Math.Max(1, n)).ToArray() : Array.Empty<Value>();
        });
        Reg("what", (it, a, n) =>
        {
            var d = a.Length > 0 ? S(a[0], "what") : Directory.GetCurrentDirectory();
            if (!Directory.Exists(d)) d = it.SearchPath.FirstOrDefault(p => Path.GetFileName(p).Equals(d, StringComparison.OrdinalIgnoreCase)) ?? d;
            var files = Directory.Exists(d) ? Directory.GetFiles(d, "*.m").Select(Path.GetFileName).OrderBy(x => x).ToList() : new List<string?>();
            if (n > 0)
            {
                var s = new StructValue(1, 1, new List<string> { "path", "m" });
                s.Elems[0]["path"] = Str(d); s.Elems[0]["m"] = CellCol(files!); return Out(s);
            }
            it.Out.WriteLine($"\nMATLAB Code files in folder {d}:\n");
            foreach (var f in files) it.Out.WriteLine("    " + f);
            it.Out.WriteLine();
            return Array.Empty<Value>();
        });
        Reg("cd", (it, a, n) =>
        {
            if (a.Length > 0)
            {
                var d = S(a[0], "cd");
                if (d == "~") d = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!Directory.Exists(d)) throw new OctaveException($"cd: {d}: No such file or directory");
                Directory.SetCurrentDirectory(d);
                it.NotifyPathChanged();
            }
            return n > 0 || a.Length == 0 ? Out(Str(Directory.GetCurrentDirectory())) : Array.Empty<Value>();
        });
        Reg("chdir", (it, a, n) => it.Builtins["cd"](it, a, n));
    }

    static void Builtins_path(Interpreter it) => it.Builtins["path"](it, Array.Empty<Value>(), 0);

    /// <summary>All definitions of name in resolution order: variable, private/command-line, current folder + path, built-in, CoreLib.</summary>
    internal static List<(string file, string desc)> WhichAll(Interpreter it, string name)
    {
        var res = new List<(string, string)>();
        if (it.IsVariable(name)) res.Add(("variable", $"'{name}' is a variable."));
        if (it.CommandLineFunctions.ContainsKey(name)) res.Add(("command-line function", $"'{name}' is a command-line function"));
        var cf = it.CurrentFile;
        if (cf != null)
        {
            var d = Path.GetDirectoryName(cf);
            if (d != null)
            {
                var pf = Path.Combine(Path.GetFileName(d) == "private" ? d : Path.Combine(d, "private"), name + ".m");
                if (File.Exists(pf)) res.Add((pf, pf + "  % Private to " + Path.GetFileName(d)));
            }
        }
        bool builtinFirst = it.PreferBuiltins && it.Builtins.ContainsKey(name) && !Interpreter.MFileFirst.Contains(name);
        if (builtinFirst) res.Add(("built-in", $"built-in ({name})"));
        foreach (var dir in new[] { Directory.GetCurrentDirectory() }.Concat(it.SearchPath))
        {
            foreach (var p in new[] { Path.Combine(dir, name + ".m"), Path.Combine(dir, "@" + name, name + ".m") })
                if (File.Exists(p)) res.Add((p, res.Count > 0 ? p + "  % Shadowed" : p));
        }
        if (it.Builtins.ContainsKey(name) && !builtinFirst) res.Add(("built-in", $"built-in ({name})" + (res.Count > 0 ? "  % Shadowed" : "")));
        if (CoreMLib.Get(name) != null) res.Add(("<core>/" + name + ".m", $"<core>/{name}.m  (MatRewrite CoreLib)" + (res.Count > 0 ? "  % Shadowed" : "")));
        return res;
    }
}
