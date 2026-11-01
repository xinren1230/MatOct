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
        foreach (var r in asm.GetManifestResourceNames())
        {
            if (!r.EndsWith(".m")) continue;
            var parts = r.Split('.');
            var name = parts[^2];
            using var s = asm.GetManifestResourceStream(r)!;
            d[name] = new StreamReader(s).ReadToEnd();
        }
        return d;
    });
    public static string? Get(string name) => all.Value.TryGetValue(name, out var s) ? s : null;
    public static IEnumerable<string> Names => all.Value.Keys;
}
