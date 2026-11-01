// MatRewrite - GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MatRewrite.Core.Graphics;
using ScottPlot.WPF;

namespace MatRewrite.Plotting.Wpf;

/// <summary>A WPF window that shows one figure; each axes is a WpfPlot placed on a grid.</summary>
public sealed class FigureWindow : Window
{
    readonly Grid grid = new();
    public Figure Figure { get; }

    public FigureWindow(Figure f)
    {
        Figure = f;
        Content = grid;
        Width = f.Width; Height = f.Height;
        Refresh();
    }

    public void Refresh()
    {
        Title = Figure.WindowTitle;
        grid.Children.Clear(); grid.RowDefinitions.Clear(); grid.ColumnDefinitions.Clear();
        var (rows, cols, cells) = FigureRenderer.Layout(Figure);
        for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < cols; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var (ax, r, c, rs, cs) in cells)
        {
            var wp = new WpfPlot();
            FigureRenderer.Apply(wp.Plot, ax, Figure.Colormap);
            Grid.SetRow(wp, r); Grid.SetColumn(wp, c); Grid.SetRowSpan(wp, rs); Grid.SetColumnSpan(wp, cs);
            grid.Children.Add(wp);
            wp.Refresh();
        }
    }
}

/// <summary>
/// Graphics backend that shows figures as WPF windows. When no WPF Application is running
/// (console REPL) it starts its own STA dispatcher thread; inside the IDE it uses the app dispatcher.
/// </summary>
public sealed class WpfBackend : IGraphicsBackend
{
    readonly Dispatcher dispatcher;
    readonly Dictionary<Figure, FigureWindow> windows = new();
    /// <summary>Raised (on the UI thread) when the user closes a figure window.</summary>
    public event Action<Figure>? WindowClosed;

    public WpfBackend(Dispatcher? uiDispatcher = null)
    {
        if (uiDispatcher != null) { dispatcher = uiDispatcher; return; }
        Dispatcher? d = null;
        using var ready = new ManualResetEventSlim();
        var t = new Thread(() => { d = Dispatcher.CurrentDispatcher; ready.Set(); Dispatcher.Run(); }) { IsBackground = true, Name = "MatRewrite figures" };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        ready.Wait();
        dispatcher = d!;
    }

    public int OpenWindows { get { lock (windows) return windows.Count; } }

    public void Update(Figure f) => dispatcher.Invoke(() =>
    {
        if (!windows.TryGetValue(f, out var w))
        {
            w = new FigureWindow(f);
            w.Closed += (_, _) => { lock (windows) windows.Remove(f); WindowClosed?.Invoke(f); };
            lock (windows) windows[f] = w;
            w.Show();
        }
        else w.Refresh();
    });

    public void Close(Figure f) => dispatcher.Invoke(() =>
    {
        FigureWindow? w;
        lock (windows) { if (!windows.Remove(f, out w)) return; }
        w.Close();
    });

    public void Export(Figure f, string path, int width, int height) => FigureRenderer.Save(f, path, width, height);

    /// <summary>Block until every figure window has been closed by the user.</summary>
    public void WaitForWindows() { while (OpenWindows > 0) Thread.Sleep(100); }
}
