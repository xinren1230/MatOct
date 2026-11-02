using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MatRewrite.Ide;

/// <summary>
/// Usage: MatOct [script.m] [--open other.m]... [--break N]... [--breakif N expr]... [--run] [--input text] [--screenshot out.png] [--exit]
/// With --break and --screenshot the IDE captures the window when execution pauses at the breakpoint
/// (then continues); a second capture "out_done.png" is taken after the run finishes.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var w = new MainWindow();
        string? file = null, shot = null; bool run = false, exit = false; var breaks = new List<int>(); var condBreaks = new List<(int, string)>(); var extra = new List<string>(); string? spShot = null;
        var a = e.Args;
        for (int i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case "--run": run = true; break;
                case "--exit": exit = true; break;
                case "--screenshot" when i + 1 < a.Length: shot = Path.GetFullPath(a[++i]); break;
                case "--input" when i + 1 < a.Length: w.AutoInput = a[++i]; break;
                case "--breakif" when i + 2 < a.Length && int.TryParse(a[i + 1], out var cn): condBreaks.Add((cn, a[i + 2])); i += 2; break;
                case "--setpath-screenshot" when i + 1 < a.Length: spShot = Path.GetFullPath(a[++i]); break;
                case "--open" when i + 1 < a.Length: extra.Add(a[++i]); break;
                case "--break" when i + 1 < a.Length && int.TryParse(a[i + 1], out var n): breaks.Add(n); i++; break;
                default: if (!a[i].StartsWith("--")) file = a[i]; break;
            }
        }
        w.Show();
        foreach (var x in extra) if (File.Exists(x)) w.LoadFile(x);
        if (file != null && File.Exists(file))
        {
            w.LoadFile(file);
            foreach (var b in breaks) w.ToggleBreakpoint(b);
            foreach (var (l, c) in condBreaks) w.SetConditionalBreakpoint(l, c);
        }
        if (condBreaks.Count > 0) breaks.AddRange(condBreaks.Select(c => c.Item1));
        w.AutoScreenshot = breaks.Count > 0 ? shot : null;
        w.RunFinished += () =>
        {
            if (shot == null && !exit) return;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                if (shot != null)
                {
                    var p = breaks.Count > 0 ? Path.Combine(Path.GetDirectoryName(shot)!, Path.GetFileNameWithoutExtension(shot) + "_done.png") : shot;
                    try { w.SaveScreenshot(p); } catch (Exception ex) { File.WriteAllText(p + ".err.txt", ex.ToString()); }
                }
                if (exit) Shutdown();
            };
            t.Start();
        };
        if (spShot != null)
        {
            var sp = w.OpenSetPath();
            var t2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            t2.Tick += (_, _) =>
            {
                t2.Stop();
                try { sp.SaveScreenshot(spShot); } catch (Exception ex) { File.WriteAllText(spShot + ".err.txt", ex.ToString()); }
                try { w.SaveScreenshot(Path.Combine(Path.GetDirectoryName(spShot)!, Path.GetFileNameWithoutExtension(spShot) + "_main.png")); } catch { }
                if (exit) { sp.SuppressCloseQuestion = true; Shutdown(); }
            };
            t2.Start();
        }
        if (run && file != null)
            w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, w.RunCurrent);
    }
}
