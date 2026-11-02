// MatOct - GPL-3.0-or-later
using System;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MatRewrite.Ide;

public partial class MainWindow
{
    public void MarkAllClean() { foreach (var d in docs) d.Dirty = false; }

    /// <summary>Demo mode (--demo-type): types <paramref name="code"/> into a new editor, presses Run,
    /// then slowly rotates the 3-D view of the resulting figure. Used to record screen captures.</summary>
    public void StartDemo(string code, double charsPerSec, Action? done, string? recordDir = null, double fps = 12)
    {
        if (recordDir != null) StartFrameRecorder(recordDir, fps, ref done);
        Editor.Text = "";
        Editor.Focus();
        int i = 0; var typeClock = Stopwatch.StartNew();
        var tType = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000 / Math.Max(1, charsPerSec)) };
        tType.Tick += (_, _) =>
        {
            if (i >= code.Length)
            {
                tType.Stop();
                var tWait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                tWait.Tick += (_, _) => { tWait.Stop(); RunFinished += AfterRun; OnRun(this, new RoutedEventArgs()); };
                tWait.Start();
                return;
            }
            int target = Math.Min(code.Length, (int)(typeClock.Elapsed.TotalSeconds * charsPerSec) + 1);
            if (target > i) { Editor.Document.Insert(Editor.Document.TextLength, code.Substring(i, target - i)); i = target; }
            Editor.CaretOffset = Editor.Document.TextLength;
            Editor.TextArea.Caret.BringCaretToView();
        };
        tType.Start();

        void AfterRun()
        {
            RunFinished -= AfterRun;
            int step = 0; const int steps = 30;
            var tRot = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            tRot.Tick += (_, _) =>
            {
                tRot.Interval = TimeSpan.FromMilliseconds(110);
                if (step >= steps || busy)
                {
                    if (step >= steps) { tRot.Stop(); var tEnd = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) }; tEnd.Tick += (_, _) => { tEnd.Stop(); done?.Invoke(); }; tEnd.Start(); }
                    return;
                }
                step++;
                Execute($"view({-35 + step * 3}, 35);");
            };
            tRot.Start();
        }
    }

    /// <summary>Captures this window plus any figure windows on top of it into numbered BMP frames and
    /// writes an ffconcat list (frames.txt) with exact per-frame durations. Only MatOct's own windows are captured.</summary>
    void StartFrameRecorder(string dir, double fps, ref Action? done)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "f*.bmp")) File.Delete(f);
        var list = new StringBuilder("ffconcat version 1.0\n");
        var sw = Stopwatch.StartNew(); double last = 0; int n = 0; string? lastName = null;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000 / fps) };
        void Grab()
        {
            try
            {
                var bmp = Compose();
                var name = $"f{n++:D5}.bmp";
                var enc = new BmpBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(Path.Combine(dir, name))) enc.Save(fs);
                double t = sw.Elapsed.TotalSeconds;
                if (lastName != null) list.Append($"file {lastName}\nduration {(t - last).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}\n");
                lastName = name; last = t;
            }
            catch (Exception ex) { File.AppendAllText(Path.Combine(dir, "errors.txt"), ex + "\n"); }
        }
        timer.Tick += (_, _) => Grab();
        timer.Start();
        var inner = done;
        done = () =>
        {
            timer.Stop(); Grab();
            if (lastName != null) list.Append($"file {lastName}\nduration 0.1\nfile {lastName}\n");
            File.WriteAllText(Path.Combine(dir, "frames.txt"), list.ToString());
            inner?.Invoke();
        };
    }

    RenderTargetBitmap Compose()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var fe = (FrameworkElement)Content;
        int W = (int)Math.Ceiling(fe.ActualWidth * dpi.DpiScaleX) / 2 * 2, H = (int)Math.Ceiling(fe.ActualHeight * dpi.DpiScaleY) / 2 * 2;
        var origin = fe.PointToScreen(new Point(0, 0));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
            dc.DrawRectangle(new VisualBrush(fe) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
            foreach (Window w in Application.Current.Windows)
            {
                if (w == this || !w.IsVisible || w.Content is not FrameworkElement c || c.ActualWidth < 1) continue;
                var p = c.PointToScreen(new Point(0, 0));
                double x = (p.X - origin.X) / dpi.DpiScaleX, y = (p.Y - origin.Y) / dpi.DpiScaleY;
                var r = new Rect(x, y - 30, c.ActualWidth, 30);
                dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(160, 160, 160)), 1), new Rect(x - 1, y - 31, c.ActualWidth + 2, c.ActualHeight + 32));
                dc.DrawText(new FormattedText(w.Title, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, dpi.PixelsPerDip), new Point(x + 8, y - 24));
                dc.DrawRectangle(Brushes.White, null, new Rect(x, y, c.ActualWidth, c.ActualHeight));
                dc.DrawRectangle(new VisualBrush(c) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(x, y, c.ActualWidth, c.ActualHeight));
            }
        }
        var rtb = new RenderTargetBitmap(W, H, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        rtb.Render(dv);
        return rtb;
    }
}
