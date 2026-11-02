// MatRewrite - GPL-3.0-or-later
// MATLAB-style interactive figure editing: menus, toolbar, Edit Plot selection, Property Inspector,
// context menus, Insert menu, data tips, zoom/pan, point dragging, data table, save/open .fig, Generate Code.
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MatRewrite.Core.Builtins;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;
using ScottPlot.WPF;
using SP = ScottPlot;

namespace MatRewrite.Plotting.Wpf;

public enum FigureMode { Normal, Edit, ZoomIn, ZoomOut, Pan, DataTip, PlaceText }

public sealed partial class FigureWindow
{
    /// <summary>Interpreter used for set/get from the GUI (set by the host).</summary>
    public static Interpreter? Interp;
    /// <summary>Runs MATLAB code in the host's command window (IDE); null = run directly.</summary>
    public static Action<string>? EvalSink;
    /// <summary>Most recently created / activated figure window (automation).</summary>
    public static FigureWindow? Latest;

    FigureMode mode = FigureMode.Normal;
    GObject? selected;
    readonly Dictionary<WpfPlot, Axes> plotAxes = new();
    readonly List<(Axes ax, double x, double y, string text)> datatips = new();
    PropertyInspector? inspector;
    ColumnDefinition inspCol = null!;
    GridSplitter splitter = null!;
    TextBlock status = null!;
    readonly Dictionary<FigureMode, ToggleButton> modeButtons = new();
    MenuItem miEdit = null!, miInspector = null!;
    (LineSeries l, int idx, WpfPlot wp, double[]? xl, double[]? yl)? drag;
    string? pendingText;

    public FigureMode Mode => mode;
    public GObject? Selected => selected;
    public PropertyInspector? Inspector => inspector;

    UIElement BuildChrome()
    {
        var root = new DockPanel { LastChildFill = true };
        var menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top); root.Children.Add(menu);
        var tb = BuildToolbar();
        DockPanel.SetDock(tb, Dock.Top); root.Children.Add(tb);
        var sb = new StatusBar();
        status = new TextBlock { Text = "Ready" };
        sb.Items.Add(status);
        DockPanel.SetDock(sb, Dock.Bottom); root.Children.Add(sb);
        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inspCol = new ColumnDefinition { Width = new GridLength(0) };
        main.ColumnDefinitions.Add(inspCol);
        Grid.SetColumn(grid, 0); main.Children.Add(grid);
        splitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Gainsboro, Visibility = Visibility.Collapsed };
        Grid.SetColumn(splitter, 1); main.Children.Add(splitter);
        inspector = new PropertyInspector(this);
        Grid.SetColumn(inspector, 2); main.Children.Add(inspector);
        root.Children.Add(main);
        PreviewKeyDown += (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (e.Key == Key.Delete && selected != null && mode == FigureMode.Edit) { DeleteSelected(); e.Handled = true; }
            if (e.Key == Key.Escape) { SetMode(FigureMode.Normal); e.Handled = true; }
        };
        Activated += (_, _) => Latest = this;
        Latest = this;
        return root;
    }

    // ------------------------------------------------------------------ menus
    static MenuItem MI(string header, Action? click = null, string? gesture = null)
    {
        var m = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        if (click != null) m.Click += (_, e) => { e.Handled = true; click(); };
        return m;
    }

    Menu BuildMenu()
    {
        var menu = new Menu();
        var file = MI("_File");
        file.Items.Add(MI("_Open Figure...", OpenFigDialog));
        file.Items.Add(MI("_Save Figure As...", () => SaveAsDialog("fig")));
        file.Items.Add(MI("_Export As (PNG/SVG/PDF)...", () => SaveAsDialog("png")));
        file.Items.Add(new Separator());
        file.Items.Add(MI("_Generate Code...", ShowGeneratedCode));
        file.Items.Add(MI("_Copy Figure", CopyFigure));
        file.Items.Add(new Separator());
        file.Items.Add(MI("_Close", Close));
        menu.Items.Add(file);

        var edit = MI("_Edit");
        miEdit = MI("_Edit Plot", () => SetMode(mode == FigureMode.Edit ? FigureMode.Normal : FigureMode.Edit));
        miEdit.IsCheckable = true;
        edit.Items.Add(miEdit);
        edit.Items.Add(MI("_Delete", DeleteSelected, "Del"));
        edit.Items.Add(MI("Edit _Data of Selected Line...", () => { if (selected is LineSeries l) ShowDataTable(l); else Status("Select a line first (Edit Plot mode)"); }));
        edit.Items.Add(new Separator());
        edit.Items.Add(MI("_Figure Properties...", () => Select(Figure, true)));
        edit.Items.Add(MI("_Axes Properties...", () => { if (CurAxes() is { } a) Select(a, true); }));
        edit.Items.Add(MI("_Current Object Properties...", () => Select(selected ?? (GObject?)CurAxes() ?? Figure, true)));
        menu.Items.Add(edit);

        var view = MI("_View");
        miInspector = MI("_Property Inspector", () => ShowInspector(inspCol.Width.Value == 0));
        miInspector.IsCheckable = true;
        view.Items.Add(miInspector);
        menu.Items.Add(view);

        var ins = MI("_Insert");
        ins.Items.Add(MI("_Title", () => InsertLabel("title")));
        ins.Items.Add(MI("_X Label", () => InsertLabel("xlabel")));
        ins.Items.Add(MI("_Y Label", () => InsertLabel("ylabel")));
        ins.Items.Add(MI("_Legend", () => { if (CurAxes() is { } a) { Call("legend", Num(a.Handle), Str("show")); RefreshAll(); } }));
        ins.Items.Add(MI("_Colorbar", () => { if (CurAxes() is { } a) { a.Colorbar = true; Touch(); RefreshAll(); } }));
        ins.Items.Add(new Separator());
        ins.Items.Add(MI("Te_xt", () =>
        {
            var s = Prompt("Insert Text", "Text:", "text");
            if (s == null) return;
            pendingText = s; SetMode(FigureMode.PlaceText); Status("Click in the axes to place the text");
        }));
        ins.Items.Add(MI("_Arrow", () => InsertAnnotation("arrow")));
        ins.Items.Add(MI("Li_ne", () => InsertAnnotation("line")));
        ins.Items.Add(MI("Text _Box", () => InsertAnnotation("textbox")));
        ins.Items.Add(MI("_Rectangle", () => InsertAnnotation("rectangle")));
        menu.Items.Add(ins);

        var tools = MI("_Tools");
        tools.Items.Add(MI("Zoom _In", () => SetMode(FigureMode.ZoomIn)));
        tools.Items.Add(MI("Zoom _Out", () => SetMode(FigureMode.ZoomOut)));
        tools.Items.Add(MI("_Pan", () => SetMode(FigureMode.Pan)));
        tools.Items.Add(MI("_Data Tips", () => SetMode(FigureMode.DataTip)));
        tools.Items.Add(MI("_Clear Data Tips", () => { datatips.Clear(); RefreshAll(); }));
        tools.Items.Add(MI("_Reset View", ResetView));
        tools.Items.Add(new Separator());
        tools.Items.Add(MI("_Grid On/Off", () => { if (CurAxes() is { } a) SetProp(a, "xgrid", OnOff(!(a.Grid))); }));
        tools.Items.Add(MI("_Box On/Off", () => { if (CurAxes() is { } a) SetProp(a, "box", OnOff(!a.Box)); }));
        menu.Items.Add(tools);
        return menu;
    }

    static ToggleButton TB(string text, string tip) => new() { Content = text, ToolTip = tip, Margin = new Thickness(1), Padding = new Thickness(6, 1, 6, 1), FontSize = 12 };

    ToolBar BuildToolbar()
    {
        var tb = new ToolBar();
        var save = new Button { Content = "💾", ToolTip = "Save figure (.png / .svg / .pdf / .fig)", Padding = new Thickness(6, 1, 6, 1) };
        save.Click += (_, _) => SaveAsDialog("png");
        tb.Items.Add(save);
        tb.Items.Add(new Separator());
        foreach (var (m, t, tip) in new[] { (FigureMode.Edit, "↖ Edit", "Edit Plot: select objects, drag points"), (FigureMode.ZoomIn, "🔍+", "Zoom in (click or drag a box)"),
            (FigureMode.ZoomOut, "🔍−", "Zoom out"), (FigureMode.Pan, "✋", "Pan"), (FigureMode.DataTip, "⌖ Tip", "Data tips") })
        {
            var b = TB(t, tip);
            var mm = m;
            b.Click += (_, _) => SetMode(b.IsChecked == true ? mm : FigureMode.Normal);
            modeButtons[m] = b; tb.Items.Add(b);
        }
        var reset = new Button { Content = "⟲", ToolTip = "Reset view", Padding = new Thickness(6, 1, 6, 1) };
        reset.Click += (_, _) => ResetView();
        tb.Items.Add(reset);
        tb.Items.Add(new Separator());
        var insp = new Button { Content = "☰ Inspector", ToolTip = "Show / hide the Property Inspector", Padding = new Thickness(6, 1, 6, 1) };
        insp.Click += (_, _) => ShowInspector(inspCol.Width.Value == 0);
        tb.Items.Add(insp);
        var code = new Button { Content = "{ } Code", ToolTip = "Generate MATLAB code for this figure", Padding = new Thickness(6, 1, 6, 1) };
        code.Click += (_, _) => ShowGeneratedCode();
        tb.Items.Add(code);
        return tb;
    }

    // ------------------------------------------------------------------ modes, selection
    public void SetMode(FigureMode m)
    {
        mode = m;
        foreach (var (k, b) in modeButtons) b.IsChecked = k == m;
        if (miEdit != null) miEdit.IsChecked = m == FigureMode.Edit;
        foreach (var wp in plotAxes.Keys) ConfigureInput(wp);
        Cursor = m switch { FigureMode.ZoomIn or FigureMode.ZoomOut => Cursors.Cross, FigureMode.Pan => Cursors.SizeAll, FigureMode.DataTip or FigureMode.PlaceText => Cursors.Cross, _ => null };
        Status(m switch
        {
            FigureMode.Edit => "Edit Plot: click to select, double-click to open the Property Inspector, drag a point of the selected line to move it, Del deletes",
            FigureMode.ZoomIn => "Zoom in: click to zoom, drag a box (right-drag) or use the wheel",
            FigureMode.ZoomOut => "Zoom out: click to zoom out",
            FigureMode.Pan => "Pan: drag to move the view, wheel to zoom",
            FigureMode.DataTip => "Data tips: click near a data point",
            _ => "Ready"
        });
        if (m != FigureMode.Edit && selected != null && m != FigureMode.PlaceText) { }
    }

    void ConfigureInput(WpfPlot wp)
    {
        if (mode is FigureMode.Pan or FigureMode.ZoomIn or FigureMode.Normal) wp.UserInputProcessor.Enable();
        else wp.UserInputProcessor.Disable();
    }

    public void Select(GObject? o, bool openInspector = false)
    {
        selected = o;
        if (o != null && o is not MatRewrite.Core.Graphics.Figure && mode != FigureMode.Edit) SetMode(FigureMode.Edit);
        if (openInspector) ShowInspector(true);
        inspector?.Show(o ?? Figure);
        RefreshAll();
        if (o != null) Status("Selected: " + Describe(o));
    }

    public void ShowInspector(bool show)
    {
        inspCol.Width = show ? new GridLength(330) : new GridLength(0);
        splitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (miInspector != null) miInspector.IsChecked = show;
        if (show) inspector?.Show(selected ?? (GObject?)CurAxes() ?? Figure);
    }

    internal string Describe(GObject o) => o switch
    {
        Figure f => $"Figure {f.Handle}",
        Axes a => $"Axes {Figure.Axes.IndexOf(a) + 1}",
        LabelObject l => l.Which switch { "title" => "Title", "xlabel" => "X Label", "ylabel" => "Y Label", _ => "Z Label" } + (l.Text != "" ? $" \"{l.Text}\"" : ""),
        GenericObject g => char.ToUpper(g.Type[0]) + g.Type[1..],
        LineSeries l => "Line" + (l.DisplayName != "" ? $" \"{l.DisplayName}\"" : $" {l.Parent.Children.IndexOf(l) + 1}"),
        TextSeries t => $"Text \"{t.Text}\"",
        Series s => FigureEditApi.TypeOf(s) + (s.DisplayName != "" ? $" \"{s.DisplayName}\"" : ""),
        _ => FigureEditApi.TypeOf(o)
    };

    internal Axes? CurAxes() => (selected switch { Axes a => a, Series s => s.Parent, LabelObject l => l.Axes, _ => null }) ?? Figure.CurrentAxes ?? Figure.Axes.LastOrDefault();

    // ------------------------------------------------------------------ set / get through the property system
    static Value Str(string s) => MatrixValue.Str(s);
    static Value Num(double d) => MatrixValue.Scalar(d);
    static Value OnOff(bool b) => Str(b ? "on" : "off");

    internal bool SetProp(GObject o, string prop, Value v, bool refresh = true)
    {
        try
        {
            if (Interp == null) throw new InvalidOperationException("no interpreter attached");
            FigureEditApi.Set(Interp, o, prop, v);
            if (refresh) RefreshAll();
            Status($"set {Describe(o)}.{prop} = {FigureEditApi.ToText(v)}");
            return true;
        }
        catch (Exception ex) { Status("error: " + ex.Message, true); return false; }
    }

    internal Value? GetProp(GObject o, string prop)
    {
        try { return Interp == null ? null : FigureEditApi.Get(Interp, o, prop); } catch { return null; }
    }

    void Call(string fn, params Value[] args)
    {
        try { Interp?.CallFunction(fn, args, 0); }
        catch (Exception ex) { Status("error: " + ex.Message, true); }
    }

    void Touch() => Interp?.Graphics.Touch(Figure);

    void RunCode(string code)
    {
        if (EvalSink != null) { EvalSink(code); return; }
        try { Interp?.Eval(code); Interp?.Graphics.Flush(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "MatOct", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    internal void Status(string s, bool error = false)
    {
        if (status == null) return;
        status.Text = s; status.Foreground = error ? Brushes.Firebrick : Brushes.Black;
    }

    /// <summary>Re-render everything and keep the inspector in sync.</summary>
    public void RefreshAll()
    {
        Refresh();
        inspector?.Reload();
    }

    // ------------------------------------------------------------------ plot hooks (called from Refresh)
    void HookPlot(WpfPlot wp, Axes ax)
    {
        plotAxes[wp] = ax;
        ConfigureInput(wp);
        wp.Menu?.Clear();
        // selection highlight
        if (selected is LineSeries sl && sl.Parent == ax && !sl.Deleted && sl.X.Length > 0)
        {
            var hl = wp.Plot.Add.ScatterPoints(sl.X, sl.Y);
            hl.MarkerShape = SP.MarkerShape.OpenSquare; hl.MarkerSize = 7; hl.Color = new SP.Color(20, 20, 20);
        }
        else if (selected is ScatterSeries ss && ss.Parent == ax && ss.X.Length > 0)
        {
            var hl = wp.Plot.Add.ScatterPoints(ss.X, ss.Y);
            hl.MarkerShape = SP.MarkerShape.OpenSquare; hl.MarkerSize = 12; hl.Color = new SP.Color(20, 20, 20);
        }
        foreach (var (dax, x, y, t) in datatips.Where(d => d.ax == ax))
        {
            var mk = wp.Plot.Add.Marker(x, y); mk.Size = 8; mk.Color = SP.Colors.Black; mk.Shape = SP.MarkerShape.FilledSquare;
            var tx = wp.Plot.Add.Text(t, x, y);
            tx.LabelFontSize = 11; tx.LabelBackgroundColor = new SP.Color(255, 255, 225); tx.LabelBorderColor = SP.Colors.Black; tx.LabelBorderWidth = 1;
            tx.LabelPadding = 4; tx.LabelOffsetX = 8; tx.LabelOffsetY = -8; tx.LabelAlignment = SP.Alignment.LowerLeft;
        }
        if (selected == ax || (selected is LabelObject lo && lo.Axes == ax) || (selected is GenericObject g && g.Parent == ax))
        {
            wp.Plot.Axes.FrameColor(new SP.Color(0, 120, 215)); wp.Plot.Axes.FrameWidth(2);
        }
        wp.PreviewMouseLeftButtonDown += (s, e) => OnPlotDown(wp, e);
        wp.PreviewMouseMove += (s, e) => OnPlotMove(wp, e);
        wp.PreviewMouseLeftButtonUp += (s, e) => OnPlotUp(wp, e);
        wp.PreviewMouseRightButtonUp += (s, e) => OnPlotRight(wp, e);
        wp.MouseUp += (_, _) => SyncLimits(wp);
        wp.MouseWheel += (_, _) => Dispatcher.BeginInvoke(() => SyncLimits(wp));
    }

    void SyncLimits(WpfPlot wp)
    {
        if (mode is not (FigureMode.Pan or FigureMode.ZoomIn or FigureMode.ZoomOut or FigureMode.Normal) || !plotAxes.TryGetValue(wp, out var ax)) return;
        var lim = wp.Plot.Axes.GetLimits();
        if (ax.XLog || ax.YLog) return;
        ax.XLim = new[] { lim.Left, lim.Right }; ax.YLim = new[] { lim.Bottom, lim.Top };
        if (inspector?.Target == ax) inspector.Reload();
    }

    SP.Pixel Px(WpfPlot wp, MouseEventArgs e)
    {
        var p = e.GetPosition(wp);
        return new SP.Pixel((float)(p.X * wp.DisplayScale), (float)(p.Y * wp.DisplayScale));
    }

    static double Dist(SP.Pixel a, SP.Pixel b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    static double SegDist(SP.Pixel p, SP.Pixel a, SP.Pixel b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        if (l2 < 1e-9) return Dist(p, a);
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        return Dist(p, new SP.Pixel((float)(a.X + t * dx), (float)(a.Y + t * dy)));
    }

    SP.Pixel ToPx(WpfPlot wp, double x, double y) => wp.Plot.GetPixel(new SP.Coordinates(x, y));

    /// <summary>Nearest data point of a line/scatter within tol pixels.</summary>
    (Series s, int idx, double d)? NearestPoint(WpfPlot wp, Axes ax, SP.Pixel p, double tol)
    {
        (Series, int, double)? best = null;
        foreach (var s in ax.Children)
        {
            var (xs, ys) = s switch { LineSeries l => (l.X, l.Y), ScatterSeries sc => (sc.X, sc.Y), _ => (Array.Empty<double>(), Array.Empty<double>()) };
            for (int i = 0; i < xs.Length && i < ys.Length; i++)
            {
                if (!double.IsFinite(xs[i]) || !double.IsFinite(ys[i])) continue;
                var d = Dist(p, ToPx(wp, xs[i], ys[i]));
                if (d < tol && (best == null || d < best.Value.Item3)) best = (s, i, d);
            }
        }
        return best;
    }

    /// <summary>Which object is under the pixel: line/scatter/text/bar, legend, title/labels, else the axes.</summary>
    GObject HitTest(WpfPlot wp, Axes ax, SP.Pixel p)
    {
        var dr = wp.Plot.RenderManager.LastRender.DataRect;
        // legend (estimated from its rendered size and alignment)
        if (ax.LegendVisible && wp.Plot.Legend.IsVisible && Interp != null)
        {
            try
            {
                using var img = wp.Plot.Legend.GetImage();
                float lw = img.Width, lh = img.Height, m = 10;
                var al = wp.Plot.Legend.Alignment;
                float lx = al is SP.Alignment.UpperLeft or SP.Alignment.MiddleLeft or SP.Alignment.LowerLeft ? dr.Left + m : al is SP.Alignment.UpperCenter or SP.Alignment.MiddleCenter or SP.Alignment.LowerCenter ? dr.HorizontalCenter - lw / 2 : dr.Right - m - lw;
                float ly = al is SP.Alignment.UpperLeft or SP.Alignment.UpperCenter or SP.Alignment.UpperRight ? dr.Top + m : al is SP.Alignment.MiddleLeft or SP.Alignment.MiddleCenter or SP.Alignment.MiddleRight ? dr.VerticalCenter - lh / 2 : dr.Bottom - m - lh;
                if (p.X >= lx && p.X <= lx + lw && p.Y >= ly && p.Y <= ly + lh) return FigureEditApi.Legend(Interp, ax);
            }
            catch { }
        }
        if (Interp != null)
        {
            if (p.Y < dr.Top && p.X > dr.Left && p.X < dr.Right && ax.Title != "") return Interp.Graphics.Label(ax, "title");
            if (p.Y > dr.Bottom + 18 && p.X > dr.Left && p.X < dr.Right && ax.XLabel != "") return Interp.Graphics.Label(ax, "xlabel");
            if (p.X < dr.Left - 22 && p.Y > dr.Top && p.Y < dr.Bottom && ax.YLabel != "") return Interp.Graphics.Label(ax, "ylabel");
        }
        GObject? best = null; double bd = 7;
        for (int k = ax.Children.Count - 1; k >= 0; k--)
        {
            var s = ax.Children[k];
            switch (s)
            {
                case LineSeries l:
                    for (int i = 0; i < l.X.Length && i < l.Y.Length; i++)
                    {
                        var a = ToPx(wp, l.X[i], l.Y[i]);
                        double d = i + 1 < l.X.Length && l.Style != LineStyle.None ? SegDist(p, a, ToPx(wp, l.X[i + 1], l.Y[i + 1])) : Dist(p, a);
                        if (d < bd) { bd = d; best = l; }
                    }
                    break;
                case ScatterSeries sc:
                    for (int i = 0; i < sc.X.Length; i++) { var d = Dist(p, ToPx(wp, sc.X[i], sc.Y[i])); if (d < bd + 3) { bd = d; best = sc; } }
                    break;
                case TextSeries t:
                    { var a = ToPx(wp, t.X, t.Y); if (p.X > a.X - 10 && p.X < a.X + 8 * t.Text.Length + 10 && Math.Abs(p.Y - a.Y) < 14) return t; }
                    break;
                case BarSeries b when best == null:
                    for (int i = 0; i < b.X.Length; i++)
                    {
                        double off = b.GroupCount > 1 ? (b.GroupIndex - (b.GroupCount - 1) / 2.0) * b.Width / b.GroupCount : 0, hw = b.Width / Math.Max(1, b.GroupCount) / 2;
                        var c0 = b.Horizontal ? ToPx(wp, 0, b.X[i] + off - hw) : ToPx(wp, b.X[i] + off - hw, 0);
                        var c1 = b.Horizontal ? ToPx(wp, b.Y[i], b.X[i] + off + hw) : ToPx(wp, b.X[i] + off + hw, b.Y[i]);
                        if (p.X >= Math.Min(c0.X, c1.X) && p.X <= Math.Max(c0.X, c1.X) && p.Y >= Math.Min(c0.Y, c1.Y) && p.Y <= Math.Max(c0.Y, c1.Y)) best = b;
                    }
                    break;
            }
        }
        return best ?? ax;
    }

    void OnPlotDown(WpfPlot wp, MouseButtonEventArgs e)
    {
        if (!plotAxes.TryGetValue(wp, out var ax)) return;
        var p = Px(wp, e);
        Figure.CurrentAxes = ax;
        switch (mode)
        {
            case FigureMode.Edit:
            {
                if (selected is LineSeries sl && sl.Parent == ax && NearestPoint(wp, ax, p, 8) is { s: var s, idx: var i } && s == sl)
                {
                    drag = (sl, i, wp, ax.XLim, ax.YLim);
                    var lim = wp.Plot.Axes.GetLimits();
                    ax.XLim = new[] { lim.Left, lim.Right }; ax.YLim = new[] { lim.Bottom, lim.Top };
                    wp.CaptureMouse(); e.Handled = true;
                    return;
                }
                var hit = HitTest(wp, ax, p);
                if (e.ClickCount == 2) { Select(hit, true); e.Handled = true; return; }
                Select(hit);
                e.Handled = true;
                break;
            }
            case FigureMode.DataTip:
            {
                if (NearestPoint(wp, ax, p, 25) is { } np)
                {
                    var (xs, ys) = np.s is LineSeries l ? (l.X, l.Y) : (((ScatterSeries)np.s).X, ((ScatterSeries)np.s).Y);
                    datatips.Add((ax, xs[np.idx], ys[np.idx], $"X {xs[np.idx]:G6}\nY {ys[np.idx]:G6}"));
                    Status($"Data tip: ({xs[np.idx]:G6}, {ys[np.idx]:G6}) on {Describe(np.s)}");
                    RefreshAll();
                }
                e.Handled = true;
                break;
            }
            case FigureMode.ZoomIn when e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.None:
            case FigureMode.ZoomOut:
            {
                var c = wp.Plot.GetCoordinates(p);
                double f = mode == FigureMode.ZoomOut || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 2 : 0.5;
                var lim = wp.Plot.Axes.GetLimits();
                double hw = (lim.Right - lim.Left) * f / 2, hh = (lim.Top - lim.Bottom) * f / 2;
                ax.XLim = new[] { c.X - hw, c.X + hw }; ax.YLim = new[] { c.Y - hh, c.Y + hh };
                RefreshAll(); e.Handled = true;
                break;
            }
            case FigureMode.PlaceText when pendingText != null:
            {
                var c = wp.Plot.GetCoordinates(p);
                Call("text", Num(c.X), Num(c.Y), Str(pendingText));
                pendingText = null; SetMode(FigureMode.Edit); RefreshAll(); e.Handled = true;
                break;
            }
        }
    }

    void OnPlotMove(WpfPlot wp, MouseEventArgs e)
    {
        if (!plotAxes.TryGetValue(wp, out var ax)) return;
        var p = Px(wp, e);
        if (drag is { } d && e.LeftButton == MouseButtonState.Pressed)
        {
            var c = wp.Plot.GetCoordinates(p);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) d.l.Y[d.idx] = c.Y;
            else { d.l.X[d.idx] = c.X; d.l.Y[d.idx] = c.Y; }
            wp.Reset();
            FigureRenderer.Apply(wp.Plot, ax, Figure.Colormap);
            var hl = wp.Plot.Add.ScatterPoints(d.l.X, d.l.Y); hl.MarkerShape = SP.MarkerShape.OpenSquare; hl.MarkerSize = 7; hl.Color = new SP.Color(20, 20, 20);
            wp.Menu?.Clear();
            wp.UserInputProcessor.Disable();
            wp.Refresh();
            Status($"Point {d.idx + 1}: ({d.l.X[d.idx]:G6}, {d.l.Y[d.idx]:G6})  (Shift: move vertically only)");
            return;
        }
        var c2 = wp.Plot.GetCoordinates(p);
        if (mode is FigureMode.Normal or FigureMode.Edit or FigureMode.Pan or FigureMode.ZoomIn) status.Text = mode == FigureMode.Edit && selected != null ? $"Selected: {Describe(selected)}    x = {c2.X:G5}, y = {c2.Y:G5}" : $"x = {c2.X:G5}, y = {c2.Y:G5}";
    }

    void OnPlotUp(WpfPlot wp, MouseButtonEventArgs e)
    {
        if (drag is not { } d) return;
        drag = null;
        wp.ReleaseMouseCapture();
        var ax = d.l.Parent;
        ax.XLim = d.xl; ax.YLim = d.yl;
        SetProp(d.l, "xdata", MatrixValue.Row(d.l.X), false);
        SetProp(d.l, "ydata", MatrixValue.Row(d.l.Y), false);
        RefreshAll();
        e.Handled = true;
    }

    void OnPlotRight(WpfPlot wp, MouseButtonEventArgs e)
    {
        if (!plotAxes.TryGetValue(wp, out var ax)) return;
        var hit = HitTest(wp, ax, Px(wp, e));
        if (mode == FigureMode.Edit || hit is not Axes) Select(hit);
        var cm = BuildContextMenu(hit);
        cm.PlacementTarget = wp; cm.IsOpen = true;
        e.Handled = true;
    }

    // ------------------------------------------------------------------ context menus
    static readonly (string name, double r, double g, double b)[] Palette =
    {
        ("Blue", 0, 0.447, 0.741), ("Orange", 0.85, 0.325, 0.098), ("Yellow", 0.929, 0.694, 0.125), ("Purple", 0.494, 0.184, 0.556),
        ("Green", 0.466, 0.674, 0.188), ("Cyan", 0.301, 0.745, 0.933), ("Dark red", 0.635, 0.078, 0.184), ("Black", 0, 0, 0), ("Red", 1, 0, 0), ("Gray", 0.5, 0.5, 0.5)
    };

    MenuItem ColorMenu(string header, GObject o, string prop)
    {
        var m = MI(header);
        foreach (var (n, r, g, b) in Palette)
        {
            var it = MI(n, () => SetProp(o, prop, MatrixValue.Row(new[] { r, g, b })));
            it.Icon = new Border { Width = 14, Height = 14, Background = new SolidColorBrush(Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255))), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            m.Items.Add(it);
        }
        m.Items.Add(new Separator());
        m.Items.Add(MI("Custom...", () =>
        {
            var cur = GetProp(o, prop) as MatrixValue;
            var c = ColorPickerWindow.Pick(this, cur != null && !cur.IsChar && cur.Numel == 3 ? cur.Re : new double[] { 0, 0, 0 });
            if (c != null) SetProp(o, prop, MatrixValue.Row(c));
        }));
        return m;
    }

    MenuItem Choices(string header, GObject o, string prop, IEnumerable<(string label, Value v)> items)
    {
        var m = MI(header);
        var cur = GetProp(o, prop) is { } cv ? FigureEditApi.ToText(cv) : "";
        foreach (var (l, v) in items) { var it = MI(l, () => SetProp(o, prop, v)); it.IsChecked = FigureEditApi.ToText(v) == cur; m.Items.Add(it); }
        return m;
    }

    static IEnumerable<(string, Value)> S(params string[] xs) => xs.Select(x => (x, Str(x)));
    static IEnumerable<(string, Value)> N(params double[] xs) => xs.Select(x => (x.ToString(System.Globalization.CultureInfo.InvariantCulture), Num(x)));

    ContextMenu BuildContextMenu(GObject o)
    {
        var cm = new ContextMenu();
        cm.Items.Add(new MenuItem { Header = Describe(o), IsEnabled = false, FontWeight = FontWeights.Bold });
        switch (o)
        {
            case LineSeries l:
                cm.Items.Add(ColorMenu("Color", l, "color"));
                cm.Items.Add(Choices("Line Style", l, "linestyle", new[] { ("Solid  -", Str("-")), ("Dashed  --", Str("--")), ("Dotted  :", Str(":")), ("Dash-dot  -.", Str("-.")), ("None", Str("none")) }));
                cm.Items.Add(Choices("Line Width", l, "linewidth", N(0.5, 1, 1.5, 2, 3, 4, 6)));
                cm.Items.Add(Choices("Marker", l, "marker", new[] { ("None", "none"), ("Circle o", "o"), ("Plus +", "+"), ("Asterisk *", "*"), ("Point .", "."), ("Cross x", "x"), ("Square s", "s"),
                    ("Diamond d", "d"), ("Triangle up ^", "^"), ("Triangle down v", "v"), ("Triangle left <", "<"), ("Triangle right >", ">"), ("Pentagram p", "p"), ("Hexagram h", "h") }.Select(t => (t.Item1, Str(t.Item2)))));
                cm.Items.Add(Choices("Marker Size", l, "markersize", N(2, 4, 6, 8, 10, 12, 16)));
                cm.Items.Add(ColorMenu("Marker Face Color", l, "markerfacecolor"));
                cm.Items.Add(ColorMenu("Marker Edge Color", l, "markeredgecolor"));
                cm.Items.Add(new Separator());
                cm.Items.Add(MI("Edit Data...", () => ShowDataTable(l)));
                cm.Items.Add(MI("Add Data Tips", () => SetMode(FigureMode.DataTip)));
                break;
            case Axes a:
                cm.Items.Add(Choices("Grid", a, "xgrid", S("on", "off")).Also(m => m.Click += (_, _) => { }));
                cm.Items.Add(MI(a.MinorGrid ? "Minor Grid Off" : "Minor Grid On", () => { SetProp(a, "xminorgrid", OnOff(!a.MinorGrid), false); SetProp(a, "yminorgrid", OnOff(a.MinorGrid)); }));
                cm.Items.Add(Choices("Box", a, "box", S("on", "off")));
                cm.Items.Add(Choices("Font Size", a, "fontsize", N(8, 9, 10, 11, 12, 14, 16, 18, 20, 24)));
                cm.Items.Add(Choices("Font Name", a, "fontname", S("Helvetica", "Arial", "Times New Roman", "Courier New", "Consolas", "Segoe UI", "Calibri")));
                cm.Items.Add(Choices("Tick Direction", a, "tickdir", S("in", "out", "both", "none")));
                cm.Items.Add(Choices("X Scale", a, "xscale", S("linear", "log")));
                cm.Items.Add(Choices("Y Scale", a, "yscale", S("linear", "log")));
                cm.Items.Add(ColorMenu("Background Color", a, "color"));
                cm.Items.Add(new Separator());
                cm.Items.Add(MI(a.LegendVisible ? "Hide Legend" : "Show Legend", () => { Call("legend", Num(a.Handle), Str(a.LegendVisible ? "off" : "show")); RefreshAll(); }));
                cm.Items.Add(MI("Reset View", ResetView));
                cm.Items.Add(MI("Generate Code...", ShowGeneratedCode));
                break;
            case LabelObject or TextSeries:
                cm.Items.Add(MI("Edit String...", () =>
                {
                    var cur = GetProp(o, "string") is MatrixValue m && m.IsChar ? m.AsString() : "";
                    if (Prompt("Edit Text", "String:", cur) is { } s) SetProp(o, "string", Str(s));
                }));
                cm.Items.Add(Choices("Font Size", o, "fontsize", N(8, 10, 11, 12, 14, 16, 18, 20, 24)));
                cm.Items.Add(Choices("Font Weight", o, "fontweight", S("normal", "bold")));
                cm.Items.Add(Choices("Font Angle", o, "fontangle", S("normal", "italic")));
                cm.Items.Add(ColorMenu("Color", o, "color"));
                break;
            case GenericObject { Type: "legend" } lg:
                cm.Items.Add(Choices("Location", lg, "location", S("north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest", "best")));
                cm.Items.Add(Choices("Orientation", lg, "orientation", S("vertical", "horizontal")));
                cm.Items.Add(Choices("Font Size", lg, "fontsize", N(8, 9, 10, 11, 12, 14, 16)));
                cm.Items.Add(Choices("Box", lg, "box", S("on", "off")));
                cm.Items.Add(ColorMenu("Text Color", lg, "textcolor"));
                break;
            case Series s:
                if (GetProp(s, "facecolor") != null) cm.Items.Add(ColorMenu("Face Color", s, "facecolor"));
                else if (GetProp(s, "color") != null) cm.Items.Add(ColorMenu("Color", s, "color"));
                break;
        }
        cm.Items.Add(new Separator());
        cm.Items.Add(MI("Show Property Inspector", () => Select(o, true)));
        if (o is not MatRewrite.Core.Graphics.Axes and not MatRewrite.Core.Graphics.Figure) cm.Items.Add(MI("Delete", () => { selected = o; DeleteSelected(); }));
        return cm;
    }

    // ------------------------------------------------------------------ actions
    void DeleteSelected()
    {
        var o = selected;
        if (o == null || o is Figure) return;
        if (o is GenericObject { Type: "legend" } lg) Call("legend", Num(lg.Parent.Handle), Str("off"));
        else if (o is LabelObject lo) SetProp(lo, "string", Str(""), false);
        else Call("delete", Num(o.Handle));
        selected = null;
        inspector?.Show(CurAxes() ?? (GObject)Figure);
        RefreshAll();
    }

    void ResetView()
    {
        foreach (var a in Figure.Axes) { a.XLim = null; a.YLim = null; a.Props.Remove("xlim"); a.Props.Remove("ylim"); }
        RefreshAll();
    }

    void InsertLabel(string which)
    {
        if (CurAxes() is not { } a || Interp == null) return;
        var lo = Interp.Graphics.Label(a, which);
        var s = Prompt("Insert " + which, which + ":", lo.Text == "" ? which switch { "title" => "Title", "xlabel" => "X", _ => "Y" } : lo.Text);
        if (s != null) { SetProp(lo, "string", Str(s)); Select(lo); }
    }

    void InsertAnnotation(string kind)
    {
        if (kind == "textbox" || kind == "rectangle")
            Call("annotation", Str(kind), MatrixValue.Row(new[] { 0.2, 0.7, 0.25, 0.1 }), Str("String"), Str(kind == "textbox" ? Prompt("Text Box", "Text:", "text") ?? "" : ""));
        else Call("annotation", Str(kind), MatrixValue.Row(new[] { 0.3, 0.5 }), MatrixValue.Row(new[] { 0.6, 0.45 }));
        RefreshAll();
    }

    string? Prompt(string title, string label, string init)
    {
        var w = new Window { Title = title, Width = 380, SizeToContent = SizeToContent.Height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(new TextBlock { Text = label });
        var tb = new TextBox { Text = init, Margin = new Thickness(0, 4, 0, 8) };
        sp.Children.Add(tb);
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        string? res = null;
        ok.Click += (_, _) => { res = tb.Text; w.Close(); };
        sp.Children.Add(ok);
        w.Content = sp;
        w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        w.ShowDialog();
        return res;
    }

    void SaveAsDialog(string defExt)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = (Figure.Name == "" ? $"figure{Figure.Handle}" : Figure.Name),
            Filter = "PNG image (*.png)|*.png|SVG vector (*.svg)|*.svg|PDF document (*.pdf)|*.pdf|JPEG image (*.jpg)|*.jpg|MatOct figure (*.fig)|*.fig",
            FilterIndex = defExt == "fig" ? 5 : 1
        };
        if (dlg.ShowDialog(this) != true) return;
        SaveTo(dlg.FileName);
    }

    /// <summary>Save as .png/.svg/.pdf/.jpg or .fig (JSON).</summary>
    public void SaveTo(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".fig", StringComparison.OrdinalIgnoreCase)) File.WriteAllText(path, FigureEditApi.ToJson(Figure));
            else FigureRenderer.Save(Figure, path, Math.Max(100, (int)grid.ActualWidth), Math.Max(100, (int)grid.ActualHeight));
            Status("Saved " + path);
        }
        catch (Exception ex) { Status("error: " + ex.Message, true); }
    }

    void OpenFigDialog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "MatOct figure (*.fig)|*.fig|All files|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try { RunCode(FigureEditApi.CodeFromJson(File.ReadAllText(dlg.FileName))); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open Figure", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    void CopyFigure()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"matoct_fig_{Environment.ProcessId}.png");
        FigureRenderer.Save(Figure, tmp, Math.Max(100, (int)grid.ActualWidth), Math.Max(100, (int)grid.ActualHeight));
        Clipboard.SetImage(ToBitmap(File.ReadAllBytes(tmp)));
        Status("Figure copied to the clipboard");
    }

    void ShowGeneratedCode()
    {
        var code = FigureEditApi.GenerateCode(Figure, "createfigure");
        var w = new Window { Title = "Generated code - createfigure.m", Width = 760, Height = 520, Owner = this };
        var dp = new DockPanel();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        var tb = new TextBox { Text = code, FontFamily = new FontFamily("Consolas"), FontSize = 12, AcceptsReturn = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = false };
        var save = new Button { Content = "Save as .m...", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        save.Click += (_, _) =>
        {
            var d = new Microsoft.Win32.SaveFileDialog { FileName = "createfigure.m", Filter = "MATLAB code (*.m)|*.m" };
            if (d.ShowDialog(w) == true)
            {
                var fn = Path.GetFileNameWithoutExtension(d.FileName);
                File.WriteAllText(d.FileName, tb.Text.Replace("function fig = createfigure ()", $"function fig = {fn} ()"));
            }
        };
        var copy = new Button { Content = "Copy", Padding = new Thickness(8, 2, 8, 2) };
        copy.Click += (_, _) => Clipboard.SetText(tb.Text);
        bar.Children.Add(save); bar.Children.Add(copy);
        DockPanel.SetDock(bar, Dock.Top); dp.Children.Add(bar); dp.Children.Add(tb);
        w.Content = dp;
        w.Show();
    }

    public void ShowDataTable(LineSeries l)
    {
        var rows = new ObservableCollection<PointRow>(Enumerable.Range(0, Math.Min(l.X.Length, l.Y.Length)).Select(i => new PointRow { Index = i + 1, X = l.X[i], Y = l.Y[i] }));
        var w = new Window { Title = $"Data - {Describe(l)}", Width = 360, Height = 480, Owner = this };
        var dp = new DockPanel();
        var dg = new DataGrid { ItemsSource = rows, AutoGenerateColumns = true, CanUserAddRows = true, CanUserDeleteRows = true };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4) };
        void Apply()
        {
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            var pts = rows.Where(r => r != null).ToList();
            SetProp(l, "xdata", MatrixValue.Row(pts.Select(r => r.X)), false);
            SetProp(l, "ydata", MatrixValue.Row(pts.Select(r => r.Y)));
        }
        var ap = new Button { Content = "Apply", Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        ap.Click += (_, _) => Apply();
        var ok = new Button { Content = "OK", Width = 70, IsDefault = true };
        ok.Click += (_, _) => { Apply(); w.Close(); };
        bar.Children.Add(ap); bar.Children.Add(ok);
        DockPanel.SetDock(bar, Dock.Bottom); dp.Children.Add(bar); dp.Children.Add(dg);
        w.Content = dp;
        w.Show();
    }

    public sealed class PointRow { public int Index { get; set; } public double X { get; set; } public double Y { get; set; } }

    // ------------------------------------------------------------------ automation
    /// <summary>Save a PNG of the whole figure window (menus, toolbar, inspector included).</summary>
    public void SaveWindowScreenshot(string path)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var fe = (FrameworkElement)Content;
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(fe.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(fe.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        rtb.Render(fe);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path); enc.Save(fs);
    }

    /// <summary>Demo for screenshots: select the first line, open the inspector and edit some properties through it.</summary>
    public void DemoInspector(IEnumerable<(string prop, string text)> edits)
    {
        if (Width < 980) Width = 980;
        var line = Figure.Axes.SelectMany(a => a.Children).OfType<LineSeries>().FirstOrDefault();
        Select(line ?? (GObject?)CurAxes() ?? Figure, true);
        foreach (var (p, t) in edits) inspector?.EditText(p, t);
    }
}

static class FluentExt { public static T Also<T>(this T t, Action<T> a) { a(t); return t; } }

/// <summary>Simple colour picker: palette + RGB sliders + hex.</summary>
public sealed class ColorPickerWindow : Window
{
    double[]? result;
    public static double[]? Pick(Window owner, double[] init)
    {
        var w = new ColorPickerWindow(init) { Owner = owner };
        w.ShowDialog();
        return w.result;
    }

    ColorPickerWindow(double[] init)
    {
        Title = "Choose Color"; Width = 330; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sp = new StackPanel { Margin = new Thickness(10) };
        var preview = new Border { Height = 30, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8) };
        var sliders = new Slider[3];
        var hex = new TextBox { Margin = new Thickness(0, 6, 0, 6) };
        bool busy = false;
        void Upd(bool fromHex = false)
        {
            if (busy) return; busy = true;
            var c = Color.FromRgb((byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value);
            preview.Background = new SolidColorBrush(c);
            if (!fromHex) hex.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            busy = false;
        }
        var pal = new WrapPanel();
        var cols = new[] { "#0072BD", "#D95319", "#EDB120", "#7E2F8E", "#77AC30", "#4DBEEE", "#A2142F", "#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF",
            "#00FFFF", "#FF00FF", "#FFFF00", "#808080", "#C0C0C0", "#800000", "#008000", "#000080", "#FFA500", "#8B4513", "#FFC0CB", "#2F4F4F" };
        foreach (var h in cols)
        {
            var c = (Color)ColorConverter.ConvertFromString(h);
            var b = new Button { Width = 22, Height = 22, Margin = new Thickness(1), Background = new SolidColorBrush(c), ToolTip = h };
            b.Click += (_, _) => { sliders[0].Value = c.R; sliders[1].Value = c.G; sliders[2].Value = c.B; };
            pal.Children.Add(b);
        }
        sp.Children.Add(preview); sp.Children.Add(pal);
        string[] names = { "R", "G", "B" };
        for (int i = 0; i < 3; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
            var lbl = new TextBlock { Text = names[i], Width = 16 };
            sliders[i] = new Slider { Minimum = 0, Maximum = 255, Value = Math.Round(Math.Clamp(init.Length > i ? init[i] : 0, 0, 1) * 255), IsSnapToTickEnabled = true, TickFrequency = 1 };
            var val = new TextBlock { Width = 30, TextAlignment = TextAlignment.Right };
            var s = sliders[i];
            s.ValueChanged += (_, _) => { val.Text = ((int)s.Value).ToString(); Upd(); };
            val.Text = ((int)s.Value).ToString();
            DockPanel.SetDock(lbl, Dock.Left); DockPanel.SetDock(val, Dock.Right);
            row.Children.Add(lbl); row.Children.Add(val); row.Children.Add(s);
            sp.Children.Add(row);
        }
        hex.TextChanged += (_, _) =>
        {
            if (busy) return;
            try { var c = (Color)ColorConverter.ConvertFromString(hex.Text); busy = true; sliders[0].Value = c.R; sliders[1].Value = c.G; sliders[2].Value = c.B; busy = false; Upd(true); } catch { }
        };
        sp.Children.Add(hex);
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => { result = sliders.Select(x => Math.Round(x.Value / 255, 4)).ToArray(); Close(); };
        sp.Children.Add(ok);
        Content = sp;
        Upd();
    }
}
