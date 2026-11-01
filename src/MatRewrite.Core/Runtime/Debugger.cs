// MatRewrite - GPL-3.0-or-later
using MatRewrite.Core.Parsing;

namespace MatRewrite.Core.Runtime;

/// <summary>Thrown to abort execution after dbquit or a user interrupt (Ctrl+C).</summary>
public sealed class InterruptException : OctaveException
{
    public InterruptException(string msg) : base(msg, "Octave:interrupt") { }
}

public enum StepMode { None, Over, In, Out }

/// <summary>Information passed to the host when execution pauses.</summary>
public sealed record PauseInfo(string? File, string Frame, int Line, string Reason);

/// <summary>Breakpoints, stepping, keyboard mode and cooperative interrupts.</summary>
public sealed partial class Interpreter
{
    // ---- interrupt
    volatile bool interruptRequested;
    /// <summary>Ask the running code to stop at the next statement or loop iteration (thread-safe).</summary>
    public void RequestInterrupt() => interruptRequested = true;
    public void ClearInterrupt() => interruptRequested = false;

    public void CheckInterrupt()
    {
        if (interruptRequested) { interruptRequested = false; throw new InterruptException("interrupted"); }
    }

    // ---- input
    /// <summary>Reads one line for input()/keyboard/debug prompts. Hosts (IDE) replace this.</summary>
    public Func<string, string?> InputReader;

    string? DefaultReader(string prompt)
    {
        Out.Write(prompt); Out.Flush();
        return Console.ReadLine();
    }

    // ---- breakpoints: key = file stem (function/script name), lower-case
    public readonly Dictionary<string, SortedSet<int>> Breakpoints = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string?> fileStack = new();
    public string? CurrentFile => fileStack.Count > 0 ? fileStack[^1] : null;
    public int FrameDepth => fileStack.Count;

    StepMode step = StepMode.None;
    int stepDepth;
    int lastPauseLine = -1; string? lastPauseFile;
    bool resumeRequested, quitRequested;
    public bool IsPaused { get; private set; }
    /// <summary>Raised on the interpreter thread when execution pauses / resumes.</summary>
    public event Action<PauseInfo>? Paused;
    public event Action? Resumed;

    public static string BreakKey(string nameOrFile) => Path.GetFileNameWithoutExtension(nameOrFile);

    public void SetBreakpoint(string nameOrFile, int line)
    {
        var k = BreakKey(nameOrFile);
        if (!Breakpoints.TryGetValue(k, out var s)) Breakpoints[k] = s = new SortedSet<int>();
        s.Add(line);
    }

    public void ClearBreakpoint(string nameOrFile, int? line = null)
    {
        var k = BreakKey(nameOrFile);
        if (line == null) Breakpoints.Remove(k);
        else if (Breakpoints.TryGetValue(k, out var s)) { s.Remove(line.Value); if (s.Count == 0) Breakpoints.Remove(k); }
    }

    void PushFile(string? f) => fileStack.Add(f);
    void PopFile() { if (fileStack.Count > 0) fileStack.RemoveAt(fileStack.Count - 1); }

    /// <summary>Called before each statement.</summary>
    void BeforeStatement(Stmt s)
    {
        CheckInterrupt();
        if (s.Line <= 0 || IsPaused) return;
        var file = CurrentFile;
        bool stop = false; string reason = "breakpoint";
        if (step != StepMode.None)
        {
            int d = FrameDepth;
            stop = step switch
            {
                StepMode.In => true,
                StepMode.Over => d <= stepDepth,
                StepMode.Out => d < stepDepth,
                _ => false
            };
            reason = "step";
        }
        if (!stop && file != null && Breakpoints.Count > 0 && Breakpoints.TryGetValue(BreakKey(file), out var set) && set.Contains(s.Line))
            stop = true;
        if (stop) Pause(new PauseInfo(file, Current.FrameName, s.Line, reason));
    }

    /// <summary>Enter debug mode: read and evaluate commands in the current frame until dbcont/dbstep/dbquit.</summary>
    public void Pause(PauseInfo info)
    {
        step = StepMode.None;
        IsPaused = true; resumeRequested = false; quitRequested = false;
        lastPauseLine = info.Line; lastPauseFile = info.File;
        Out.Flush();
        if (info.Reason != "keyboard")
            Out.WriteLine($"stopped in {(info.Frame == "" ? "base" : info.Frame)} at line {info.Line}{(info.File != null ? " [" + info.File + "]" : "")}");
        Out.Flush();
        Paused?.Invoke(info);
        try
        {
            while (!resumeRequested)
            {
                var line = (InputReader ?? DefaultReader)(info.Reason == "keyboard" ? "K>> " : "debug> ");
                if (line == null) { quitRequested = true; break; }
                if (line.Trim().Length == 0) continue;
                try { EvalInCurrentScope(line); }
                catch (Exception ex) when (ex is not ControlException && ex is not InterruptException)
                {
                    Out.Flush(); Out.WriteLine("error: " + ErrorInfo(ex).msg);
                }
                Out.Flush();
            }
        }
        finally
        {
            IsPaused = false;
            Resumed?.Invoke();
        }
        stepDepth = FrameDepth;
        ApplyPendingStep();
        if (quitRequested) { quitRequested = false; step = StepMode.None; throw new InterruptException("dbquit"); }
    }

    internal void DbContinue() { RequireDebug("dbcont"); step = StepMode.None; resumeRequested = true; }
    internal void DbStep(StepMode m) { RequireDebug("dbstep"); resumeRequested = true; pendingStep = m; }
    internal void DbQuit() { RequireDebug("dbquit"); quitRequested = true; resumeRequested = true; }
    StepMode pendingStep = StepMode.None;
    void RequireDebug(string fn) { if (!IsPaused) throw new OctaveException($"{fn}: can only be called in debug mode"); }

    /// <summary>Called by Pause after the loop to apply a pending dbstep.</summary>
    void ApplyPendingStep() { step = pendingStep; pendingStep = StepMode.None; }
}
