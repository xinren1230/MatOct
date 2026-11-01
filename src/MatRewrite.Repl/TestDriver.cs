// MatRewrite - GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MatRewrite.Core.Testing;

namespace MatRewrite.Repl;

/// <summary>Compatibility runner: executes GNU Octave's embedded %! tests, one child process per file.</summary>
static class TestDriver
{
    public static int RunOne(string file)
    {
        var lib = Environment.GetEnvironmentVariable("MATRW_TESTLIB");
        List<string>? path = null;
        if (!string.IsNullOrEmpty(lib))
            path = new[] { lib }.Concat(Directory.GetDirectories(lib, "*", SearchOption.AllDirectories))
                .Where(d => !d.Split(Path.DirectorySeparatorChar).Any(x => x == "private" || x.StartsWith("@") || x.StartsWith("+") || x == "deprecated"))
                .ToList();
        var r = OctaveTestRunner.RunFile(file, 10000, path, it => it.Graphics.Backend = new MatRewrite.Plotting.FileOnlyBackend());
        Console.Out.Write(OctaveTestRunner.Serialize(r));
        Console.Out.Flush();
        Environment.Exit(0);   // abandon any wedged background thread
        return 0;
    }

    /// usage: matrw --octave-tests DIR [--out report.md] [--filter area] [-j N] [--timeout SEC]
    public static int RunAll(string[] a)
    {
        string dir = Path.GetFullPath(a[0]); string outFile = "octave-compat.md"; string? filter = null;
        int jobs = Environment.ProcessorCount, timeout = 60; string? lib = null;
        for (int i = 1; i < a.Length - 1; i++)
        {
            if (a[i] == "--out") outFile = a[++i];
            else if (a[i] == "--filter") filter = a[++i];
            else if (a[i] == "-j") jobs = int.Parse(a[++i]);
            else if (a[i] == "--lib") lib = Path.GetFullPath(a[++i]);
            else if (a[i] == "--timeout") timeout = int.Parse(a[++i]);
        }
        var files = Directory.GetFiles(dir, "*.m", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("\n%!"))
            .Where(f => filter == null || Area(dir, f).StartsWith(filter))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        Console.WriteLine($"running embedded tests of {files.Count} files ({jobs} parallel)...");
        var results = new ConcurrentBag<(string area, FileTestResult r)>();
        var sw = Stopwatch.StartNew(); int done = 0;
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = jobs }, f =>
        {
            var r = RunChild(f, timeout, lib) ?? new FileTestResult { File = f, Fail = 1, Failures = { new TestFailure(0, "crash", "child process crashed or timed out") } };
            results.Add((Area(dir, f), r));
            var n = Interlocked.Increment(ref done);
            if (n % 50 == 0) Console.WriteLine($"  {n}/{files.Count}");
        });
        var list = results.ToList();
        var areas = OctaveTestRunner.Summarize(list);
        int tp = areas.Sum(x => x.Pass), tf = areas.Sum(x => x.Fail), ts = areas.Sum(x => x.Skip);
        var sb = new StringBuilder();
        sb.AppendLine("# GNU Octave compatibility report").AppendLine();
        sb.AppendLine(lib == null ? "Mode: core builtins only (Octave m-files not on path)" : $"Mode: Octave m-file library on path ({lib})").AppendLine();
        sb.AppendLine($"Files: {files.Count}, blocks passed {tp}, failed {tf}, skipped {ts}, pass rate {(tp + tf == 0 ? 0 : 100.0 * tp / (tp + tf)):F1}% ({sw.Elapsed.TotalSeconds:F0} s)").AppendLine();
        sb.AppendLine("| Area | Files | Pass | Fail | Skip | Rate |").AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var x in areas) sb.AppendLine($"| {x.Area} | {x.Files} | {x.Pass} | {x.Fail} | {x.Skip} | {x.Rate:F1}% |");
        sb.AppendLine().AppendLine("## Failures").AppendLine();
        foreach (var (area, r) in list.OrderBy(x => x.r.File, StringComparer.Ordinal))
            foreach (var f in r.Failures)
                sb.AppendLine($"- {Path.GetRelativePath(dir, r.File)}:{f.Line} [{f.Kind}] {f.Message}");
        File.WriteAllText(outFile, sb.ToString());
        foreach (var x in areas) Console.WriteLine($"{x.Area,-22} {x.Pass,6}/{x.Pass + x.Fail,-6} {x.Rate,6:F1}%");
        Console.WriteLine($"TOTAL {tp}/{tp + tf} = {(tp + tf == 0 ? 0 : 100.0 * tp / (tp + tf)):F1}%  -> {outFile}");
        return 0;
    }

    static string Area(string root, string f)
    {
        var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
        var i = rel.IndexOf('/');
        return i < 0 ? "." : rel[..i];
    }

    static FileTestResult? RunChild(string file, int timeoutSec, string? lib)
    {
        var psi = new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        var host = Environment.ProcessPath!;
        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        { psi.FileName = host; psi.ArgumentList.Add(typeof(TestDriver).Assembly.Location); }
        else psi.FileName = host;
        if (lib != null) psi.Environment["MATRW_TESTLIB"] = lib;
        psi.ArgumentList.Add("--test"); psi.ArgumentList.Add(file);
        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutSec * 1000)) { try { p.Kill(true); } catch { } return null; }
        return OctaveTestRunner.Deserialize(file, outTask.Result);
    }
}
