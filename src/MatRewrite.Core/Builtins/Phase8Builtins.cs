// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase8()
    {
        RegisterPhase8Time();
        RegisterPhase8Poly();
        RegisterPhase8Linalg();
        RegisterPhase8Special();
        RegisterPhase8Misc(); RegisterPhase8Interp(); RegisterPhase8Interp2(); RegisterPhase8Gallery(); RegisterPhase8Decic();
    }
}
