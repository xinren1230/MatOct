// MatRewrite - GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using MatRewrite.Core.Graphics;
using ScottPlot.WPF;

namespace MatRewrite.Plotting.Wpf;

/// <summary>A WPF window that shows one figure; each axes is a WpfPlot placed on a grid.</summary>
public sealed partial class FigureWindow : Window
{
    readonly Grid grid = new();
    public Figure Figure { get; }

    public FigureWindow(Figure f)
    {
        Figure = f;
        Content = BuildChrome();
        Width = f.Width; Height = f.Height;
        // Phase 6: forward UI events to the interpreter thread (run at drawnow/pause).
        SizeChanged += (_, e) =>
        {
            if (!e.PreviousSize.IsEmpty && e.PreviousSize.Width > 0)
            {
                Figure.Width = (int)ActualWidth; Figure.Height = (int)ActualHeight;
                if (HasCallback(Figure, "sizechangedfcn")) RaiseCb(Figure.Handle, "sizechangedfcn");
            }
        };
        PreviewMouseDown += (_, _) =>
        {
            var tgt = Figure.CurrentAxes ?? Figure.Axes.LastOrDefault();
            if (tgt != null && HasCallback(tgt, "buttondownfcn")) RaiseCb(tgt.Handle, "buttondownfcn");
            else if (HasCallback(Figure, "buttondownfcn")) RaiseCb(Figure.Handle, "buttondownfcn");
        };
        Closing += (_, e) =>
        {
            if (AllowClose || !HasCallback(Figure, "closerequestfcn", "closereq")) return;
            e.Cancel = true;
            RaiseCb(Figure.Handle, "closerequestfcn");
        };
        Refresh();
    }

    /// <summary>Set by the backend for programmatic closes (delete/close from the interpreter).</summary>
    public bool AllowClose;
    public static Action<double, string>? CallbackSink;
    static void RaiseCb(double h, string prop) => CallbackSink?.Invoke(h, prop);
    static bool HasCallback(GObject o, string prop, string? defaultName = null)
    {
        if (!o.Props.TryGetValue(prop, out var v)) return false;
        if (v is MatRewrite.Core.Runtime.MatrixValue m && m.IsEmpty) return false;
        if (defaultName != null && v is MatRewrite.Core.Runtime.MatrixValue s && s.IsChar && s.AsString() == defaultName) return false;
        return true;
    }

    public void Refresh()
    {
        Title = Figure.WindowTitle;
        plotAxes.Clear();
        grid.Children.Clear(); grid.RowDefinitions.Clear(); grid.ColumnDefinitions.Clear();
        var (rows, cols, cells) = FigureRenderer.Layout(Figure);
        for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < cols; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var (ax, r, c, rs, cs) in cells)
        {
            if (Renderer3D.Handles(ax))
            {
                var v3 = new Axes3DView(ax, Figure);
                Grid.SetRow(v3, r); Grid.SetColumn(v3, c); Grid.SetRowSpan(v3, rs); Grid.SetColumnSpan(v3, cs);
                grid.Children.Add(v3);
                continue;
            }
            var wp = new WpfPlot();
            FigureRenderer.Apply(wp.Plot, ax, Figure.Colormap, MatRewrite.Core.Graphics.FigStyle.Current.ScreenPxPerPt * Math.Max(1, wp.DisplayScale));
            HookPlot(wp, ax);
            Grid.SetRow(wp, r); Grid.SetColumn(wp, c); Grid.SetRowSpan(wp, rs); Grid.SetColumnSpan(wp, cs);
            grid.Children.Add(wp);
            wp.Refresh();
        }
        if (Figure.Annotations.Count > 0)
        {
            var overlay = new AnnotationOverlay(Figure) { IsHitTestVisible = false };
            if (rows > 0) Grid.SetRowSpan(overlay, rows);
            if (cols > 0) Grid.SetColumnSpan(overlay, cols);
            grid.Children.Add(overlay);
        }
    }

    internal static BitmapSource ToBitmap(byte[] png)
    {
        var bi = new BitmapImage();
        bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = new MemoryStream(png); bi.EndInit(); bi.Freeze();
        return bi;
    }
}

/// <summary>Interactive 3-D axes: drag to rotate (changes the axes' view), wheel to zoom, double-click to reset zoom.</summary>
public sealed class Axes3DView : Border
{
    readonly Axes ax; readonly Figure fig;
    readonly Image img = new() { Stretch = Stretch.None };
    Point? drag; double az0, el0;

    public Axes3DView(Axes ax, Figure fig)
    {
        this.ax = ax; this.fig = fig;
        Background = Brushes.White; Child = img; ClipToBounds = true;
        SizeChanged += (_, _) => Render();
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { ax.Zoom = 1; Render(); return; }
            drag = e.GetPosition(this); az0 = ax.Az; el0 = ax.El; CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (drag is not { } d || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            ax.Az = Math.Round(az0 - (p.X - d.X) * 0.5, 1);
            ax.El = Math.Clamp(Math.Round(el0 + (p.Y - d.Y) * 0.5, 1), -90, 90);
            if (ax.Az == 0 && ax.El == 90) ax.Az = 0.1; // keep it a 3-D view while dragging
            Render();
        };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        MouseWheel += (_, e) => { ax.Zoom = Math.Clamp(ax.Zoom * (e.Delta > 0 ? 1.1 : 1 / 1.1), 0.2, 10); Render(); };
        ToolTip = "Drag: rotate   Wheel: zoom   Double-click: reset zoom";
    }

    void Render()
    {
        int w = (int)ActualWidth, h = (int)ActualHeight;
        if (w < 10 || h < 10) return;
        img.Source = FigureWindow.ToBitmap(Renderer3D.RenderPng(ax, fig.Colormap, w, h));
    }
}

/// <summary>Transparent layer drawing figure annotations.</summary>
public sealed class AnnotationOverlay : Border
{
    readonly Image img = new() { Stretch = Stretch.None };
    public AnnotationOverlay(Figure f)
    {
        Child = img;
        SizeChanged += (_, _) =>
        {
            int w = (int)ActualWidth, h = (int)ActualHeight;
            if (w < 10 || h < 10) return;
            using var surf = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul));
            surf.Canvas.Clear(SkiaSharp.SKColors.Transparent);
            Renderer3D.DrawAnnotations(surf.Canvas, f, w, h);
            using var im = surf.Snapshot(); using var d = im.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            img.Source = FigureWindow.ToBitmap(d.ToArray());
        };
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
        w.AllowClose = true;
        w.Close();
    });

    public void Export(Figure f, string path, int width, int height) => FigureRenderer.Save(f, path, width, height);

    /// <summary>Block until every figure window has been closed by the user.</summary>
    public void WaitForWindows() { while (OpenWindows > 0) Thread.Sleep(100); }
}
