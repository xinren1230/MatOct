// MatRewrite - GPL-3.0-or-later
// Functions implemented in the MatRewrite language itself and shipped inside the core assembly
// (CoreLib/*.m, embedded resources). Used only when no file of that name is on the path.
using System.Reflection;

namespace MatRewrite.Core.Runtime;

public static class CoreMLib
{
    static readonly Lazy<Dictionary<string, string>> all = new(() =>
    {
        var d = new Dictionary<string, string>();
        var asm = typeof(CoreMLib).Assembly;
        // GNU Octave's own m-file library (GPL-3.0-or-later, bundled unmodified) is loaded first;
        // MatOct's CoreLib overrides it on name clashes.
        foreach (var r in asm.GetManifestResourceNames().OrderBy(r => r.StartsWith("OctaveLib.") ? 0 : 1))
        {
            if (!r.EndsWith(".m")) continue;
            if (r.StartsWith("OctavePkg."))
            {
                using var ps = asm.GetManifestResourceStream(r)!;
                pkg[r["OctavePkg.".Length..^2]] = new StreamReader(ps).ReadToEnd();
                continue;
            }
            var stem = r[..^2];
            var name = stem[(stem.LastIndexOfAny(new[] { '.', '/', '\\' }) + 1)..];
            if (r.StartsWith("OctaveLib."))
            {
                var area = stem["OctaveLib.".Length..].Split('/', '\\')[0];
                if (area is "plot" or "gui" or "image" && !GraphicsSafe.Contains(name)) graphics.Add(name);
            }
            if (!r.StartsWith("OctaveLib.") && octave.Contains(name) && OctaveFirst.Contains(name)) continue;
            using var s = asm.GetManifestResourceStream(r)!;
            d[name] = new StreamReader(s).ReadToEnd();
            if (r.StartsWith("OctaveLib.")) octave.Add(name); else octave.Remove(name);
        }
        return d;
    });
    static readonly HashSet<string> octave = new();
    static readonly HashSet<string> graphics = new();
    /// <summary>True for Octave library m-files from plot/, gui/ or image/ (MatOct renders these natively).</summary>
    public static bool IsOctaveGraphics(string name) { _ = all.Value; return graphics.Contains(name) || NativeFirst.Contains(name); }
    /// <summary>Builtins whose bundled Octave m-file depends on Octave internals MatOct lacks: always use the builtin.</summary>
    /// <summary>plot/gui/image m-files that only compute or set properties (rendering stays native), so the tuned m-file preference applies.</summary>
    static readonly HashSet<string> GraphicsSafe = new() { "setappdata", "getappdata", "isappdata", "rmappdata", "contourc", "ndgrid", "isgraphics", "view", "xlim", "ylim", "zlim", "grid", "shading", "lighting", "material", "hist" };
    static readonly HashSet<string> NativeFirst = new() { "eigs", "sprand", "sprandn", "sprandsym", "help", "lookfor", "doc" };
    /// <summary>Names where Octave's own m-file beats MatOct's CoreLib version on the %! test suite.</summary>
    static readonly HashSet<string> OctaveFirst = new() { "interpn", "interp3", "fplot", "fminsearch", "area", "pcg", "gmres", "bicg", "ilu", "spconvert", "xticks", "yticks", "xticklabels", "yticklabels" };
    static readonly Dictionary<string, string> pkg = new();
    /// <summary>Embedded package function source by qualified name, e.g. "matlab.lang.makeValidName".</summary>
    public static string? GetPackage(string qualified) { _ = all.Value; return pkg.TryGetValue(qualified, out var s) ? s : null; }
    public static bool HasPackagePrefix(string prefix) { _ = all.Value; return pkg.Keys.Any(k => k.StartsWith(prefix + ".")); }
    /// <summary>True when the embedded source for <paramref name="name"/> is GNU Octave's library m-file.</summary>
    public static bool IsOctaveLib(string name) { _ = all.Value; return octave.Contains(name); }
    public static string? Get(string name) => all.Value.TryGetValue(name, out var s) ? s : null;
    public static IEnumerable<string> Names => all.Value.Keys;
}
