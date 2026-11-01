// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Parsing;

namespace MatRewrite.Core.Runtime;

/// <summary>A loaded classdef definition.</summary>
public sealed class ClassInfo
{
    public string Name = "";
    public ClassInfo? Super;
    public bool IsHandle;
    public string? File;
    public List<PropDef> Props = new();
    public Dictionary<string, UserFunction> Methods = new();
    public HashSet<string> Static = new();
    public Dictionary<string, Value> Constants = new();
    public Dictionary<string, string> MethodAccess = new();
    public HashSet<string> Events = new();
    public List<EnumDef> Enums = new();
    public Dictionary<string, ObjectValue> EnumCache = new();
    public bool IsOldStyle;     // @folder class built with class(struct, name)
    public string? Folder;      // @folder directory
    public PropDef? FindProp(string name, out ClassInfo? owner)
    {
        foreach (var c in Chain()) { var p = c.Props.FirstOrDefault(x => x.Name == name); if (p != null) { owner = c; return p; } }
        owner = null; return null;
    }
    public bool IsEnum => Chain().Any(c => c.Enums.Count > 0);
    public bool HasEvent(string e) => Chain().Any(c => c.Events.Contains(e));

    public IEnumerable<ClassInfo> Chain() { for (var c = this; c != null; c = c.Super) yield return c; }
    public UserFunction? FindMethod(string name, out ClassInfo? owner)
    {
        foreach (var c in Chain()) if (c.Methods.TryGetValue(name, out var f)) { owner = c; return f; }
        owner = null; return null;
    }
    public bool IsA(string name) => Chain().Any(c => c.Name == name) || (name == "handle" && IsHandle);
    public IEnumerable<PropDef> AllProps() => Chain().Reverse().SelectMany(c => c.Props).GroupBy(p => p.Name).Select(g => g.Last());
}

/// <summary>Instance of a classdef class. Handle classes share this object; value classes are copied on write.</summary>
public sealed class ObjectValue : Value
{
    public ClassInfo Cls;
    public Dictionary<string, Value> Props = new();
    public bool Deleted;
    public string? EnumName;
    public Dictionary<string, List<ObjectValue>>? Listeners;
    public ObjectValue(ClassInfo c) { Cls = c; }
    public override int Rows => 1;
    public override int Cols => 1;
    public override string ClassName => Cls.Name;
    public ObjectValue CopyForWrite() => Cls.IsHandle ? this : new ObjectValue(Cls) { Props = new Dictionary<string, Value>(Props), EnumName = EnumName };
}

/// <summary>Array of objects of one class (r x c), e.g. [a, b] or objs(3) = Foo().</summary>
public sealed class ObjectArrayValue : ArrayValue
{
    public ClassInfo Cls;
    public ObjectValue?[] Items;
    public static Func<ClassInfo, ObjectValue>? DefaultFactory;
    public ObjectArrayValue(ClassInfo cls, int r, int c, ObjectValue?[]? items = null) { Cls = cls; R = r; C = c; Items = items ?? new ObjectValue?[r * c]; }
    public static ObjectArrayValue Of(ObjectValue o) => new(o.Cls, 1, 1, new ObjectValue?[] { o });
    public override string ClassName => Cls.Name;
    public ObjectValue At(int k) => Items[k] ??= DefaultFactory?.Invoke(Cls) ?? new ObjectValue(Cls);
    public override ArrayValue Gather(int[] lin, int r, int c) => new ObjectArrayValue(Cls, r, c, lin.Select(k => (ObjectValue?)At(k)).ToArray());
    public override ArrayValue Resized(int r, int c)
    {
        var m = new ObjectArrayValue(Cls, r, c);
        for (int j = 0; j < Math.Min(c, C); j++)
            for (int i = 0; i < Math.Min(r, R); i++) m.Items[i + j * r] = Items[i + j * R];
        return m;
    }
    public override void SetFrom(int dst, ArrayValue src, int srcIdx)
    {
        if (src is not ObjectArrayValue oa) throw new OctaveException($"operator = undefined for '{Cls.Name}' by '{src.ClassName}' operations");
        var o = oa.At(srcIdx);
        if (o.Cls != Cls && !o.Cls.IsA(Cls.Name)) throw new OctaveException($"operator = undefined for '{Cls.Name}' by '{o.Cls.Name}' operations");
        Items[dst] = o;
    }
    public override ArrayValue EmptyLike(int r, int c) => new ObjectArrayValue(Cls, r, c);
    /// <summary>1x1 arrays collapse to the plain object.</summary>
    public Value Simplify() => Numel == 1 && ND == null ? At(0) : this;
}

public partial class Interpreter
{
    readonly Dictionary<string, ClassInfo> classes = new();

    ClassInfo? FindClassQuiet(string name)
    {
        if (classes.TryGetValue(name, out var c)) return c;
        if (LookupVar(name) != null) return null;
        try { return FindOnPath(name)?.Class; } catch (OctaveException) { return null; }
    }

    void LoadFolderMethods(ClassInfo ci, string dir)
    {
        foreach (var mf in Directory.GetFiles(dir, "*.m"))
        {
            var mn = Path.GetFileNameWithoutExtension(mf);
            if (mn == ci.Name || ci.Methods.ContainsKey(mn)) continue;
            var mu = Parser.ParseProgram(File.ReadAllText(mf));
            if (mu.Functions.Count == 0) continue;
            var defs = mu.Functions.Select(d => UserFunction.From(d, mf)).ToList();
            var msibs = defs.ToDictionary(d => d.Name, d => d);
            foreach (var d in defs) { d.SetSiblings(msibs); d.OwnerClass = ci; }
            ci.Methods[mn] = defs[0];
        }
    }

    /// <summary>Old-style @folder class registered by class(struct, 'Name').</summary>
    public ClassInfo OldStyleClass(string name)
    {
        if (classes.TryGetValue(name, out var c)) return c;
        var ci = new ClassInfo { Name = name, IsOldStyle = true };
        var file = FindFile(name);
        if (file != null && Path.GetFileName(Path.GetDirectoryName(file)) == "@" + name)
        {
            ci.File = file; ci.Folder = Path.GetDirectoryName(file);
            LoadFolderMethods(ci, ci.Folder!);
        }
        classes[name] = ci;
        return ci;
    }

    public ClassInfo? FindClass(string name)
    {
        if (classes.TryGetValue(name, out var c)) return c;
        var uf = FindOnPath(name);
        return uf?.Class;
    }

    ClassInfo BuildClass(ClassDefAst ast, string path, List<FunctionDef> locals)
    {
        var ci = new ClassInfo { Name = ast.Name, File = path, Props = ast.Props, Events = ast.Events.ToHashSet(), Enums = ast.Enums };
        classes[ast.Name] = ci; // register early (self references)
        foreach (var s in ast.Supers)
        {
            if (s == "handle") { ci.IsHandle = true; continue; }
            var sc = FindClass(s) ?? throw new OctaveException($"superclass '{s}' of '{ast.Name}' not found");
            ci.Super ??= sc;
            if (sc.IsHandle) ci.IsHandle = true;
        }
        var sibs = new Dictionary<string, UserFunction>();
        foreach (var l in locals) sibs[l.Name] = UserFunction.From(l, path);
        foreach (var m in ast.Methods)
        {
            var f = UserFunction.From(m.Func, path);
            f.OwnerClass = ci;
            ci.Methods[m.Func.Name] = f;
            if (m.Static) ci.Static.Add(m.Func.Name);
            if (m.Access != "public") ci.MethodAccess[m.Func.Name] = m.Access;
        }
        // @folder classdef: other .m files in the folder are methods
        var dir = Path.GetDirectoryName(path);
        if (dir != null && Path.GetFileName(dir) == "@" + ast.Name)
        {
            ci.Folder = dir;
            LoadFolderMethods(ci, dir);
        }
        var all = new Dictionary<string, UserFunction>(sibs);
        foreach (var f in ci.Methods.Values) f.SetSiblings(all);
        foreach (var f in sibs.Values) f.SetSiblings(all);
        foreach (var p in ast.Props.Where(p => p.Constant))
            ci.Constants[p.Name] = p.Default != null ? Eval(p.Default) : MatrixValue.Empty();
        return ci;
    }

    ObjectValue NewObject(ClassInfo ci)
    {
        var o = new ObjectValue(ci);
        foreach (var p in ci.AllProps())
            if (!p.Constant && !p.Dependent) o.Props[p.Name] = p.Default != null ? Eval(p.Default) : MatrixValue.Empty();
        return o;
    }

    /// <summary>Construct an instance: default property values, then the constructor (if any).</summary>
    public Value[] Construct(ClassInfo ci, Value[] args, int nargout)
    {
        var o = NewObject(ci);
        return RunCtor(ci, o, args, nargout, implicitSuper: true);
    }

    Value[] RunCtor(ClassInfo ci, ObjectValue o, Value[] args, int nargout, bool implicitSuper)
    {
        if (ci.Methods.TryGetValue(ci.Name, out var ctor))
        {
            if (ctor.Outputs.Count == 0) throw new OctaveException($"{ci.Name}: constructor must return the object");
            // implicit no-arg superclass constructor when the ctor does not call obj@Super(...)
            if (implicitSuper && ci.Super != null && !CallsSuper(ctor.Body, ci.Super.Name)) o = (ObjectValue)RunCtor(ci.Super, o, Array.Empty<Value>(), 1, true)[0];
            var r = CallUser(ctor, args, 1, new Dictionary<string, Value> { [ctor.Outputs[0]] = o });
            return r.Length > 0 ? r : new Value[] { o };
        }
        if (ci.Super != null) return RunCtor(ci.Super, o, args, nargout, true);
        if (args.Length > 0) throw new OctaveException($"{ci.Name}: class has no constructor accepting arguments");
        return new Value[] { o };
    }

    static bool CallsSuper(List<Stmt> body, string super)
    {
        bool found = false;
        void Visit(object? n)
        {
            if (found || n == null) return;
            if (n is IdentExpr id && id.Name.EndsWith("@" + super)) { found = true; return; }
            if (n is string || n.GetType().IsPrimitive) return;
            if (n is System.Collections.IEnumerable en) { foreach (var x in en) Visit(x); return; }
            if (n is Node || n.GetType().Name.StartsWith("ValueTuple"))
                foreach (var pi in n.GetType().GetProperties()) { if (pi.GetIndexParameters().Length == 0 && pi.Name != "EqualityContract") Visit(pi.GetValue(n)); }
            foreach (var fi in n.GetType().GetFields()) if (n.GetType().Name.StartsWith("ValueTuple")) Visit(fi.GetValue(n));
        }
        Visit(body);
        return found;
    }

    /// <summary>name@Super(args): superclass constructor call (name is the object variable) or superclass method call.</summary>
    Value[] CallSuper(string spec, Value[] args, int nargout)
    {
        var parts = spec.Split('@', 2);
        var sc = FindClass(parts[1]) ?? throw new OctaveException($"'{parts[1]}' undefined");
        var objVar = LookupVar(parts[0]);
        if (objVar is ObjectValue o && Current.Func?.OwnerClass != null && Current.Func.Name == Current.Func.OwnerClass.Name)
        {
            var r = RunCtor(sc, o, args, nargout, true);
            if (r.Length > 0 && r[0] is ObjectValue ro)
            {
                // superclass part initialized: keep the subclass identity
                if (ro != o) foreach (var kv in ro.Props) o.Props[kv.Key] = kv.Value;
                SetVar(parts[0], o);
                return new Value[] { o };
            }
            return r;
        }
        var m = sc.FindMethod(parts[0], out _) ?? throw new OctaveException($"no method '{parts[0]}' in superclass '{sc.Name}'");
        return CallUser(m, args, nargout);
    }

    /// <summary>Dispatch fname(obj, ...) to a class method when an argument is an object.</summary>
    bool TryDispatchMethod(string name, Value[] args, int nargout, out Value[] result)
    {
        foreach (var a in args)
        {
            var cls = a is ObjectValue o ? o.Cls : a is ObjectArrayValue oa ? oa.Cls : null;
            if (cls != null)
            {
                var m = cls.FindMethod(name, out var mo);
                if (m != null && !mo!.Static.Contains(name) && name != m.OwnerClass?.Name && !name.Contains('.'))
                {
                    if (cls.MethodAccess.TryGetValue(name, out var acc) && !CanAccess(mo, acc))
                        throw new OctaveException($"{cls.Name}: method '{name}' has {acc} access and cannot be called from this context");
                    result = CallUser(m, args, nargout);
                    if (name == "delete" && a is ObjectValue d && cls.IsHandle) d.Deleted = true;
                    return true;
                }
            }
        }
        result = Array.Empty<Value>();
        return false;
    }

    Value GetProp(ObjectValue o, string name)
    {
        if (o.Deleted) throw new OctaveException("invalid object (deleted handle)");
        if (o.Props.TryGetValue(name, out var v)) return v;
        foreach (var c in o.Cls.Chain()) if (c.Constants.TryGetValue(name, out var cv)) return cv;
        throw new OctaveException($"invalid use of a {o.Cls.Name} object: unknown property or method '{name}'");
    }

    public static List<string> ObjectProps(ObjectValue o) => o.Cls.AllProps().Where(p => !p.Constant && !p.Dependent && !p.Hidden && p.GetAccess == "public" && o.Props.ContainsKey(p.Name)).Select(p => p.Name).ToList();

    // ------------------------------------------------------------ access control
    ClassInfo? CallerClass()
    {
        for (var f = Current.Func; f != null; f = f.Parent) if (f.OwnerClass != null) return f.OwnerClass;
        return null;
    }

    public bool CanAccess(ClassInfo owner, string access)
    {
        if (access is "public" or "" or "true") return true;
        var caller = CallerClass();
        if (caller == null) return false;
        if (access == "private") return caller == owner;
        if (access == "immutable") return caller == owner && Current.Func?.Name == caller.Name;
        // protected / class lists: allow the class hierarchy
        return caller.IsA(owner.Name) || owner.IsA(caller.Name);
    }

    /// <summary>Property read honoring get.Prop methods and GetAccess.</summary>
    public Value ReadProp(ObjectValue o, string name)
    {
        if (o.Deleted) throw new OctaveException("invalid object (deleted handle)");
        var pd = o.Cls.FindProp(name, out var owner);
        if (pd != null && !CanAccess(owner!, pd.GetAccess))
            throw new OctaveException($"{o.Cls.Name}: property '{name}' has {pd.GetAccess} access and cannot be read from this context");
        var getter = o.Cls.FindMethod("get." + name, out _);
        if (getter != null && !ReferenceEquals(Current.Func, getter))
        {
            var r = CallUser(getter, new Value[] { o }, 1);
            return r.Length > 0 ? r[0] : MatrixValue.Empty();
        }
        if (pd != null && pd.Dependent && !o.Props.ContainsKey(name))
            throw new OctaveException($"{o.Cls.Name}: dependent property '{name}' has no get method");
        return GetProp(o, name);
    }

    /// <summary>Property write honoring set.Prop methods, SetAccess and Constant; returns the (new) object.</summary>
    public ObjectValue WriteProp(ObjectValue o, string name, Func<Value?, Value> compute, bool needOld)
    {
        if (o.Deleted) throw new OctaveException("invalid object (deleted handle)");
        var pd = o.Cls.FindProp(name, out var owner);
        if (pd == null || pd.Constant)
            throw new OctaveException(o.Cls.Chain().Any(c => c.Constants.ContainsKey(name)) ? $"subsasgn: cannot set constant property '{name}'" : $"subsasgn: unknown property: {name}");
        if (!CanAccess(owner!, pd.SetAccess))
            throw new OctaveException($"{o.Cls.Name}: property '{name}' has {pd.SetAccess} access and cannot be set from this context");
        Value? old = o.Props.TryGetValue(name, out var ov) ? ov : null;
        var setter = o.Cls.FindMethod("set." + name, out _);
        bool inSetter = setter != null && ReferenceEquals(Current.Func, setter);
        if (old == null && pd.Dependent && !inSetter && needOld)
        {
            var getter = o.Cls.FindMethod("get." + name, out _);
            if (getter != null) old = CallUser(getter, new Value[] { o }, 1).FirstOrDefault();
        }
        var nv = compute(old);
        if (setter != null && !inSetter)
        {
            var r = CallUser(setter, new Value[] { o, nv }, o.Cls.IsHandle ? 0 : 1);
            if (o.Cls.IsHandle) return o;
            if (r.Length == 0 || r[0] is not ObjectValue ro) throw new OctaveException($"set.{name}: value class set method must return the object");
            return ro;
        }
        var target = o.CopyForWrite();
        target.Props[name] = nv;
        return target;
    }

    // ------------------------------------------------------------ enumerations
    public ObjectValue EnumMember(ClassInfo ci, string name)
    {
        var owner = ci.Chain().First(c => c.Enums.Any(e => e.Name == name));
        if (owner.EnumCache.TryGetValue(name, out var o)) return o;
        var def = owner.Enums.First(e => e.Name == name);
        var args = def.Args.Select(a => Eval(a)).ToArray();
        var obj = NewObject(owner);
        obj = (ObjectValue)RunCtor(owner, obj, args, 1, true)[0];
        obj.EnumName = name;
        owner.EnumCache[name] = obj;
        return obj;
    }

    public List<ObjectValue> EnumMembers(ClassInfo ci) => ci.Chain().First(c => c.Enums.Count > 0).Enums.Select(e => EnumMember(ci, e.Name)).ToList();

    // ------------------------------------------------------------ events
    static ClassInfo? listenerClass, eventDataClass;
    public static ClassInfo ListenerClass => listenerClass ??= new ClassInfo
    {
        Name = "event.listener", IsHandle = true,
        Props = new() { new PropDef("Source", null, false), new PropDef("EventName", null, false), new PropDef("Callback", null, false), new PropDef("Enabled", null, false) }
    };
    public static ClassInfo EventDataClass => eventDataClass ??= new ClassInfo
    {
        Name = "event.EventData", IsHandle = true,
        Props = new() { new PropDef("Source", null, false), new PropDef("EventName", null, false) }
    };

    public ObjectValue AddListener(ObjectValue src, string evt, Value cb)
    {
        if (!src.Cls.IsHandle) throw new OctaveException("addlistener: events require a handle class");
        if (!src.Cls.HasEvent(evt)) throw new OctaveException($"addlistener: '{evt}' is not an event of class '{src.Cls.Name}'");
        var l = new ObjectValue(ListenerClass);
        l.Props["Source"] = src; l.Props["EventName"] = MatrixValue.Str(evt); l.Props["Callback"] = cb; l.Props["Enabled"] = MatrixValue.Bool(true);
        src.Listeners ??= new();
        if (!src.Listeners.TryGetValue(evt, out var list)) src.Listeners[evt] = list = new();
        list.Add(l);
        return l;
    }

    public void Notify(ObjectValue src, string evt, Value? data)
    {
        if (!src.Cls.HasEvent(evt)) throw new OctaveException($"notify: '{evt}' is not an event of class '{src.Cls.Name}'");
        if (src.Listeners == null || !src.Listeners.TryGetValue(evt, out var list)) return;
        ObjectValue ed;
        if (data is ObjectValue d) { ed = d; if (d.Cls.FindProp("Source", out _) != null || d.Props.ContainsKey("Source")) { d.Props["Source"] = src; d.Props["EventName"] = MatrixValue.Str(evt); } }
        else { ed = new ObjectValue(EventDataClass); ed.Props["Source"] = src; ed.Props["EventName"] = MatrixValue.Str(evt); }
        foreach (var l in list.ToList())
        {
            if (l.Deleted || !IsTrueValue(l.Props["Enabled"])) continue;
            var cb = l.Props["Callback"];
            var args = new Value[] { src, ed };
            if (cb is FunctionHandleValue fh) CallHandle(fh, args, 0);
            else if (cb is MatrixValue s && s.IsChar) CallFunction(s.AsString(), args, 0);
            else if (cb is CellValue cc && cc.Numel > 0 && cc.Data[0] is FunctionHandleValue fh2) CallHandle(fh2, args.Concat(cc.Data.Skip(1)).ToArray(), 0);
        }
    }

    static bool IsTrueValue(Value v) { try { return Ops.IsTrue(v); } catch (OctaveException) { return false; } }
}
