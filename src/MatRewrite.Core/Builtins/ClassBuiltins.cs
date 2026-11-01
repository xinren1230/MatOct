// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>classdef introspection builtins.</summary>
public static partial class BuiltinLibrary
{
    static void RegisterClasses()
    {
        Reg1("isobject", a => Bool(a[0] is ObjectValue));
        Reg("properties", (it, a, n) =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : it.FindClass(S(a[0], "properties")) ?? throw new OctaveException("properties: invalid class name");
            var names = ci.AllProps().Where(p => !p.Constant).Select(p => p.Name).ToList();
            if (n > 0) return Out(new CellValue(names.Count, 1, names.Select(x => (Value)Str(x)).ToArray()));
            it.Out.Write($"properties for class {ci.Name}:\n\n" + string.Concat(names.Select(x => $"  {x}\n")) + "\n");
            return Array.Empty<Value>();
        });
        Reg("methods", (it, a, n) =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : it.FindClass(S(a[0], "methods")) ?? throw new OctaveException("methods: invalid class name");
            var names = ci.Chain().SelectMany(c => c.Methods.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (n > 0) return Out(new CellValue(names.Count, 1, names.Select(x => (Value)Str(x)).ToArray()));
            it.Out.Write($"Methods for class {ci.Name}:\n" + string.Join("  ", names) + "\n\n");
            return Array.Empty<Value>();
        });
        Reg1("isprop", a => Bool(a[0] is ObjectValue o && (o.Props.ContainsKey(S(a[1], "isprop")) || o.Cls.Chain().Any(c => c.Constants.ContainsKey(S(a[1], "isprop"))))));
        Reg1("ismethod", a => Bool(a[0] is ObjectValue o && o.Cls.FindMethod(S(a[1], "ismethod"), out _) != null));
        Reg1("isvalid", a => Bool(a[0] is ObjectValue o && o.Cls.IsHandle && !o.Deleted));
        Reg1("metaclass", a => { var o = a[0] as ObjectValue ?? throw new OctaveException("metaclass: argument must be an object"); var s = new StructValue(); s.Set("Name", Str(o.Cls.Name)); return s; });
        Reg1("superclasses", a =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : null;
            var list = ci == null ? new List<string>() : ci.Chain().Skip(1).Select(c => c.Name).ToList();
            if (ci != null && ci.IsHandle) list.Add("handle");
            return new CellValue(list.Count, 1, list.Select(x => (Value)Str(x)).ToArray());
        });
    }

    /// <summary>struct(obj) and fieldnames(obj) support.</summary>
    internal static StructValue ObjectToStruct(ObjectValue o)
    {
        var s = new StructValue();
        foreach (var p in Interpreter.ObjectProps(o)) s.Set(p, o.Props[p]);
        return s;
    }
}
