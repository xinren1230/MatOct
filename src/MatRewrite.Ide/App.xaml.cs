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
        string? file = null, shot = null; bool run = false, exit = false; var breaks = new List<int>(); var condBreaks = new List<(int, string)>(); var extra = new List<string>(); string? spShot = null; string? helpShot = null, helpTopic = null; string? inspShot = null, varShot = null, varName = "A";
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
                case "--help-screenshot" when i + 1 < a.Length: helpShot = Path.GetFullPath(a[++i]); break;
                case "--var-screenshot" when i + 2 < a.Length: varName = a[++i]; varShot = Path.GetFullPath(a[++i]); break;
                case "--inspector-screenshot" when i + 1 < a.Length: inspShot = Path.GetFullPath(a[++i]); break;
                case "--help-topic" when i + 1 < a.Length: helpTopic = a[++i]; break;
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
        void VarShot()
        {
            try
            {
                var ve = w.OpenVariableEditor(varName);
                var t4 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                t4.Tick += (_, _) =>
                {
                    t4.Stop();
                    try { global::MatRewrite.Ide.MainWindow.SaveWindowPng(ve, varShot!); w.SaveScreenshot(Path.Combine(Path.GetDirectoryName(varShot!)!, Path.GetFileNameWithoutExtension(varShot!) + "_main.png")); }
                    catch (Exception ex) { File.WriteAllText(varShot + ".err.txt", ex.ToString()); }
                    if (exit) Shutdown();
                };
                t4.Start();
            }
            catch (Exception ex) { File.WriteAllText(varShot + ".err.txt", ex.ToString()); if (exit) Shutdown(); }
        }
        w.RunFinished += () =>
        {
            if (shot == null && !exit && inspShot == null && varShot == null) return;
            if (inspShot != null)
            {
                var edits = new (string, string)[] { ("Color", "[0.85 0.1 0.1]"), ("LineWidth", "3"), ("LineStyle", "--"), ("Marker", "s"), ("MarkerSize", "8"), ("MarkerFaceColor", "y"), ("DisplayName", "edited in inspector") };
                int step = -1;
                var ti = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                var dir = Path.Combine(Path.GetDirectoryName(inspShot)!, "frames");
                Directory.CreateDirectory(dir);
                ti.Tick += (_, _) =>
                {
                    var fw = MatRewrite.Plotting.Wpf.FigureWindow.Latest;
                    try
                    {
                        if (step >= 0) fw?.SaveWindowScreenshot(Path.Combine(dir, $"frame{step:00}.png"));
                        step++;
                        if (step == 0) fw?.DemoInspector(Array.Empty<(string, string)>());
                        else if (step <= edits.Length) fw?.DemoInspector(new[] { edits[step - 1] });
                        else
                        {
                            ti.Stop();
                            fw?.SaveWindowScreenshot(inspShot);
                            if (varShot != null) VarShot();
                            else if (exit) Shutdown();
                        }
                    }
                    catch (Exception ex) { ti.Stop(); File.WriteAllText(inspShot + ".err.txt", ex.ToString()); if (exit) Shutdown(); }
                };
                ti.Start();
                return;
            }
            if (varShot != null) { var tv = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) }; tv.Tick += (_, _) => { tv.Stop(); VarShot(); }; tv.Start(); return; }
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
        if (helpShot != null)
        {
            var hw = w.OpenHelp(helpTopic);
            var t3 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            t3.Tick += (_, _) => { t3.Stop(); try { hw.SaveScreenshot(helpShot); } catch (Exception ex) { File.WriteAllText(helpShot + ".err.txt", ex.ToString()); } if (exit && !run) Shutdown(); };
            t3.Start();
        }
        if (run && file != null)
            w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, w.RunCurrent);
    }
}
