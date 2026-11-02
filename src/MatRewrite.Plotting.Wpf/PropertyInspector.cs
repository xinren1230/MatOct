// MatRewrite - GPL-3.0-or-later
// Property Inspector: live editing of handle-graphics properties via set/get.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MatRewrite.Core.Builtins;
using MatRewrite.Core.Graphics;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Plotting.Wpf;

public sealed class PropertyInspector : DockPanel
{
    readonly FigureWindow owner;
    readonly ComboBox objects = new() { Margin = new Thickness(4) };
    readonly TextBlock header = new() { FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(6, 2, 6, 4) };
    readonly StackPanel rows = new();
    readonly Dictionary<string, Action> reloaders = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Action<string>> setters = new(StringComparer.OrdinalIgnoreCase);
    bool loading;
    public GObject? Target { get; private set; }

    static readonly Dictionary<string, string[]> Curated = new()
    {
        ["line"] = new[] { "Color", "LineStyle", "LineWidth", "Marker", "MarkerSize", "MarkerEdgeColor", "MarkerFaceColor", "MarkerIndices", "DisplayName", "Visible", "XData", "YData" },
        ["axes"] = new[] { "FontName", "FontSize", "FontWeight", "FontAngle", "XLim", "YLim", "XScale", "YScale", "XTick", "YTick", "XTickLabel", "YTickLabel", "XTickLabelRotation",
            "TickDir", "TickLength", "Box", "XGrid", "YGrid", "XMinorGrid", "YMinorGrid", "GridColor", "GridLineStyle", "LineWidth", "XColor", "YColor", "Color" },
        ["text"] = new[] { "String", "FontName", "FontSize", "FontWeight", "FontAngle", "Color", "Interpreter", "Rotation", "HorizontalAlignment" },
        ["legend"] = new[] { "String", "Location", "Orientation", "FontName", "FontSize", "FontWeight", "TextColor", "Box", "Color", "Visible" },
        ["figure"] = new[] { "Name", "Color", "Colormap", "Position", "NumberTitle" },
        ["colorbar"] = new[] { "Location", "FontSize", "Color" },
        ["scatter"] = new[] { "MarkerFaceColor", "MarkerEdgeColor", "SizeData", "Marker", "DisplayName" },
        ["bar"] = new[] { "FaceColor", "EdgeColor", "BarWidth", "DisplayName" },
        ["patch"] = new[] { "FaceColor", "EdgeColor", "FaceAlpha", "LineWidth" },
    };

    static readonly string[] LineStyles = { "-", "--", ":", "-.", "none" };
    static readonly string[] Markers = { "none", "o", "+", "*", ".", "x", "s", "d", "^", "v", "<", ">", "p", "h" };
    static readonly string[] OnOffProps = { "box", "xgrid", "ygrid", "zgrid", "xminorgrid", "yminorgrid", "visible", "numbertitle", "clipping", "xminortick", "yminortick" };

    static string[]? Enum(string p) => p.ToLowerInvariant() switch
    {
        "linestyle" or "gridlinestyle" => LineStyles,
        "marker" => Markers,
        "fontweight" => new[] { "normal", "bold" },
        "fontangle" => new[] { "normal", "italic" },
        "tickdir" => new[] { "in", "out", "both", "none" },
        "xscale" or "yscale" or "zscale" => new[] { "linear", "log" },
        "interpreter" => new[] { "tex", "latex", "none" },
        "location" => new[] { "north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest", "best", "northoutside", "southoutside", "eastoutside", "westoutside", "none" },
        "orientation" => new[] { "vertical", "horizontal" },
        "horizontalalignment" => new[] { "left", "center", "right" },
        "colormap" => new[] { "viridis", "parula", "jet", "hot", "cool", "gray", "bone", "copper", "hsv", "spring", "summer", "autumn", "winter", "turbo" },
        "fontname" => new[] { "Helvetica", "Arial", "Times New Roman", "Courier New", "Consolas", "Segoe UI", "Calibri", "Cambria", "Georgia", "Verdana" },
        "markeredgecolor" or "markerfacecolor" => null,
        _ => OnOffProps.Contains(p.ToLowerInvariant()) ? new[] { "on", "off" } : null
    };

    static bool IsColor(string p) { var l = p.ToLowerInvariant(); return l.EndsWith("color") && l != "colororder"; }
    static bool IsReadOnly(string p) => p.ToLowerInvariant() is "type" or "parent" or "children" or "beingdeleted" or "number" or "currentaxes" or "title" or "xlabel" or "ylabel" or "zlabel";

    public PropertyInspector(FigureWindow owner)
    {
        this.owner = owner;
        Background = new SolidColorBrush(Color.FromRgb(248, 248, 250));
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = "Property Inspector", FontSize = 11, Foreground = Brushes.DimGray, Margin = new Thickness(6, 4, 6, 0) });
        top.Children.Add(objects);
        top.Children.Add(header);
        SetDock(top, Dock.Top); Children.Add(top);
        Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        objects.SelectionChanged += (_, _) => { if (!loading && objects.SelectedItem is ComboBoxItem { Tag: GObject o }) owner.Select(o); };
    }

    void FillObjects()
    {
        loading = true;
        objects.Items.Clear();
        void Add(GObject o, int depth)
        {
            var it = new ComboBoxItem { Content = new string(' ', depth * 3) + owner.Describe(o), Tag = o };
            objects.Items.Add(it);
            if (o == Target) objects.SelectedItem = it;
        }
        var f = owner.Figure;
        Add(f, 0);
        foreach (var a in f.Axes.Where(a => !a.Deleted))
        {
            Add(a, 1);
            if (a.TitleObj != null && a.Title != "") Add(a.TitleObj, 2);
            if (a.XLabelObj != null && a.XLabel != "") Add(a.XLabelObj, 2);
            if (a.YLabelObj != null && a.YLabel != "") Add(a.YLabelObj, 2);
            if (a.LegendVisible && a.LegendObj != null) Add(a.LegendObj, 2);
            foreach (var s in a.Children.Where(s => !s.Deleted && s is not GenericObject)) Add(s, 2);
        }
        if (Target != null && objects.SelectedItem == null) Add(Target, 1);
        loading = false;
    }

    public void Show(GObject o)
    {
        Target = o;
        reloaders.Clear(); setters.Clear();
        rows.Children.Clear();
        FillObjects();
        var it = FigureWindow.Interp;
        var type = FigureEditApi.TypeOf(o);
        header.Text = owner.Describe(o) + "   (" + type + ")";
        if (it == null) { rows.Children.Add(new TextBlock { Text = "(no interpreter)" }); return; }
        var all = FigureEditApi.PropertyNames(it, o).ToList();
        var cur = Curated.TryGetValue(type, out var c) ? c : Array.Empty<string>();
        AddGroup("Common");
        foreach (var p in cur) AddRow(o, p);
        if (o is LineSeries l)
        {
            var b = new Button { Content = "Edit data table...", Margin = new Thickness(6, 4, 6, 4), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1) };
            b.Click += (_, _) => owner.ShowDataTable(l);
            rows.Children.Add(b);
        }
        var rest = all.Where(p => !cur.Contains(p, StringComparer.OrdinalIgnoreCase) && !p.EndsWith("fcn", StringComparison.OrdinalIgnoreCase)).ToList();
        if (rest.Count > 0)
        {
            var exp = new Expander { Header = $"All properties ({rest.Count})", Margin = new Thickness(2, 6, 2, 2) };
            var inner = new StackPanel();
            exp.Content = inner;
            exp.Expanded += (_, _) => { if (inner.Children.Count == 0) foreach (var p in rest) AddRow(o, p, inner); };
            rows.Children.Add(exp);
        }
    }

    void AddGroup(string title) => rows.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = Brushes.SteelBlue, Margin = new Thickness(6, 4, 6, 2) });

    void AddRow(GObject o, string prop, Panel? into = null)
    {
        var it = FigureWindow.Interp!;
        Value? v;
        try { v = FigureEditApi.Get(it, o, prop); } catch { return; }
        var g = new Grid { Margin = new Thickness(4, 1, 4, 1) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock { Text = prop, VerticalAlignment = VerticalAlignment.Center, ToolTip = prop };
        g.Children.Add(lbl);
        FrameworkElement ed;
        var key = prop.ToLowerInvariant();
        Value? Cur() { try { return FigureEditApi.Get(it, o, prop); } catch { return null; } }
        if (IsReadOnly(prop) || v is not (MatrixValue or CellValue))
        {
            ed = new TextBlock { Text = FigureEditApi.ToText(v), Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            reloaders[prop] = () => ((TextBlock)ed).Text = FigureEditApi.ToText(Cur() ?? v);
        }
        else if (IsColor(prop))
        {
            var dp = new DockPanel();
            var sw = new Button { Width = 24, Height = 20, Margin = new Thickness(0, 0, 4, 0), BorderBrush = Brushes.Gray, ToolTip = "Pick a colour" };
            var tb = new ComboBox { IsEditable = true, ItemsSource = key.StartsWith("marker") || key is "facecolor" or "edgecolor" or "color" ? new[] { "none", "auto", "r", "g", "b", "k", "w", "c", "m", "y" } : new[] { "r", "g", "b", "k", "w", "c", "m", "y" } };
            void Rel()
            {
                var cv = Cur(); var txt = cv == null ? "" : FigureEditApi.ToText(cv);
                loading = true; tb.Text = txt; loading = false;
                sw.Background = cv is MatrixValue m && !m.IsChar && m.Numel == 3 ? new SolidColorBrush(Color.FromRgb((byte)(m.Re[0] * 255), (byte)(m.Re[1] * 255), (byte)(m.Re[2] * 255))) : Brushes.Transparent;
            }
            sw.Click += (_, _) =>
            {
                var cv = Cur() as MatrixValue;
                var c = ColorPickerWindow.Pick(owner, cv != null && !cv.IsChar && cv.Numel == 3 ? cv.Re : new double[] { 0, 0, 0 });
                if (c != null) Commit(o, prop, MatrixValue.Row(c));
            };
            tb.SelectionChanged += (_, _) => { if (!loading && tb.SelectedItem is string s) Commit(o, prop, MatrixValue.Str(s)); };
            tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(o, prop, FigureEditApi.FromText(tb.Text, Cur())); };
            tb.LostKeyboardFocus += (_, _) => { if (!loading && tb.Text != FigureEditApi.ToText(Cur() ?? v)) Commit(o, prop, FigureEditApi.FromText(tb.Text, Cur())); };
            DockPanel.SetDock(sw, Dock.Left); dp.Children.Add(sw); dp.Children.Add(tb);
            ed = dp; reloaders[prop] = Rel; Rel();
            setters[prop] = t => Commit(o, prop, FigureEditApi.FromText(t, Cur()));
        }
        else if (Enum(prop) is { } choices)
        {
            var cb = new ComboBox { ItemsSource = choices, IsEditable = key == "fontname" };
            void Rel() { loading = true; var t = FigureEditApi.ToText(Cur() ?? v); cb.SelectedItem = choices.Contains(t) ? t : null; cb.Text = t; loading = false; }
            cb.SelectionChanged += (_, _) => { if (!loading && cb.SelectedItem is string s) Commit(o, prop, MatrixValue.Str(s)); };
            if (cb.IsEditable) cb.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(o, prop, MatrixValue.Str(cb.Text)); };
            ed = cb; reloaders[prop] = Rel; Rel();
            setters[prop] = t => Commit(o, prop, MatrixValue.Str(t));
        }
        else
        {
            var tb = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
            void Rel() { loading = true; tb.Text = FigureEditApi.ToText(Cur() ?? v); tb.ClearValue(BackgroundProperty); loading = false; }
            void Do() { if (!loading && tb.Text != FigureEditApi.ToText(Cur() ?? v)) Commit(o, prop, FigureEditApi.FromText(tb.Text, Cur())); }
            tb.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Do(); e.Handled = true; } };
            tb.LostKeyboardFocus += (_, _) => Do();
            // mouse wheel nudges scalar numeric values (live)
            tb.PreviewMouseWheel += (_, e) =>
            {
                if (Cur() is MatrixValue m && !m.IsChar && m.Numel == 1)
                {
                    double step = key.Contains("width") || key.Contains("alpha") || key == "ticklength" ? (key == "ticklength" ? 0.005 : 0.5) : 1;
                    var nv = Math.Max(0, m.Re[0] + (e.Delta > 0 ? step : -step));
                    Commit(o, prop, MatrixValue.Scalar(Math.Round(nv, 4)));
                    e.Handled = true;
                }
            };
            ed = tb; reloaders[prop] = Rel; Rel();
            setters[prop] = t => { tb.Text = t; Commit(o, prop, FigureEditApi.FromText(t, Cur())); };
        }
        Grid.SetColumn(ed, 1); g.Children.Add(ed);
        (into ?? rows).Children.Add(g);
    }

    void Commit(GObject o, string prop, Value v)
    {
        if (!owner.SetProp(o, prop, v)) { Reload(); return; }
    }

    /// <summary>Re-read every visible property (after a GUI or code change).</summary>
    public void Reload()
    {
        if (Target == null) return;
        if (Target.Deleted) { Show(owner.Figure); return; }
        foreach (var r in reloaders.Values) try { r(); } catch { }
        header.Text = owner.Describe(Target) + "   (" + FigureEditApi.TypeOf(Target) + ")";
    }

    /// <summary>Automation: type a value into a property's editor and commit it.</summary>
    public void EditText(string prop, string text)
    {
        if (setters.TryGetValue(prop, out var s)) s(text);
        else if (Target != null) Commit(Target, prop, FigureEditApi.FromText(text, null));
    }
}
