// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins;

/// <summary>classdef introspection builtins.</summary>
public static partial class BuiltinLibrary
{
    static void Wrap(string name, Func<BuiltinFn, Interpreter, Value[], int, Value[]> f)
    {
        if (!cur!.Builtins.TryGetValue(name, out var orig)) orig = (it, a, n) => throw new OctaveException($"'{name}' undefined");
        cur.Builtins[name] = (it, a, n) => f(orig, it, a, n);
    }

    static void RegisterClasses()
    {
        Reg1("isobject", a => Bool(a[0] is ObjectValue || a[0] is ObjectArrayValue));
        Reg("addlistener", (it, a, n) =>
        {
            if (a.Length < 3 || a[0] is not ObjectValue src) throw new OctaveException("addlistener: usage addlistener(obj, 'Event', callback)");
            return Out(it.AddListener(src, S(a[1], "addlistener"), a[2]));
        });
        Reg("listener", (it, a, n) =>
        {
            if (a.Length < 3 || a[0] is not ObjectValue src) throw new OctaveException("listener: usage listener(obj, 'Event', callback)");
            var l = it.AddListener(src, S(a[1], "listener"), a[2]);
            return Out(l);
        });
        Reg("notify", (it, a, n) =>
        {
            if (a.Length < 2 || a[0] is not ObjectValue src) throw new OctaveException("notify: usage notify(obj, 'Event'[, eventData])");
            it.Notify(src, S(a[1], "notify"), a.Length > 2 ? a[2] : null);
            return Array.Empty<Value>();
        });
        Reg("events", (it, a, n) =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : it.FindClass(S(a[0], "events")) ?? throw new OctaveException("events: invalid class name");
            var names = ci.Chain().SelectMany(c => c.Events).Distinct().ToList();
            if (n > 0) return Out(new CellValue(names.Count, 1, names.Select(x => (Value)Str(x)).ToArray()));
            it.Out.Write($"Events for class {ci.Name}:\n" + string.Concat(names.Select(x => $"  {x}\n")) + "\n");
            return Array.Empty<Value>();
        });
        Reg("enumeration", (it, a, n) =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : it.FindClass(S(a[0], "enumeration")) ?? throw new OctaveException("enumeration: invalid class name");
            if (!ci.IsEnum) throw new OctaveException($"enumeration: class '{ci.Name}' is not an enumeration");
            var mem = it.EnumMembers(ci);
            if (n > 0)
            {
                var arr = new ObjectArrayValue(ci, mem.Count, 1, mem.Cast<ObjectValue?>().ToArray());
                var names = new CellValue(mem.Count, 1, mem.Select(x => (Value)Str(x.EnumName!)).ToArray());
                return new Value[] { arr.Simplify(), names };
            }
            it.Out.Write($"Enumeration members for class '{ci.Name}':\n" + string.Concat(mem.Select(x => $"    {x.EnumName}\n")) + "\n");
            return Array.Empty<Value>();
        });
        // object-aware wrappers around existing builtins
        Wrap("delete", (orig, it, a, n) =>
        {
            if (a.Length >= 1 && a[0] is ObjectValue o) { if (!o.Cls.IsHandle) throw new OctaveException("delete: only handle objects can be deleted"); o.Deleted = true; return Array.Empty<Value>(); }
            if (a.Length >= 1 && a[0] is ObjectArrayValue oa) { for (int i = 0; i < oa.Numel; i++) oa.At(i).Deleted = true; return Array.Empty<Value>(); }
            return orig(it, a, n);
        });
        Wrap("char", (orig, it, a, n) =>
            a.Length == 1 && a[0] is ObjectValue o && o.EnumName != null ? Out(Str(o.EnumName)) : orig(it, a, n));
        Wrap("class", (orig, it, a, n) =>
        {
            if (a.Length >= 2 && a[0] is StructValue st)
            {
                // old-style @folder class: class(struct, 'Name')
                var nm = S(a[1], "class");
                var ci = it.OldStyleClass(nm);
                var obj = new ObjectValue(ci);
                foreach (var f in st.Fields)
                {
                    obj.Props[f] = st.Get(f);
                    if (!ci.Props.Any(p => p.Name == f)) ci.Props.Add(new MatRewrite.Core.Parsing.PropDef(f, null, false));
                }
                return Out(obj);
            }
            return orig(it, a, n);
        });
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
