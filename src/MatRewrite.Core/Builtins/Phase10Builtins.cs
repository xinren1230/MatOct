// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;
namespace MatRewrite.Core.Builtins;

public static partial class BuiltinLibrary
{
    static void RegisterPhase10()
    {
        RegisterPhase10Sparse(); RegisterPhase10Plot(); RegisterPhase11(); RegisterPhase23();
    }
}
