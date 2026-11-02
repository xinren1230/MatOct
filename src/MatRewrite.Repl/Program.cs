// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Text;
using MatRewrite.Core.Runtime;
using MatRewrite.Core.Builtins;

namespace MatRewrite.Repl;

public static class Program
{
    public static int Main(string[] argv)
    {
        if (argv.Length >= 2 && argv[0] == "--test") return TestDriver.RunOne(argv[1]);
        if (argv.Length >= 2 && argv[0] == "--octave-tests") return TestDriver.RunAll(argv.Skip(1).ToArray());
        Console.OutputEncoding = Encoding.UTF8;
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
        var it = new Interpreter(stdout);
        if (!argv.Contains("--no-pathdef")) PathManager.LoadStartup(it);
        bool gui = !argv.Contains("--no-gui");
#if WPF_FIGURES
        MatRewrite.Plotting.Wpf.WpfBackend? wpf = null;
        if (gui) { wpf = new MatRewrite.Plotting.Wpf.WpfBackend(); MatRewrite.Plotting.Wpf.FigureWindow.CallbackSink = it.Graphics.RaiseCallback; it.Graphics.Backend = wpf; wpf.WindowClosed += f => { try { it.Graphics.Close(f); } catch { } }; }
        else it.Graphics.Backend = new MatRewrite.Plotting.FileOnlyBackend();
#else
        it.Graphics.Backend = new MatRewrite.Plotting.FileOnlyBackend();
#endif
        var args = argv.ToList();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; it.RequestInterrupt(); };
        string? evalCode = null;
        bool quiet = false;
        for (int k = 0; k < args.Count; k++)
        {
            if (args[k] is "--eval" or "-e" && k + 1 < args.Count) { evalCode = args[k + 1]; args.RemoveRange(k, 2); k--; }
            else if (args[k] == "--no-pathdef") { args.RemoveAt(k); k--; }
            else if (args[k] is "-q" or "--quiet" or "--no-gui") { quiet = true; args.RemoveAt(k); k--; }
            else if (args[k] is "-p" or "--path" && k + 1 < args.Count) { it.SearchPath.Add(Path.GetFullPath(args[k + 1])); args.RemoveRange(k, 2); k--; }
            else if (args[k] == "--lib" && k + 1 < args.Count)
            {
                var lib = Path.GetFullPath(args[k + 1]);
                foreach (var d in new[] { lib }.Concat(Directory.GetDirectories(lib, "*", SearchOption.AllDirectories))
                    .Where(d => !d.Split(Path.DirectorySeparatorChar).Any(x => x == "private" || x.StartsWith("@") || x.StartsWith("+") || x == "deprecated")))
                    it.SearchPath.Add(d);
                it.PreferBuiltins = true; args.RemoveRange(k, 2); k--;
            }
            else if (args[k] is "-h" or "--help") { Console.WriteLine("usage: matoct|matrw [-q] [-p DIR] [--no-pathdef] [--eval CODE] [script.m]"); return 0; }
        }
        try
        {
            if (evalCode != null) return Run(it, () => it.Eval(evalCode), stdout);
            if (args.Count > 0)
            {
                var file = args[0];
                var dir = Path.GetDirectoryName(Path.GetFullPath(file));
                if (dir != null) it.SearchPath.Add(dir);
                int rc = Run(it, () => it.RunFile(file), stdout);
#if WPF_FIGURES
                if (wpf != null && wpf.OpenWindows > 0) { stdout.Flush(); Console.Error.WriteLine("(close all figure windows to exit)"); wpf.WaitForWindows(); }
#endif
                return rc;
            }
            if (!quiet)
            {
                stdout.WriteLine($"MatOct {BuiltinLibrary.Version} - a C# reimplementation of the GNU Octave language");
                stdout.WriteLine("Licensed under GPL-3.0-or-later. Type 'exit' to quit.\n");
            }
            return Interactive(it, stdout);
        }
        finally { stdout.Flush(); }
    }

    static int Run(Interpreter it, Action a, TextWriter o)
    {
        try { a(); return 0; }
        catch (Exception ex) when (Interpreter.IsQuit(ex, out int code)) { return code; }
        catch (InterruptException ie) { o.Flush(); if (ie.Message != "dbquit") Console.Error.WriteLine("^C"); return 130; }
        catch (Exception ex)
        {
            o.Flush();
            Console.Error.Write("error: " + Interpreter.ErrorInfo(ex).msg + "\n" + Interpreter.FormatStack(ex));
            if (Environment.GetEnvironmentVariable("MATRW_TRACE") == "1") { Exception? e = ex; while (e != null) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); e = e.InnerException ?? (e as OctaveException)?.Inner; } }
            return 1;
        }
    }

    static int Interactive(Interpreter it, StreamWriter o)
    {
        var buf = new StringBuilder();
        while (true)
        {
            o.Write(buf.Length == 0 ? ">> " : "> ");
            o.Flush();
            var line = Console.ReadLine();
            if (line == null) { o.WriteLine(); return 0; }
            buf.AppendLine(line);
            var code = buf.ToString();
            if (NeedsMore(code)) continue;
            buf.Clear();
            if (string.IsNullOrWhiteSpace(code)) continue;
            try { it.Eval(code); }
            catch (Exception ex) when (Interpreter.IsQuit(ex, out int c)) { return c; }
            catch (InterruptException ie) { o.Flush(); if (ie.Message != "dbquit") Console.Error.WriteLine("^C"); }
            catch (Exception ex)
            {
                o.Flush();
                Console.Error.Write("error: " + Interpreter.ErrorInfo(ex).msg + "\n" + Interpreter.FormatStack(ex));
                Console.Error.Flush();
            }
        }
    }

    /// <summary>Crude block-completeness check: open control keywords vs 'end'.</summary>
    static bool NeedsMore(string code)
    {
        try { MatRewrite.Core.Parsing.Parser.ParseProgram(code); return false; }
        catch (Exception ex)
        {
            var m = ex.Message;
            return m.Contains("found ''") || m.Contains("end of input") || m.Contains("unexpected EOF") || m.Contains("EOF") || code.TrimEnd().EndsWith("...");
        }
    }
}
