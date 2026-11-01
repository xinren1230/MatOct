// MatRewrite - GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using MatRewrite.Core.Runtime;
using Xunit;

namespace MatRewrite.Tests;

/// <summary>Exercises the host protocol used by the IDE: worker thread + blocking input queue + Paused events.</summary>
public class IdeHostTests
{
    sealed class Host
    {
        public readonly StringWriter Out = new();
        public readonly Interpreter It;
        public readonly BlockingCollection<string?> Q = new();
        public readonly List<PauseInfo> Pauses = new();
        public readonly List<string> Prompts = new();
        public Action<PauseInfo>? OnPause;
        public Host()
        {
            It = new Interpreter(Out);
            It.InputReader = p => { lock (Prompts) Prompts.Add(p); return Q.Take(); };
            It.Paused += i => { lock (Pauses) Pauses.Add(i); OnPause?.Invoke(i); };
        }
        public Exception? Run(string code, int timeoutMs = 10000)
        {
            Exception? err = null;
            var t = new Thread(() => { try { It.Eval(code); } catch (Exception e) { err = e; } }, 16 << 20);
            t.Start();
            Assert.True(t.Join(timeoutMs), "interpreter thread hung");
            return err;
        }
    }

    static string Script(string body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "mr_ide_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, "dbgdemo.m");
        File.WriteAllText(f, body);
        return f;
    }

    [Fact]
    public void BreakpointStepAndContinue()
    {
        var f = Script("a = 1;\nb = a + 1;\nc = b * 10;\nfprintf('%d\\n', c);\n");
        var h = new Host();
        h.It.SetBreakpoint(f, 2);
        foreach (var c in new[] { "dbstep", "dbcont" }) h.Q.Add(c);
        Assert.Null(h.Run($"run('{f}')"));
        Assert.Equal(new[] { 2, 3 }, h.Pauses.Select(p => p.Line).ToArray());
        Assert.Equal("breakpoint", h.Pauses[0].Reason);
        Assert.Equal("step", h.Pauses[1].Reason);
        Assert.All(h.Prompts, p => Assert.Equal("debug> ", p));
        Assert.Contains("20", h.Out.ToString());
    }

    [Fact]
    public void WorkspaceShowsPausedFrameAndEditsApply()
    {
        var f = Script("function r = dbgdemo(x)\n  y = x * 2;\n  r = y + 1;\nend\n");
        var h = new Host();
        h.It.SearchPath.Add(Path.GetDirectoryName(f)!);
        h.It.SetBreakpoint("dbgdemo", 3);
        string[]? names = null;
        h.OnPause = _ =>
        {
            names = h.It.VariableNames.ToArray();           // IDE refreshes the workspace on pause
            h.It.SetVar("y", MatrixValue.Scalar(100));      // variable editor "Apply" while paused
            h.Q.Add("dbcont");
        };
        Assert.Null(h.Run("z = dbgdemo(5); disp(z)"));
        Assert.Contains("x", names!);
        Assert.Contains("y", names!);
        Assert.DoesNotContain("z", names!);
        Assert.Equal("101", h.Out.ToString().Trim().Split('\n')[^1].Trim());
    }

    [Fact]
    public void InputAndKeyboardUseHostReader()
    {
        var h = new Host();
        h.Q.Add("6*7");          // input()
        h.Q.Add("q = 9;");       // typed at K>>
        h.Q.Add("dbcont");
        Assert.Null(h.Run("v = input('n? '); keyboard; disp(v + q)"));
        Assert.Equal("n? ", h.Prompts[0]);
        Assert.Equal("K>> ", h.Prompts[1]);
        Assert.Equal("keyboard", h.Pauses.Single().Reason);
        Assert.EndsWith("51", h.Out.ToString().Trim());
    }

    [Fact]
    public void DbquitAbortsRun()
    {
        var f = Script("a = 1;\nb = 2;\nc = 3;\n");
        var h = new Host();
        h.It.SetBreakpoint(f, 2);
        h.Q.Add("dbquit");
        var err = h.Run($"run('{f}')");
        Assert.IsType<InterruptException>(err);
        Assert.Null(h.It.GetVar("c"));
        Assert.False(h.It.IsPaused);
    }

    [Fact]
    public void RequestInterruptFromUiThreadStopsLoop()
    {
        var h = new Host();
        var t = new Thread(() => { Thread.Sleep(300); h.It.RequestInterrupt(); });
        t.Start();
        var err = h.Run("k = 0; while true, k = k + 1; end");
        Assert.IsType<InterruptException>(err);
        Assert.True(((MatrixValue)h.It.GetVar("k")!).Re[0] > 0);
    }

    [Fact]
    public void NullFromReaderEndsDebugSession()
    {
        var h = new Host();
        h.Q.Add(null);   // IDE Ctrl+C / window close while at a prompt
        Assert.IsType<InterruptException>(h.Run("keyboard; x = 1;"));
        Assert.Null(h.It.GetVar("x"));
    }
}

public class ColormapFunctionTests
{
    static string Run(string code) { var o = new StringWriter(); new Interpreter(o).Eval(code); return o.ToString().Trim(); }
    [Fact] public void JetSizeAndEnds() => Assert.Equal("64 3 0 1", Run("m = jet(); c = colormap(jet(8)); printf('%d %d %g %g', size(m,1), size(m,2), m(1,1), max(m(:)))"));
    [Fact] public void ColormapAcceptsGeneratedMatrix() => Assert.Equal("gray", Run("colormap(gray(16)); disp(colormap())"));
    [Fact] public void ColormapRejectsCustom() => Assert.Throws<OctaveException>(() => Run("colormap(rand(4,3))"));
}
