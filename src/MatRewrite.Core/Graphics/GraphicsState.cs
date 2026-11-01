// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Runtime;

namespace MatRewrite.Core.Graphics;

/// <summary>All figures of one interpreter plus the handle registry.</summary>
public sealed class GraphicsState
{
    public readonly SortedDictionary<int, Figure> Figures = new();
    readonly Dictionary<double, GObject> handles = new();
    double nextHandle = -1000.5;
    public Figure? Current;
    public IGraphicsBackend? Backend;
    readonly HashSet<Figure> dirty = new();

    public GObject? Lookup(double h) => handles.TryGetValue(h, out var o) ? o : Figures.TryGetValue((int)h, out var f) && f.Handle == h ? f : null;

    internal T Register<T>(T o) where T : GObject
    {
        o.Handle = nextHandle; nextHandle -= 1.0;
        handles[o.Handle] = o;
        return o;
    }

    public Figure NewFigure(int? id = null)
    {
        int n = id ?? (Figures.Count == 0 ? 1 : Enumerable.Range(1, Figures.Count + 1).First(k => !Figures.ContainsKey(k)));
        var f = new Figure { Handle = n };
        Figures[n] = f;
        Current = f;
        Touch(f);
        return f;
    }

    public Figure Gcf() => Current ?? NewFigure();

    public Axes Gca()
    {
        var f = Gcf();
        if (f.CurrentAxes == null) { var a = Register(new Axes { Figure = f }); f.Axes.Add(a); f.CurrentAxes = a; }
        return f.CurrentAxes;
    }

    public Axes Subplot(int rows, int cols, int[] cells)
    {
        var f = Gcf();
        var a = f.Axes.FirstOrDefault(x => x.Rows == rows && x.Cols == cols && x.Cells.SequenceEqual(cells));
        if (a == null)
        {
            // remove axes that overlap the new one
            f.Axes.RemoveAll(x => x.Rows != rows || x.Cols != cols ? Overlaps(x, rows, cols, cells) : x.Cells.Intersect(cells).Any());
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
        Figures.Remove((int)f.Handle);
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
