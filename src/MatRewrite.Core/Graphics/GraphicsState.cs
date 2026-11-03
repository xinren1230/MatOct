// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Graphics;

/// <summary>All figures of one interpreter plus the handle registry.</summary>
public sealed class GraphicsState
{
    public readonly SortedDictionary<int, Figure> Figures = new();
    /// <summary>Properties set on the root object (handle 0), e.g. defaultaxescolororder.</summary>
    public readonly Dictionary<string, Runtime.Value> RootProps = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<double, GObject> handles = new();
    double nextHandle = -1000.5;
    public Figure? Current;
    public IGraphicsBackend? Backend;
    readonly HashSet<Figure> dirty = new();

    public readonly RootObject Root = new();
    /// <summary>Object whose callback is executing (gcbo) and its figure (gcbf).</summary>
    public GObject? CallbackObject;
    /// <summary>Callbacks raised by a front-end (mouse clicks, resizes) waiting to run on the interpreter thread.</summary>
    public readonly System.Collections.Concurrent.ConcurrentQueue<(double handle, string prop)> PendingCallbacks = new();
    public void RaiseCallback(double handle, string prop) => PendingCallbacks.Enqueue((handle, prop));

    public GObject? Lookup(double h) => h == 0 ? Root : handles.TryGetValue(h, out var o) ? o : Figures.TryGetValue((int)h, out var f) && f.Handle == h ? f : null;

    public IEnumerable<GObject> AllObjects() => handles.Values.Concat(Figures.Values);

    public LabelObject Label(Axes a, string which)
    {
        var l = which switch { "title" => a.TitleObj, "xlabel" => a.XLabelObj, "ylabel" => a.YLabelObj, _ => a.ZLabelObj };
        if (l != null) return l;
        l = Register(new LabelObject { Axes = a, Which = which });
        switch (which) { case "title": a.TitleObj = l; break; case "xlabel": a.XLabelObj = l; break; case "ylabel": a.YLabelObj = l; break; default: a.ZLabelObj = l; break; }
        return l;
    }

    public Figure NewFigureNonInteger()
    {
        var f = new Figure();
        Register(f);
        Figures[-(int)Math.Abs(f.Handle) - 100000] = f; // keep out of the integer range
        Current = f; Touch(f);
        return f;
    }

    public Axes NewAxes(Figure f)
    {
        var a = Register(new Axes { Figure = f });
        f.Axes.Add(a); f.CurrentAxes = a; Touch(f);
        return a;
    }

    public GenericObject AddGeneric(GObject parent, string type)
    {
        var g = new GenericObject { Type = type };
        switch (parent)
        {
            case Axes ax: Add(ax, g); break;
            case GenericObject grp when grp.Parent != null: Add(grp.Parent, g); g.Group = grp; break;
            case Series s: Add(s.Parent, g); g.Group = s; break;
            default: g.Owner = parent; Register(g); if (parent is Figure f) { f.Others.Add(g); } else Root.Others.Add(g); break;
        }
        return g;
    }

    /// <summary>Parent object of o (null for the root).</summary>
    public GObject? ParentOf(GObject o) => o switch
    {
        RootObject => null,
        Figure => Root,
        Axes a => a.Figure,
        LabelObject l => l.Axes,
        GenericObject g when g.Owner != null => g.Owner,
        Series s => (GObject?)s.Group ?? s.Parent,
        Annotation => Current,
        _ => null
    };

    /// <summary>Children, most recent first (Octave order).</summary>
    public List<GObject> ChildrenOf(GObject o)
    {
        var r = new List<GObject>();
        switch (o)
        {
            case RootObject root:
                r.AddRange(Figures.Values.Reverse());
                break;
            case Figure f:
                r.AddRange(f.Others.AsEnumerable().Reverse());
                r.AddRange(f.Axes.AsEnumerable().Reverse());
                break;
            case Axes a:
                r.AddRange(a.Children.Where(c => c.Group == null).Reverse());
                break;
            case Series s:
                if (s.Parent != null) r.AddRange(s.Parent.Children.Where(c => c.Group == s).Reverse());
                break;
        }
        return r;
    }

    /// <summary>Remove an object and its descendants.</summary>
    public void Delete(GObject o)
    {
        if (o is RootObject || o.Deleted) return;
        if (o is Figure f) { o.Deleted = true; Close(f); return; }
        foreach (var c in ChildrenOf(o)) Delete(c);
        o.Deleted = true;
        handles.Remove(o.Handle);
        switch (o)
        {
            case Axes a:
                a.Figure.Axes.Remove(a);
                if (a.Figure.CurrentAxes == a) a.Figure.CurrentAxes = a.Figure.Axes.LastOrDefault();
                foreach (var c in a.Children) handles.Remove(c.Handle);
                Touch(a.Figure);
                break;
            case GenericObject g when g.Owner != null:
                if (g.Owner is Figure gf) gf.Others.Remove(g); else Root.Others.Remove(g);
                break;
            case Series s:
                s.Parent?.Children.Remove(s);
                if (s.Parent != null) Touch(s.Parent.Figure);
                break;
            case Annotation an:
                foreach (var fig in Figures.Values) fig.Annotations.Remove(an);
                break;
        }
    }

    public T Register<T>(T o) where T : GObject
    {
        o.Handle = nextHandle; nextHandle -= 1.0;
        handles[o.Handle] = o;
        return o;
    }

    public Figure NewFigure(int? id = null)
    {
        int n = id ?? (Figures.Count == 0 ? 1 : Enumerable.Range(1, Figures.Count + 1).First(k => !Figures.ContainsKey(k)));
        var f = new Figure { Handle = n };
        if (Figures.TryGetValue(n, out var old)) return old;
        Figures[n] = f;
        Current = f;
        Touch(f);
        return f;
    }

    public Figure Gcf() => Current ?? NewFigure();

    public Axes Gca()
    {
        var f = Gcf();
        if (f.CurrentAxes == null) NewAxes(f);
        return f.CurrentAxes;
    }

    public Axes Subplot(int rows, int cols, int[] cells)
    {
        var f = Gcf();
        var a = f.Axes.FirstOrDefault(x => x.Rows == rows && x.Cols == cols && x.Cells.SequenceEqual(cells));
        if (a == null)
        {
            // remove axes that overlap the new one
            foreach (var x in f.Axes.Where(x => !x.Props.ContainsKey("position") && !x.Props.ContainsKey("outerposition")).Where(x => x.Rows != rows || x.Cols != cols ? Overlaps(x, rows, cols, cells) : x.Cells.Intersect(cells).Any()).ToList()) Delete(x);
            a = Register(new Axes { Figure = f, Rows = rows, Cols = cols, Cells = cells });
            f.Axes.Add(a);
        }
        f.CurrentAxes = a;
        Touch(f);
        return a;
    }

    static (double x0, double y0, double x1, double y1) Rect(int rows, int cols, int[] cells)
    {
        double x0 = 1, y0 = 1, x1 = 0, y1 = 0;
        foreach (var c in cells)
        {
            int r = (c - 1) / cols, k = (c - 1) % cols;
            x0 = Math.Min(x0, (double)k / cols); x1 = Math.Max(x1, (double)(k + 1) / cols);
            y0 = Math.Min(y0, (double)r / rows); y1 = Math.Max(y1, (double)(r + 1) / rows);
        }
        return (x0, y0, x1, y1);
    }

    static bool Overlaps(Axes a, int rows, int cols, int[] cells)
    {
        var p = Rect(a.Rows, a.Cols, a.Cells); var q = Rect(rows, cols, cells);
        const double e = 1e-9;
        return p.x0 < q.x1 - e && q.x0 < p.x1 - e && p.y0 < q.y1 - e && q.y0 < p.y1 - e;
    }

    public T Add<T>(Axes a, T s) where T : Series
    {
        s.Parent = a;
        Register(s);
        a.Children.Add(s);
        Touch(a.Figure);
        return s;
    }

    public void Close(Figure f)
    {
        foreach (var kv in Figures.Where(kv => kv.Value == f).ToList()) Figures.Remove(kv.Key);
        f.Deleted = true;
        handles.Remove(f.Handle);
        foreach (var o in f.Others) handles.Remove(o.Handle);
        foreach (var a in f.Axes) { handles.Remove(a.Handle); foreach (var s in a.Children) handles.Remove(s.Handle); }
        dirty.Remove(f);
        if (Current == f) Current = Figures.Count > 0 ? Figures.Values.Last() : null;
        Backend?.Close(f);
    }

    public void CloseAll() { foreach (var f in Figures.Values.ToList()) Close(f); }

    public void Touch(Figure f) => dirty.Add(f);

    /// <summary>Push pending changes to the backend (called after every graphics builtin and at drawnow).</summary>
    public void Flush()
    {
        if (Backend == null) { dirty.Clear(); return; }
        foreach (var f in dirty.ToList()) if (f.Visible && Figures.ContainsValue(f)) Backend.Update(f);
        dirty.Clear();
    }
}
