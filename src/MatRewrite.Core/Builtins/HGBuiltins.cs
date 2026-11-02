// MatRewrite - GPL-3.0-or-later
// Phase 6: generic graphics object model (property system, defaults, __go_*__ internals, findobj, callbacks).
using System.IO;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static Value E() => new MatrixValue(0, 0);

        static readonly Dictionary<string, Func<Value>> CommonDefaults = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tag"] = () => Str(""), ["userdata"] = E, ["visible"] = () => Str("on"), ["handlevisibility"] = () => Str("on"),
            ["hittest"] = () => Str("on"), ["pickableparts"] = () => Str("visible"), ["busyaction"] = () => Str("queue"),
            ["interruptible"] = () => Str("on"), ["buttondownfcn"] = E, ["createfcn"] = E, ["deletefcn"] = E,
            ["beingdeleted"] = () => Str("off"), ["clipping"] = () => Str("on"), ["selected"] = () => Str("off"),
            ["selectionhighlight"] = () => Str("on"), ["uicontextmenu"] = E, ["contextmenu"] = E, ["displayname"] = () => Str(""),
            ["__modified__"] = () => Str("on"), ["__appdata__"] = E,
            ["xliminclude"] = () => Str("on"), ["yliminclude"] = () => Str("on"), ["zliminclude"] = () => Str("on"), ["climinclude"] = () => Str("on"), ["aliminclude"] = () => Str("on"),
            ["seriesindex"] = () => Num(1), ["annotation"] = E, ["datatiptemplate"] = E, ["latexstring"] = () => Str(""),
        };
    }
}

namespace MatRewrite.Core.Builtins
{
    public static partial class BuiltinLibrary
    {
        static Value E0() => new MatrixValue(0, 0);

        /// <summary>Full get with defaults/factory lookups.</summary>
        static Value GetAny(Interpreter it, GObject o, string p0, string fn = "get")
        {
            var p = p0.ToLowerInvariant();
            var v = GetGeneric(it, o, p);
            if (v != null) return v;
            if (p.StartsWith("default") || p.StartsWith("factory"))
            {
                var rest = p.StartsWith("default") ? p[7..] : p[7..];
                foreach (var t in TypeDefaults.Keys.OrderByDescending(k => k.Length))
                    if (rest.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                    {
                        var prop = rest[t.Length..];
                        if (p.StartsWith("default") && it.Graphics.RootProps.TryGetValue(p, out var rv)) return rv;
                        if (t == "axes" && prop == "colororder") return ColorOrderM();
                        if (TypeDefaults[t].TryGetValue(prop, out var f)) return f();
                        if (CommonDefaults.TryGetValue(prop, out var cf)) return cf();
                        if (t == "figure" && prop == "colormap") return it.CallFunction("parula", new Value[] { Num(64) }, 1)[0];
                    }
            }
            if (o is RootObject && it.Graphics.RootProps.TryGetValue(p, out var rp)) return rp;
            try { return GetProp(it, o, p); }
            catch (OctaveException) { throw new OctaveException($"{fn}: unknown {TypeOf(o)} property {p0}"); }
        }

        static IEnumerable<string> PropNames(Interpreter it, GObject o)
        {
            var s = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "type", "parent", "children", "beingdeleted" };
            if (TypeDefaults.TryGetValue(TypeOf(o), out var d)) s.UnionWith(d.Keys);
            if (o is not RootObject) s.UnionWith(CommonDefaults.Keys);
            s.UnionWith(o.Props.Keys.Where(k => !k.StartsWith("default")));
            switch (o)
            {
                case RootObject: s.UnionWith(new[] { "currentfigure", "callbackobject" }); break;
                case Figure: s.UnionWith(new[] { "currentaxes", "colormap", "name", "number" }); break;
                case Axes: s.UnionWith(new[] { "title", "xlabel", "ylabel", "zlabel", "xlim", "ylim", "zlim", "clim", "view", "xscale", "yscale", "nextplot", "xgrid", "ygrid", "box", "ydir", "colororder", "xtick", "ytick" }); break;
                case LabelObject: s.UnionWith(TypeDefaults["text"].Keys); break;
                case BarSeries: s.UnionWith(new[] { "xdata", "ydata", "barwidth", "barlayout", "horizontal", "basevalue", "showbaseline", "baseline", "bargroup", "facecolor", "edgecolor", "linewidth", "linestyle", "hittestarea" }); break;
            }
            return s.Where(k => !k.StartsWith("__"));
        }

        static StructValue PropStruct(Interpreter it, GObject o)
        {
            var st = new StructValue();
            foreach (var k in PropNames(it, o))
            {
                Value v; try { v = GetAny(it, o, k); } catch (OctaveException) { continue; }
                st.AddField(k); st.Elems[0][k] = v;
            }
            return st;
        }

        /// <summary>Set PROP/VALUE pairs given as a list, a struct, or a pair of cells.</summary>
        static void ApplyPairs(Interpreter it, GObject o, Value[] a, string fn)
        {
            var flat = new List<Value>();
            for (int k = 0; k < a.Length; k++)
            {
                if (flat.Count % 2 == 1) { flat.Add(a[k]); continue; }
                if (a[k] is StructValue st)
                    foreach (var f in st.Fields) { flat.Add(Str(f)); flat.Add(st.Elems[0][f]); }
                else if (a[k] is CellValue pc && k + 1 < a.Length && a[k + 1] is CellValue vc)
                {
                    for (int i = 0; i < pc.Data.Length; i++) { flat.Add(pc.Data[i]); flat.Add(vc.Data[Math.Min(i, vc.Data.Length - 1)]); }
                    k++;
                }
                else flat.Add(a[k]);
            }
            if (flat.Count % 2 != 0) throw new OctaveException($"{fn}: invalid number of PROPERTY/VALUE arguments");
            for (int k = 0; k < flat.Count; k += 2)
                if (!IsString(flat[k])) throw new OctaveException($"{fn}: PROPERTY must be a string");
            SetProps(it, o, flat.ToArray(), fn);
        }

        static Axes AxesFor(Interpreter it, GObject? parent) => parent switch
        {
            Axes ax => ax,
            Series s => s.Parent,
            Figure f => f.CurrentAxes ?? it.Graphics.NewAxes(f),
            _ => it.Graphics.Gca()
        };

        /// <summary>Create a graphics object of TYPE under PARENT.</summary>
        static GObject MakeGo(Interpreter it, string type, GObject? parent)
        {
            var g = it.Graphics;
            GObject o;
            Series? grp = parent is Series ps0 ? ps0 : null;
            switch (type)
            {
                case "axes":
                    o = g.NewAxes(parent as Figure ?? g.Gcf()); break;
                case "line": { var ax = AxesFor(it, parent); o = g.Add(ax, new LineSeries { X = new double[] { 0, 1 }, Y = new double[] { 0, 1 }, Color = ax.NextColor() }); break; }
                case "text": { var ax = AxesFor(it, parent); o = g.Add(ax, new TextSeries()); break; }
                case "patch": { var ax = AxesFor(it, parent); o = g.Add(ax, new PatchSeries()); break; }
                case "surface": { var ax = AxesFor(it, parent); o = g.Add(ax, new SurfaceSeries()); break; }
                case "image": { var ax = AxesFor(it, parent); o = g.Add(ax, new ImageSeries { CMin = 0, CMax = 1 }); break; }
                case "light": { var ax = AxesFor(it, parent); o = g.Add(ax, new LightObject()); break; }
                default:
                    if (type is "hggroup" or "scatter" && parent is Figure pf) parent = AxesFor(it, pf);
                    o = g.AddGeneric(parent ?? g.Gcf(), type); grp = null; break;
            }
            if (grp != null && o is Series s && s != grp) s.Group = grp;
            return o;
        }

        static void Walk(Interpreter it, GObject o, int depth, int max, bool all, List<GObject> acc)
        {
            acc.Add(o);
            if (depth >= max) return;
            foreach (var c in it.Graphics.ChildrenOf(o))
                if (!c.Deleted && (all || !IsHidden(c))) Walk(it, c, depth + 1, max, all, acc);
        }

        static bool IsEq(Interpreter it, Value a, Value b)
        {
            var r = it.CallFunction("isequal", new[] { a, b }, 1);
            return r.Length > 0 && r[0] is MatrixValue m && m.Numel == 1 && m.Re[0] != 0;
        }

        static bool ValEq(Value a, Value b)
        {
            if (a is MatrixValue x && b is MatrixValue y)
            {
                if (x.IsChar || y.IsChar) return x.IsChar && y.IsChar && string.Equals(S(x, "findobj"), S(y, "findobj"), StringComparison.OrdinalIgnoreCase);
                if (x.Numel != y.Numel) return false;
                for (int i = 0; i < x.Numel; i++) if (x.Re[i] != y.Re[i]) return false;
                return true;
            }
            if (a is CellValue ca && b is CellValue cb) return ca.Data.Length == cb.Data.Length && ca.Data.Zip(cb.Data).All(t => ValEq(t.First, t.Second));
            return false;
        }

        static Value[] FindObj(Interpreter it, Value[] a, bool all, int nargout)
        {
            var g = it.Graphics; int k = 0;
            var roots = new List<GObject>();
            if (a.Length > 0 && a[0].Numel == 0 && a[0] is MatrixValue { IsChar: false }) return Out(new MatrixValue(0, 1));
            if (a.Length > 0 && a[0] is MatrixValue hm && !hm.IsChar)
            {
                foreach (var h in hm.Re) { var o = g.Lookup(h); if (o == null || o.Deleted) throw new OctaveException("findobj: invalid graphics handles in input HLIST"); roots.Add(o); }
                k = 1;
            }
            else if (a.Length > 0 && !IsString(a[0])) throw new OctaveException("findobj: properties and options must be strings");
            else roots.Add(g.Root);
            int max = int.MaxValue;
            if (k < a.Length && IsString(a[k]) && S(a[k], "findobj").Equals("flat", StringComparison.OrdinalIgnoreCase)) { max = 0; k++; }
            if (k + 1 < a.Length && IsString(a[k]) && S(a[k], "findobj") == "-depth") { max = (int)Math.Min(int.MaxValue, D(a[k + 1], "findobj")); k += 2; }
            // level-order collection, as Octave does
            var cand = new List<GObject>(roots);
            var level = new List<GObject>(roots);
            for (int d = 0; d < max && level.Count > 0; d++)
            {
                var next = new List<GObject>();
                foreach (var o in level) foreach (var c in g.ChildrenOf(o)) if (!c.Deleted && (all || !IsHidden(c))) next.Add(c);
                cand.AddRange(next); level = next;
            }
            // terms joined by operators; precedence: -and > -or > -xor
            var terms = new List<Func<GObject, bool>>(); var ops = new List<string>();
            string op = "-and"; bool neg = false;
            while (k < a.Length)
            {
                if (!IsString(a[k])) throw new OctaveException("findobj: properties and options must be strings");
                var s = S(a[k], "findobj");
                Func<GObject, bool>? term = null;
                switch (s.ToLowerInvariant())
                {
                    case "-and" or "-or" or "-xor": op = s.ToLowerInvariant(); k++; continue;
                    case "-not": neg = !neg; k++; continue;
                    case "-depth": k += 2; continue;
                    case "-property":
                    {
                        if (k + 1 >= a.Length) throw new OctaveException("findobj: inconsistent number of arguments");
                        var pn = S(a[k + 1], "findobj").ToLowerInvariant(); k += 2;
                        term = o => { try { GetAny(it, o, pn); return true; } catch (OctaveException) { return false; } };
                        break;
                    }
                    case "-regexp":
                    {
                        if (k + 2 >= a.Length) throw new OctaveException("findobj: inconsistent number of arguments");
                        var pn = S(a[k + 1], "findobj"); var rx = new System.Text.RegularExpressions.Regex(S(a[k + 2], "findobj")); k += 3;
                        term = o => { try { var v = GetAny(it, o, pn); return IsString(v) && rx.IsMatch(S(v, "findobj")); } catch (OctaveException) { return false; } };
                        break;
                    }
                    default:
                    {
                        if (k + 1 >= a.Length) throw new OctaveException("findobj: inconsistent number of arguments");
                        var pn = s; var want = a[k + 1]; k += 2;
                        term = o => { try { var v = GetAny(it, o, pn); return v is MatrixValue || want is MatrixValue ? ValEq(v, want) : IsEq(it, v, want); } catch (OctaveException) { return false; } };
                        break;
                    }
                }
                var t0 = term!; bool n0 = neg; neg = false;
                terms.Add(n0 ? o => !t0(o) : t0);
                if (terms.Count > 1) ops.Add(op);
                op = "-and";
            }
            Func<GObject, bool>? pred = null;
            if (terms.Count > 0)
                pred = o =>
                {
                    var vals = terms.Select(t => t(o)).ToList(); var os = new List<string>(ops);
                    foreach (var lvl in new[] { "-and", "-or", "-xor" })
                        for (int i = 0; i < os.Count;)
                            if (os[i] == lvl) { vals[i] = lvl == "-and" ? vals[i] && vals[i + 1] : lvl == "-or" ? vals[i] || vals[i + 1] : vals[i] ^ vals[i + 1]; vals.RemoveAt(i + 1); os.RemoveAt(i); }
                            else i++;
                    return vals[0];
                };
            var res = cand.Where(o => pred == null || pred(o)).Select(o => o.Handle).ToArray();
            return Out(res.Length == 0 ? new MatrixValue(0, 0) : new MatrixValue(res.Length, 1, res));
        }
    }
}
