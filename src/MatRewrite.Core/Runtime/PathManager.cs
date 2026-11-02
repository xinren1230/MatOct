// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MatRewrite.Core.Runtime;

/// <summary>MATLAB-style search path model. Resolution order: current folder, then <see cref="Interpreter.SearchPath"/>
/// in order (user folders first, defaults last), then the embedded CoreLib "toolbox". The path is persisted to
/// pathdef.m in the user config folder (%APPDATA%\MatOct, override with MATRW_CONFIG_DIR).</summary>
public static class PathManager
{
    public static string ConfigDir
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("MATRW_CONFIG_DIR");
            var d = !string.IsNullOrEmpty(env) ? env
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create) is { Length: > 0 } ad ? ad : Path.GetTempPath(), "MatOct");
            return d;
        }
    }
    public static string PathDefFile => Path.Combine(ConfigDir, "pathdef.m");
    static string UserPathFile => Path.Combine(ConfigDir, "userpath.txt");

    /// <summary>Default user folder (Documents\MatOct), like MATLAB's Documents\MATLAB.</summary>
    public static string DefaultUserPath
    {
        get
        {
            var doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(doc)) doc = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(doc, "MatOct");
        }
    }

    /// <summary>Current userpath ("" when cleared).</summary>
    public static string UserPath
    {
        get
        {
            try { if (File.Exists(UserPathFile)) return File.ReadAllText(UserPathFile).Trim(); } catch { }
            return DefaultUserPath;
        }
    }
    public static void SetUserPath(string? dir)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(UserPathFile, dir ?? "");
    }

    public static string Normalize(string p)
    {
        var f = Path.GetFullPath(p.Trim());
        if (f.Length > 3) f = f.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return f;
    }
    public static StringComparer Cmp => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static IEnumerable<string> Split(string s) =>
        s.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);

    /// <summary>Is a folder name excluded from genpath (private, @class, +package, hidden/VCS)?</summary>
    public static bool IsSpecialDir(string name) =>
        name == "private" || name.StartsWith('@') || name.StartsWith('+') || name.StartsWith('.') || name == "resources";

    public static List<string> GenPath(string root, IEnumerable<string>? skip = null)
    {
        var res = new List<string>();
        var sk = new HashSet<string>(skip ?? Array.Empty<string>(), Cmp);
        void Walk(string d)
        {
            res.Add(d);
            string[] subs;
            try { subs = Directory.GetDirectories(d); } catch { return; }
            Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
            foreach (var s in subs)
            {
                var n = Path.GetFileName(s);
                if (IsSpecialDir(n) || sk.Contains(n)) continue;
                Walk(s);
            }
        }
        if (Directory.Exists(root)) Walk(Normalize(root));
        return res;
    }

    /// <summary>Adds dirs at the front (begin) or end, moving existing entries (MATLAB semantics).</summary>
    public static void Add(Interpreter it, IEnumerable<string> dirs, bool atEnd)
    {
        var list = dirs.Select(Normalize).ToList();
        foreach (var d in list) it.SearchPath.RemoveAll(x => Cmp.Equals(x, d));
        if (atEnd) it.SearchPath.AddRange(list); else it.SearchPath.InsertRange(0, list);
        it.NotifyPathChanged();
    }
    public static int Remove(Interpreter it, IEnumerable<string> dirs)
    {
        int n = 0;
        foreach (var d0 in dirs) { var d = Normalize(d0); n += it.SearchPath.RemoveAll(x => Cmp.Equals(x, d)); }
        it.NotifyPathChanged();
        return n;
    }
    public static void Set(Interpreter it, IEnumerable<string> dirs)
    {
        var seen = new HashSet<string>(Cmp);
        it.SearchPath.Clear();
        foreach (var d in dirs.Select(Normalize)) if (seen.Add(d)) it.SearchPath.Add(d);
        it.NotifyPathChanged();
    }
    public static string ToPathString(Interpreter it) => string.Join(Path.PathSeparator, it.SearchPath);

    public static void RestoreDefault(Interpreter it)
    {
        var l = new List<string>();
        var up = UserPath;
        if (up.Length > 0 && Directory.Exists(up)) l.Add(up);
        l.AddRange(it.DefaultPath);
        Set(it, l);
    }

    public static void Save(Interpreter it, string? file = null)
    {
        file ??= PathDefFile;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var sb = new StringBuilder();
        sb.AppendLine("function p = pathdef");
        sb.AppendLine("%PATHDEF Search path defaults saved by MatRewrite savepath.");
        sb.AppendLine("%   Saved " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("p = [ ...");
        foreach (var d in it.SearchPath) sb.AppendLine("     '" + d.Replace("'", "''") + Path.PathSeparator + "', ...");
        sb.AppendLine("];");
        sb.AppendLine("end");
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
        File.Move(tmp, file, true);
    }

    public static List<string>? ReadPathDef(string file)
    {
        if (!File.Exists(file)) return null;
        var res = new List<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(file), @"^\s*'((?:[^']|'')*)'", RegexOptions.Multiline))
            res.AddRange(Split(m.Groups[1].Value.Replace("''", "'")));
        return res;
    }

    /// <summary>Startup: load pathdef.m if present (dropping folders that no longer exist), else the default path.
    /// Set MATRW_NO_PATHDEF=1 to skip.</summary>
    public static void LoadStartup(Interpreter it)
    {
        if (Environment.GetEnvironmentVariable("MATRW_NO_PATHDEF") == "1") return;
        try
        {
            var saved = ReadPathDef(PathDefFile);
            if (saved == null) { RestoreDefault(it); return; }
            var l = saved.Where(Directory.Exists).ToList();
            foreach (var d in it.DefaultPath) if (!l.Contains(d, Cmp)) l.Add(d);
            Set(it, l);
        }
        catch (Exception e) { it.Out.WriteLine("warning: could not load pathdef: " + e.Message); }
    }
}
