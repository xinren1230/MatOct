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
    public ObjectValue(ClassInfo c) { Cls = c; }
    public override int Rows => 1;
    public override int Cols => 1;
    public override string ClassName => Cls.Name;
    public ObjectValue CopyForWrite() => Cls.IsHandle ? this : new ObjectValue(Cls) { Props = new Dictionary<string, Value>(Props) };
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

    public ClassInfo? FindClass(string name)
    {
        if (classes.TryGetValue(name, out var c)) return c;
        var uf = FindOnPath(name);
        return uf?.Class;
    }

    ClassInfo BuildClass(ClassDefAst ast, string path, List<FunctionDef> locals)
    {
        var ci = new ClassInfo { Name = ast.Name, File = path, Props = ast.Props };
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
            if (!p.Constant) o.Props[p.Name] = p.Default != null ? Eval(p.Default) : MatrixValue.Empty();
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
            return RunCtor(sc, o, args, nargout, true);
        var m = sc.FindMethod(parts[0], out _) ?? throw new OctaveException($"no method '{parts[0]}' in superclass '{sc.Name}'");
        return CallUser(m, args, nargout);
    }

    /// <summary>Dispatch fname(obj, ...) to a class method when an argument is an object.</summary>
    bool TryDispatchMethod(string name, Value[] args, int nargout, out Value[] result)
    {
        foreach (var a in args)
        {
            if (a is ObjectValue o)
            {
                var m = o.Cls.FindMethod(name, out _);
                if (m != null && !o.Cls.Static.Contains(name) && name != m.OwnerClass?.Name) { result = CallUser(m, args, nargout); return true; }
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

    public static List<string> ObjectProps(ObjectValue o) => o.Cls.AllProps().Where(p => !p.Constant).Select(p => p.Name).ToList();
}
