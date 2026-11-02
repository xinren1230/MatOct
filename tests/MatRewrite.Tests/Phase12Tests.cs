// MatRewrite - GPL-3.0-or-later
using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

public class Phase12Tests
{
    static readonly string Root;
    static Phase12Tests()
    {
        Root = Path.Combine(Path.GetTempPath(), "matrw_p12_" + Environment.ProcessId);
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("MATRW_CONFIG_DIR", Path.Combine(Root, "config"));
    }

    static string Fresh(string name)
    {
        var d = Path.Combine(Root, name + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(d); return d;
    }
    static void W(string dir, string rel, string body)
    {
        var p = Path.Combine(dir, rel); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, body);
    }
    static (Interpreter it, StringWriter o) New() { var o = new StringWriter(); return (new Interpreter(o), o); }
    static string Run(Interpreter it, StringWriter o, string code) { o.GetStringBuilder().Clear(); it.Eval(code); return o.ToString().Replace("\r\n", "\n").Trim(); }
    static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    [Fact] public void AddpathBeginDefaultAndEnd()
    {
        var a = Fresh("a"); var b = Fresh("b"); var c = Fresh("c");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)}); addpath({Q(b)}); addpath({Q(c)}, '-end');");
        Assert.Equal(new[] { b, a, c }, it.SearchPath.Take(3));
    }
    [Fact] public void AddpathMultipleAndPathsep()
    {
        var a = Fresh("a"); var b = Fresh("b"); var c = Fresh("c");
        var (it, o) = New();
        Run(it, o, $"addpath([{Q(a)} pathsep {Q(b)}], {Q(c)});");
        Assert.Equal(new[] { a, b, c }, it.SearchPath.Take(3));
    }
    [Fact] public void AddpathMovesExistingToFront()
    {
        var a = Fresh("a"); var b = Fresh("b");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)}); addpath({Q(b)}); addpath({Q(a)});");
        Assert.Equal(new[] { a, b }, it.SearchPath);
    }
    [Fact] public void RmpathAndPathString()
    {
        var a = Fresh("a"); var b = Fresh("b");
        var (it, o) = New();
        Assert.Equal("1", Run(it, o, $"addpath({Q(a)},{Q(b)}); rmpath({Q(a)}); p = path(); printf('%d', strcmp(p, {Q(b)}))"));
    }
    [Fact] public void PathSetReplaces()
    {
        var a = Fresh("a"); var b = Fresh("b");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)}); path({Q(b)});");
        Assert.Equal(new[] { b }, it.SearchPath);
    }
    [Fact] public void GenpathSkipsSpecial()
    {
        var r = Fresh("g");
        foreach (var s in new[] { "x", "x/y", "private", "@cls", "+pkg", ".git", "x/private" }) Directory.CreateDirectory(Path.Combine(r, s));
        var (it, o) = New();
        var res = Run(it, o, $"printf('%s', genpath({Q(r)}))");
        var parts = res.Split(Path.PathSeparator);
        Assert.Equal(new[] { r, Path.Combine(r, "x"), Path.Combine(r, "x", "y") }, parts);
    }
    [Fact] public void PathOrderResolution()
    {
        var a = Fresh("a"); var b = Fresh("b");
        W(a, "p12f.m", "function r = p12f\n r = 1;\nend\n"); W(b, "p12f.m", "function r = p12f\n r = 2;\nend\n");
        var (it, o) = New();
        Assert.Equal("2", Run(it, o, $"addpath({Q(a)}); addpath({Q(b)}); printf('%d', p12f())"));
        Assert.Equal("1", Run(it, o, $"addpath({Q(a)}); printf('%d', p12f())"));
        Assert.Equal("2", Run(it, o, $"rmpath({Q(a)}); printf('%d', p12f())"));
    }
    [Fact] public void CurrentFolderFirst()
    {
        var a = Fresh("a"); var cwd = Fresh("cwd");
        W(a, "p12g.m", "function r = p12g\n r = 1;\nend\n"); W(cwd, "p12g.m", "function r = p12g\n r = 9;\nend\n");
        var (it, o) = New();
        var old = Directory.GetCurrentDirectory();
        lock (typeof(Directory))
        {
            try { Assert.Equal("9", Run(it, o, $"addpath({Q(a)}); cd({Q(cwd)}); printf('%d', p12g())")); }
            finally { Directory.SetCurrentDirectory(old); }
        }
    }
    [Fact] public void MissThenAddFound()
    {
        var a = Fresh("a");
        W(a, "p12h.m", "function r = p12h\n r = 5;\nend\n");
        var (it, o) = New();
        Assert.Equal("0", Run(it, o, "printf('%d', exist('p12h'))"));
        Assert.Equal("2 5", Run(it, o, $"addpath({Q(a)}); printf('%d %d', exist('p12h'), p12h())"));
        Assert.Equal("0", Run(it, o, $"rmpath({Q(a)}); printf('%d', exist('p12h'))"));
    }
    [Fact] public void NewFileFoundAfterRehash()
    {
        var a = Fresh("a");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)});");
        Assert.Equal("0", Run(it, o, "printf('%d', exist('p12k'))"));
        W(a, "p12k.m", "function r = p12k\n r = 7;\nend\n");
        Assert.Equal("7", Run(it, o, "printf('%d', p12k())"));
    }
    [Fact] public void WhichReportsShadowing()
    {
        var a = Fresh("a"); var b = Fresh("b");
        W(a, "p12w.m", "function p12w\nend\n"); W(b, "p12w.m", "function p12w\nend\n");
        var (it, o) = New();
        Assert.Equal(Path.Combine(b, "p12w.m"), Run(it, o, $"addpath({Q(a)},'-end'); addpath({Q(b)}); printf('%s', which('p12w'))"));
        var all = Run(it, o, "c = which('p12w','-all'); printf('%d', numel(c))");
        Assert.Equal("2", all);
        Assert.Contains("built-in", Run(it, o, "which sin"));
        Assert.Contains("is a variable", Run(it, o, "zz = 3; which zz"));
    }
    [Fact] public void PrivateFunctions()
    {
        var a = Fresh("a");
        W(a, "p12pub.m", "function r = p12pub\n r = p12priv() + 1;\nend\n");
        W(a, "private/p12priv.m", "function r = p12priv\n r = 41;\nend\n");
        var (it, o) = New();
        Assert.Equal("42 0", Run(it, o, $"addpath({Q(a)}); printf('%d %d', p12pub(), exist('p12priv'))"));
    }
    [Fact] public void PackageFunction()
    {
        var a = Fresh("a");
        W(a, "+p12pkg/twice.m", "function r = twice(x)\n r = 2*x;\nend\n");
        var (it, o) = New();
        Assert.Equal("6", Run(it, o, $"addpath({Q(a)}); printf('%d', p12pkg.twice(3))"));
    }
    [Fact] public void SavepathRoundTrip()
    {
        var a = Fresh("a"); var b = Fresh("b"); var f = Path.Combine(Fresh("s"), "pathdef.m");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)},{Q(b)}); savepath({Q(f)});");
        Assert.Equal(new[] { a, b }, PathManager.ReadPathDef(f));
    }
    [Fact] public void SavepathDefaultAndStartupLoad()
    {
        var a = Fresh("a");
        var (it, o) = New();
        lock (typeof(PathManager))
        {
            Run(it, o, $"addpath({Q(a)}); savepath;");
            var (it2, _) = New();
            PathManager.LoadStartup(it2);
            Assert.Contains(a, it2.SearchPath);
        }
    }
    [Fact] public void RestoreDefaultDropsUserDirs()
    {
        var a = Fresh("a");
        var (it, o) = New();
        Run(it, o, $"addpath({Q(a)}); restoredefaultpath;");
        Assert.DoesNotContain(a, it.SearchPath);
    }
    [Fact] public void PathsepValue() { var (it, o) = New(); Assert.Equal(Path.PathSeparator.ToString(), Run(it, o, "printf('%s', pathsep)")); }
    [Fact] public void AddpathReturnsOld()
    {
        var a = Fresh("a");
        var (it, o) = New();
        Assert.Equal("1", Run(it, o, $"old = addpath({Q(a)}); printf('%d', isempty(old))"));
    }
    [Fact] public void PathChangedEvent()
    {
        var a = Fresh("a"); int n = 0;
        var (it, o) = New(); it.PathChanged += () => n++;
        Run(it, o, $"addpath({Q(a)}); rmpath({Q(a)});");
        Assert.True(n >= 2);
    }
}
