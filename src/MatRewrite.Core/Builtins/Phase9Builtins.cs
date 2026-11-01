// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;
namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase9()
    {
        RegisterPhase9Linalg();
        RegisterPhase9Colormaps();
        RegisterPhase9Image();
        RegisterPhase9Graphics();
        RegisterPhase9Misc();
    }
}
