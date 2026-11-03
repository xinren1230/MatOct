// MatRewrite - GPL-3.0-or-later
// Phase 6: registration of the handle-graphics builtins (overrides the phase 3 versions).
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static GObject HLookup(Interpreter it, Value v, string fn)
        {
            if (v is not MatrixValue m || m.IsChar || m.Numel != 1) throw new OctaveException($"{fn}: H must be a graphics handle");
            return it.Graphics.Lookup(m.Re[0]) ?? throw new OctaveException($"{fn}: invalid graphics handle ({m.Re[0]})");
        }

        static Figure? FigureOf(Interpreter it, GObject? o)
        {
            for (; o != null; o = it.Graphics.ParentOf(o)) if (o is Figure f) return f;
            return null;
        }

        static Value IsHg(Interpreter it, Value v)
        {
            if (v is not MatrixValue m || m.IsChar) return Bool(false);
            var r = new MatrixValue(m.R, m.C, NumKind.Logical) { ND = (int[]?)m.ND?.Clone() };
            for (int i = 0; i < m.Numel; i++) r.Re[i] = it.Graphics.Lookup(m.Re[i]) is { Deleted: false } ? 1 : 0;
            return r;
        }

        static void FireListeners(Interpreter it, GObject o, string p)
        {
            if (o.Listeners == null || !o.Listeners.TryGetValue(p, out var l) || l.Count == 0) return;
            o.Firing ??= new(StringComparer.OrdinalIgnoreCase);
            if (!o.Firing.Add(p)) return;
            try { foreach (var cb in l.ToList()) RunCallback(it, o, cb); }
            finally { o.Firing.Remove(p); }
        }

        static void RegisterHG()
        {
            foreach (var t in new[] { "axes", "line", "text", "patch", "surface", "image", "light", "hggroup", "uicontrol", "uipanel",
                "uibuttongroup", "uimenu", "uicontextmenu", "uitoolbar", "uipushtool", "uitoggletool", "uitable", "scatter" })
            {
                var type = t;
                RegG($"__go_{type}__", (it, a, n) =>
                {
                    if (a.Length < 1) throw new OctaveException($"Invalid call to __go_{type}__");
                    var parent = HLookup(it, a[0], $"__go_{type}__");
                    var o = MakeGo(it, type, parent);
                    ApplyPairs(it, o, a.Skip(1).ToArray(), $"__go_{type}__");
                    return Out(Num(o.Handle));
                });
            }
            RegG("__go_figure__", (it, a, n) =>
            {
                var g = it.Graphics;
                double h = a.Length > 0 ? D(a[0], "__go_figure__") : double.NaN;
                bool intH = !double.IsNaN(h) && h == Math.Floor(h) && h > 0;
                var rest = a.Skip(1).ToList();
                for (int i = 0; i + 1 < rest.Count; i += 2)
                    if (IsString(rest[i]) && S(rest[i], "figure").Equals("integerhandle", StringComparison.OrdinalIgnoreCase) && IsString(rest[i + 1]) && S(rest[i + 1], "figure") == "off")
                    { intH = false; rest.RemoveRange(i, 2); break; }
                var f = intH ? g.NewFigure((int)h) : g.NewFigureNonInteger();
                ApplyPairs(it, f, rest.ToArray(), "figure");
                return Out(Num(f.Handle));
            });
            RegG("__go_delete__", (it, a, n) => { foreach (var h in M(a[0], "__go_delete__").Re) if (it.Graphics.Lookup(h) is { } o) DeleteObject(it, o); return Array.Empty<Value>(); });
            RegG("__go_handles__", (it, a, n) =>
            {
                bool all = a.Length > 0 && D(a[0], "__go_handles__") != 0;
                var acc = new List<GObject>(); Walk(it, it.Graphics.Root, 0, int.MaxValue, all, acc);
                return Out(MatrixValue.Col(acc.Select(o => o.Handle)));
            });
            RegG("__go_figure_handles__", (it, a, n) => Out(MatrixValue.Col(it.Graphics.Figures.Values.Where(f => !f.Deleted).Select(f => f.Handle))));
            RegG("__go_execute_callback__", (it, a, n) =>
            {
                var o = HLookup(it, a[0], "__go_execute_callback__");
                var name = S(a[1], "__go_execute_callback__").ToLowerInvariant();
                if (a.Length > 2) RunCallback(it, o, a[2]); else RunCallbackProp(it, o, name);
                return Array.Empty<Value>();
            });
            RegG("ishghandle", (it, a, n) => { if (a.Length != 1) throw new OctaveException("Invalid call to ishghandle"); return Out(IsHg(it, a[0])); });
            RegG("ishandle", (it, a, n) => { if (a.Length != 1) throw new OctaveException("Invalid call to ishandle"); return Out(IsHg(it, a[0])); });
            RegG("isgraphics", (it, a, n) =>
            {
                if (a.Length < 1 || a.Length > 2) throw new OctaveException("Invalid call to isgraphics");
                var r = IsHg(it, a[0]);
                if (a.Length == 2 && r is MatrixValue rm && a[0] is MatrixValue hm)
                {
                    var t = S(a[1], "isgraphics");
                    for (int i = 0; i < rm.Numel; i++) if (rm.Re[i] != 0 && !string.Equals(TypeOf(it.Graphics.Lookup(hm.Re[i])!), t, StringComparison.OrdinalIgnoreCase)) rm.Re[i] = 0;
                }
                return Out(r);
            });
            RegG("reset", (it, a, n) =>
            {
                foreach (var h in M(a[0], "reset").Re)
                {
                    var o = it.Graphics.Lookup(h) ?? throw new OctaveException("reset: invalid graphics handle");
                    foreach (var k in o.Props.Keys.ToList())
                        if (!(k is "position" or "outerposition" or "innerposition" or "units" or "paperunits" or "windowstyle" or "paperposition" or "papersize" or "paperorientation" or "papertype" or "tag" or "userdata" or "__appdata__" or "parent" or "handlevisibility") && !k.StartsWith("default"))
                            o.Props.Remove(k);
                    if (o is Axes ax)
                    {
                        ax.XLim = ax.YLim = ax.ZLim = null; ax.CLim = null; ax.Az = 0; ax.El = 90; ax.XLog = ax.YLog = false;
                        ax.YDirReverse = false; ax.Grid = false; ax.Box = true; ax.ColorIndex = 0; ax.Equal = ax.Tight = ax.Square = false;
                        ax.LegendVisible = false; ax.LegendLabels = null; ax.Colorbar = false;
                    }
                    if (o is Figure f) it.Graphics.Touch(f);
                }
                return Array.Empty<Value>();
            });
            RegG("addproperty", (it, a, n) =>
            {
                if (a.Length < 3) throw new OctaveException("Invalid call to addproperty");
                var name = S(a[0], "addproperty"); var o = HLookup(it, a[1], "addproperty");
                var type = S(a[2], "addproperty");
                if (o.Props.ContainsKey(name) || (KnownProp(o, name.ToLowerInvariant()) && name != "displayname"))
                    throw new OctaveException($"addproperty: a '{name}' property already exists in the graphics object");
                Value v;
                if (a.Length > 3)
                {
                    v = a[3];
                    if (type.Equals("radio", StringComparison.OrdinalIgnoreCase) && IsString(v))
                    {
                        var opts = S(v, "addproperty").Split('|');
                        var def = opts.FirstOrDefault(x => x.StartsWith("{")) ?? opts[0];
                        v = Str(def.Trim('{', '}'));
                    }
                }
                else
                {
                    v = E0();
                    foreach (var t in TypeDefaults.Keys.OrderByDescending(k => k.Length))
                        if (type.StartsWith(t, StringComparison.OrdinalIgnoreCase) && TypeDefaults[t].TryGetValue(type[t.Length..], out var f)) { v = f(); break; }
                    if (type.Equals("color", StringComparison.OrdinalIgnoreCase)) v = MatrixValue.Row(new double[] { 0, 0, 0 });
                }
                o.Props[name] = v;
                return Array.Empty<Value>();
            });
            RegG("addlistener", (it, a, n) =>
            {
                if (a.Length < 3) throw new OctaveException("Invalid call to addlistener");
                if (a[0] is ObjectValue lsrc) return Out(it.AddListener(lsrc, S(a[1], "addlistener"), a[2]));
                var p = S(a[1], "addlistener").ToLowerInvariant();
                foreach (var h in M(a[0], "addlistener").Re)
                {
                    var o = it.Graphics.Lookup(h) ?? throw new OctaveException("addlistener: invalid graphics object");
                    o.Listeners ??= new(StringComparer.OrdinalIgnoreCase);
                    if (!o.Listeners.TryGetValue(p, out var l)) o.Listeners[p] = l = new();
                    l.Add(a[2]);
                }
                return Array.Empty<Value>();
            });
            RegG("dellistener", (it, a, n) =>
            {
                var p = S(a[1], "dellistener").ToLowerInvariant();
                foreach (var h in M(a[0], "dellistener").Re)
                    if (it.Graphics.Lookup(h)?.Listeners is { } ls && ls.TryGetValue(p, out var l))
                    { if (a.Length > 2) l.RemoveAll(x => ReferenceEquals(x, a[2]) || ValEq(x, a[2])); else l.Clear(); }
                return Array.Empty<Value>();
            });
            RegG("__is_handle_visible__", (it, a, n) =>
            {
                var o = it.Graphics.Lookup(D(a[0], "__is_handle_visible__"));
                return Out(Bool(o != null && !IsHidden(o)));
            });
            RegG("findobj", (it, a, n) => FindObj(it, a, false, n));
            RegG("findall", (it, a, n) => FindObj(it, a, true, n));
            RegG("allchild", (it, a, n) =>
            {
                var hs = M(a[0], "allchild").Re;
                Value One(double h) => HCol(it.Graphics.ChildrenOf(it.Graphics.Lookup(h) ?? throw new OctaveException("allchild: invalid handle")).Where(c => !c.Deleted));
                if (hs.Length == 1) return Out(One(hs[0]));
                return Out(new CellValue(hs.Length, 1, hs.Select(One).ToArray()));
            });
            RegG("gco", (it, a, n) =>
            {
                var g = it.Graphics;
                GObject? f = a.Length > 0 ? HLookup(it, a[0], "gco") : g.Current;
                if (f != null && f.Props.TryGetValue("currentobject", out var co)) return Out(co);
                return Out(E0());
            });
            RegG("gcbo", (it, a, n) =>
            {
                var o = it.Graphics.CallbackObject;
                var f = FigureOf(it, o);
                return Out(HandleOrEmpty(o), HandleOrEmpty(f));
            });
            RegG("gcbf", (it, a, n) => Out(HandleOrEmpty(FigureOf(it, it.Graphics.CallbackObject))));
            RegG("closereq", (it, a, n) =>
            {
                var f = FigureOf(it, it.Graphics.CallbackObject) ?? it.Graphics.Current;
                if (f != null) DeleteObject(it, f);
                return Array.Empty<Value>();
            });
            RegG("close", (it, a, n) =>
            {
                var g = it.Graphics;
                var figs = new List<Figure>(); bool force = false;
                if (a.Length == 0) { if (g.Current != null) figs.Add(g.Current); }
                else
                {
                    foreach (var v in a.Skip(1)) { var s = S(v, "close"); if (s is "force") force = true; else if (s is not "hidden") throw new OctaveException("close: second argument must be \"hidden\" or \"force\""); }
                    if (IsString(a[0]))
                    {
                        var s = S(a[0], "close");
                        if (s == "all") figs.AddRange(g.Figures.Values.Where(f => a.Length > 1 || !IsHidden(f)));
                        else figs.Add(g.Figures.Values.FirstOrDefault(x => x.Name == s) ?? throw new OctaveException($"close: no figure named '{s}'"));
                    }
                    else if (a[0] is MatrixValue m && !m.IsChar)
                        foreach (var h in m.Re)
                            figs.Add(g.Lookup(h) as Figure ?? throw new OctaveException("close: first argument must be \"all\", a figure handle, or a figure name"));
                    else throw new OctaveException("close: first argument must be \"all\", a figure handle, or a figure name");
                }
                foreach (var f in figs.ToList()) CloseFigure(it, f, force);
                return n > 0 ? Out(Num(1)) : Array.Empty<Value>();
            });
            var fileDelete = cur!.Builtins.TryGetValue("delete", out var fd) ? fd : null;
            RegG("delete", (it, a, n) =>
            {
                if (a.Length == 1 && a[0] is MatrixValue m && !m.IsChar)
                {
                    foreach (var h in m.Re)
                    {
                        var o = it.Graphics.Lookup(h) ?? throw new OctaveException("delete: invalid graphics object");
                        DeleteObject(it, o);
                    }
                    return Array.Empty<Value>();
                }
                if (fileDelete != null) return fileDelete(it, a, n);
                throw new OctaveException("delete: first argument must be a filename or graphics handle");
            });
            RegG("drawnow", (it, a, n) => { DrainCallbacks(it); foreach (var f in it.Graphics.Figures.Values) it.Graphics.Touch(f); return Array.Empty<Value>(); });
            RegG("set", (it, a, n) =>
            {
                if (a.Length < 1) throw new OctaveException("Invalid call to set");
                var hm = a[0] as MatrixValue;
                if (hm == null || hm.IsChar) throw new OctaveException("set: H must be a graphics handle");
                var objs = hm.Re.Select(h => it.Graphics.Lookup(h) ?? throw new OctaveException("set: invalid graphics handle")).ToList();
                if (a.Length == 1)
                {
                    if (n == 0) return Array.Empty<Value>();
                    var st = new StructValue();
                    foreach (var k in PropNames(it, objs[0])) { st.AddField(k); st.Elems[0][k] = new CellValue(0, 1); }
                    return Out(st);
                }
                if (a.Length == 2 && IsString(a[1])) return Out(new CellValue(1, 0));
                foreach (var o in objs)
                {
                    if (o is RootObject)
                        for (int k = 1; k + 1 < a.Length; k += 2) if (IsString(a[k])) { it.Graphics.RootProps[S(a[k], "set")] = a[k + 1]; Graphics.FigStyle.ApplyRootDefault(S(a[k], "set"), a[k + 1]); }
                    ApplyPairs(it, o, a.Skip(1).ToArray(), "set");
                }
                return Array.Empty<Value>();
            });
            RegG("get", (it, a, n) =>
            {
                if (a.Length < 1 || a.Length > 2) throw new OctaveException("Invalid call to get");
                if (a[0] is not MatrixValue hm || hm.IsChar) throw new OctaveException("get: H must be a graphics handle");
                if (hm.IsEmpty) return Out(new MatrixValue(0, 1));
                var objs = hm.Re.Select(h => it.Graphics.Lookup(h) ?? throw new OctaveException("get: invalid graphics handle")).ToList();
                Value One(GObject o)
                {
                    if (a.Length == 1) return PropStruct(it, o);
                    if (a[1] is CellValue pc) return new CellValue(1, pc.Data.Length, pc.Data.Select(p => GetAny(it, o, S(p, "get"))).ToArray());
                    return GetAny(it, o, S(a[1], "get"));
                }
                if (objs.Count == 1) return Out(One(objs[0]));
                if (a.Length > 1 && a[1] is CellValue pc2)
                {
                    var cv = new CellValue(objs.Count, pc2.Data.Length);
                    for (int i = 0; i < objs.Count; i++) for (int j = 0; j < pc2.Data.Length; j++) cv.Data[i + j * objs.Count] = GetAny(it, objs[i], S(pc2.Data[j], "get"));
                    return Out(cv);
                }
                var all = objs.Select(One).ToArray();
                if (a.Length == 1 && all.All(v => v is StructValue))
                {
                    var flds = new List<string>();
                    foreach (StructValue sv in all) foreach (var f in sv.Fields) if (!flds.Contains(f)) flds.Add(f);
                    var sa = new StructValue(all.Length, 1, flds);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var sv = (StructValue)all[i];
                        foreach (var f in flds) sa.Elems[i][f] = sv.HasField(f) ? sv.Get(f) : MatrixValue.Empty();
                    }
                    return Out(sa);
                }
                return Out(new CellValue(objs.Count, 1, all));
            });
            foreach (var w in new[] { "title", "xlabel", "ylabel", "zlabel" })
            {
                var which = w;
                RegG(which, (it, a, n) =>
                {
                    if (a.Length == 0) throw new OctaveException($"Invalid call to {which}");
                    int k = 0; Axes ax;
                    if (which == "title" && a[0] is MatrixValue lm && !lm.IsChar && lm.Numel == 1 && a.Length > 1 && it.Graphics.Lookup(lm.Re[0]) is GenericObject { Type: "legend" } lg)
                    {
                        if (!(lg.Props.TryGetValue("title", out var tv) && tv is MatrixValue tm && it.Graphics.Lookup(tm.Re[0]) is GenericObject { Deleted: false } tobj))
                        {
                            tobj = it.Graphics.Register(new GenericObject { Type = "text", Owner = lg, Parent = lg.Parent });
                            lg.Props["title"] = Num(tobj.Handle);
                        }
                        tobj.Props["string"] = a[1];
                        var rest0 = a.Skip(2).ToArray();
                        for (int q = 0; q + 1 < rest0.Length; q += 2) it.CallFunction("set", new[] { Num(tobj.Handle), rest0[q], rest0[q + 1] }, 0);
                        return n > 0 ? Out(Num(tobj.Handle)) : Array.Empty<Value>();
                    }
                    if (a[0] is MatrixValue hm && !hm.IsChar && hm.Numel == 1 && a.Length > 1 && it.Graphics.Lookup(hm.Re[0]) is Axes hax) { ax = hax; k = 1; }
                    else ax = it.Graphics.Gca();
                    var lbl = it.Graphics.Label(ax, which);
                    lbl.Text = LabelString(a[k], which); lbl.Raw = a[k] is CellValue ? a[k] : null;
                    var rest = a.Skip(k + 1).ToArray();
                    if (rest.Length > 0) ApplyPairs(it, lbl, rest, which);
                    it.Graphics.Touch(ax.Figure);
                    return n > 0 ? Out(Num(lbl.Handle)) : Array.Empty<Value>();
                });
            }
            RegG("line", (it, a, n) =>
            {
                var g = it.Graphics; int k = 0;
                GObject? parent = null;
                if (a.Length > 0 && a[0] is MatrixValue h0 && !h0.IsChar && h0.Numel == 1 && (a.Length == 1 || a.Length % 2 == 1) && g.Lookup(h0.Re[0]) is Axes or Series && a.Length != 2)
                { parent = g.Lookup(h0.Re[0]); k = 1; }
                var nums = new List<MatrixValue>();
                while (k < a.Length && a[k] is MatrixValue m && !m.IsChar && nums.Count < 3) { nums.Add(m); k++; }
                var props = a.Skip(k).ToArray();
                for (int i = 0; i + 1 < props.Length; i += 2)
                    if (IsString(props[i]) && S(props[i], "line").Equals("parent", StringComparison.OrdinalIgnoreCase)) parent = HLookup(it, props[i + 1], "line");
                var ax = AxesFor(it, parent);
                var lines = new List<GObject>();
                if (nums.Count == 0) lines.Add(MakeGo(it, "line", parent ?? ax));
                else
                {
                    if (nums.Count == 1) throw new OctaveException("line: X and Y must be specified together");
                    var X = nums[0]; var Y = nums[1]; var Z = nums.Count > 2 ? nums[2] : null;
                    bool xv = X.R == 1 || X.C == 1, yv = Y.R == 1 || Y.C == 1;
                    int cols = Math.Max(xv ? 1 : X.C, yv ? 1 : Y.C);
                    double[] Col(MatrixValue M0, bool vec, int c) => vec ? M0.Re.ToArray() : M0.Re.Skip(c * M0.R).Take(M0.R).ToArray();
                    for (int c = 0; c < cols; c++)
                    {
                        var l = (LineSeries)MakeGo(it, "line", parent ?? ax);
                        l.X = Col(X, xv, c); l.Y = Col(Y, yv, c);
                        if (Z != null) l.Z = Col(Z, Z.R == 1 || Z.C == 1, c);
                        if (l.X.Length != l.Y.Length && l.X.Length > 0 && l.Y.Length > 0) throw new OctaveException("line: number of X and Y values must agree");
                        lines.Add(l);
                    }
                }
                foreach (var l in lines) ApplyPairs(it, l, props, "line");
                return n > 0 ? Out(MatrixValue.Col(lines.Select(l => l.Handle))) : Array.Empty<Value>();
            });
            RegG("patch", (it, a, n) =>
            {
                var g = it.Graphics; int k = 0; GObject? parent = null;
                if (a.Length > 0 && a[0] is MatrixValue h0 && !h0.IsChar && h0.Numel == 1 && g.Lookup(h0.Re[0]) is Axes or Series) { parent = g.Lookup(h0.Re[0]); k = 1; }
                var nums = new List<Value>();
                while (k < a.Length && !IsString(a[k]) && a[k] is not StructValue && nums.Count < 4) { nums.Add(a[k]); k++; }
                if (k < a.Length && IsString(a[k]) && nums.Count >= 2 && nums.Count < 4 && (k + 1 >= a.Length || (a.Length - k) % 2 == 1)) { nums.Add(a[k]); k++; }
                var props = a.Skip(k).ToArray();
                for (int i = 0; i + 1 < props.Length; i += 2)
                    if (IsString(props[i]) && S(props[i], "patch").Equals("parent", StringComparison.OrdinalIgnoreCase)) parent = HLookup(it, props[i + 1], "patch");
                var ax = AxesFor(it, parent);
                var ps = (PatchSeries)MakeGo(it, "patch", parent ?? ax);
                Value? c = null;
                if (nums.Count >= 2)
                {
                    ps.Props["xdata"] = nums[0]; ps.Props["ydata"] = nums[1];
                    if (nums.Count == 4) { ps.Props["zdata"] = nums[2]; c = nums[3]; } else if (nums.Count == 3) c = nums[2];
                    RebuildPatch(ps, false);
                }
                else if (nums.Count == 1) throw new OctaveException("patch: invalid arguments");
                if (c != null)
                {
                    if (IsString(c)) ps.FaceColor = ParseColor(c, "patch");
                    else if (c is MatrixValue cm && cm.Numel == 3 && cm.R == 1) ps.FaceColor = new Rgb(cm.Re[0], cm.Re[1], cm.Re[2]);
                    else { ps.Props["cdata"] = c; ps.Props["facecolor"] = Str("flat"); ps.FaceColor = ax.NextColor(); }
                }
                ApplyPairs(it, ps, props, "patch");
                g.Touch(ax.Figure);
                return n > 0 ? Out(Num(ps.Handle)) : Array.Empty<Value>();
            });
        }
    }
}
