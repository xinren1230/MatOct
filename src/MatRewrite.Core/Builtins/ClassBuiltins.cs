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
        Reg1("isobject", a => Bool(a[0] is ObjectValue || a[0] is ObjectArrayValue || a[0] is MapValue));
        Reg("__fieldnames__", (it, a, n) =>
        {
            if (a.Length == 1 && a[0] is MapValue)
                return new Value[] { new CellValue(3, 1, new Value[] { MatrixValue.Str("Count"), MatrixValue.Str("KeyType"), MatrixValue.Str("ValueType") }) };
            return it.CallFunction("fieldnames", a, 1);
        });
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
        Reg("__methods__", (it, a, n) =>
        {
            var ci = a[0] is ObjectValue o ? o.Cls : IsString(a[0]) ? it.FindClass(S(a[0], "__methods__")) : null;
            if (ci == null) return new Value[] { new CellValue(0, 1), Bool(false), new MatrixValue(0, 1, NumKind.Logical) };
            var own = ci.Chain().SelectMany(c => c.Methods.Keys.Where(k => !c.MethodAccess.ContainsKey(k))).Append(ci.Name).Distinct().ToList();
            var hm = new[] { "delete", "eq", "ge", "gt", "le", "lt", "ne" };
            var all = own.Concat(ci.IsHandle ? hm : Array.Empty<string>()).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var fh = new MatrixValue(all.Count, 1, NumKind.Logical);
            for (int i = 0; i < all.Count; i++) fh.Re[i] = ci.IsHandle && hm.Contains(all[i]) && !own.Contains(all[i]) ? 1 : 0;
            return new Value[] { new CellValue(all.Count, 1, all.Select(x => (Value)Str(x)).ToArray()), Bool(true), fh };
        });
        Reg1("isjava", a => Bool(false));
        Reg1("list_in_columns", a =>
        {
            if (a.Length < 1 || a.Length > 3) throw new OctaveException("Invalid call to list_in_columns");
            var items = a[0] is CellValue c ? c.Data.Select(v => S(v, "list_in_columns")).ToList() : IsString(a[0]) ? CharRows(a[0]) : throw new OctaveException("list_in_columns: ARG must be a cellstr or char array");
            int width = a.Length > 1 && a[1].Numel > 0 ? (int)D(a[1], "list_in_columns") : 80;
            string prefix = a.Length > 2 ? S(a[2], "list_in_columns") : "";
            if (items.Count == 0) return Str("\n");
            int w = items.Max(x => x.Length) + 2;
            int ncols = Math.Max(1, (width - prefix.Length) / w);
            int nrows = (items.Count + ncols - 1) / ncols;
            ncols = (items.Count + nrows - 1) / nrows;
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < nrows; r++)
            {
                sb.Append(prefix);
                for (int col = 0; col < ncols; col++)
                {
                    int i = r + col * nrows; if (i >= items.Count) break;
                    bool last = col == ncols - 1 || r + (col + 1) * nrows >= items.Count;
                    sb.Append(last ? items[i] : items[i].PadRight(w));
                }
                sb.Append('\n');
            }
            return Str(sb.ToString());
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

    static List<string> CharRows(Value v) { var m = (MatrixValue)v; var l = new List<string>(); for (int r = 0; r < m.R; r++) { var cs = new char[m.C]; for (int j = 0; j < m.C; j++) cs[j] = (char)m.Re[r + j * m.R]; l.Add(new string(cs).TrimEnd()); } return l; }
    /// <summary>struct(obj) and fieldnames(obj) support.</summary>
    internal static StructValue ObjectToStruct(ObjectValue o)
    {
        var s = new StructValue();
        foreach (var p in Interpreter.ObjectProps(o)) s.Set(p, o.Props[p]);
        return s;
    }
}
