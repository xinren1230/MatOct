// MatRewrite - GPL-3.0-or-later
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MatRewrite.Core.Runtime;
using MatRewrite.Plotting.Wpf;
using Microsoft.Win32;

namespace MatRewrite.Ide;

public sealed record WorkspaceItem(string Name, string Value, string Size, string Class);
public sealed record StackItem(string Name, int Line, string? File) { public string FileName => File == null ? "" : Path.GetFileName(File); }
public sealed record ProblemItem(int Line, string Text);

/// <summary>One editor tab: the text editor, its breakpoint margin and file state.</summary>
public sealed class EditorDoc
{
    public required TextEditor Editor;
    public required BreakpointMargin Bp;
    public required LintRenderer Lint;
    public required System.Windows.Controls.TabItem Tab;
    public string? File;
    public bool Dirty;
    public string? UntitledPath;
    public string Name => File == null ? "untitled.m" : Path.GetFileName(File);
}

/// <summary>Forwards interpreter output to the command window (batched on the UI thread).</summary>
sealed class UiWriter : TextWriter
{
    readonly MainWindow win;
    readonly StringBuilder pending = new();
    public UiWriter(MainWindow w) { win = w; }
    public override Encoding Encoding => Encoding.UTF8;
    public override void Write(char value) { lock (pending) pending.Append(value); }
    public override void Write(string? value) { lock (pending) pending.Append(value); }
    public override void Flush()
    {
        string s;
        lock (pending) { if (pending.Length == 0) return; s = pending.ToString(); pending.Clear(); }
        win.Dispatcher.BeginInvoke(() => win.AppendOutput(s));
    }
}

public partial class MainWindow : Window
{
    readonly Interpreter it;
    readonly UiWriter writer;
    readonly WpfBackend figures;
    readonly List<string> history = new();
    int historyPos;
    bool busy;
    Thread? worker;
    readonly List<EditorDoc> docs = new();
    EditorDoc doc = null!;
    TextEditor Editor => doc.Editor;
    BreakpointMargin bpMargin => doc.Bp;
    string? currentFile { get => doc.File; set => doc.File = value; }
    bool dirty { get => doc.Dirty; set => doc.Dirty = value; }
    readonly System.Windows.Threading.DispatcherTimer lintTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    ReplaceWindow? replaceWin;
    /// <summary>Lines typed in the command window while the interpreter waits in input()/keyboard/debug mode.</summary>
    readonly BlockingCollection<string?> inputQueue = new();
    volatile bool waitingForInput;
    bool paused;
    string? untitledPath { get => doc.UntitledPath; set => doc.UntitledPath = value; }
    /// <summary>Command-line automation: run the opened script, optionally save a screenshot and exit.</summary>
    internal string? AutoScreenshot;
    internal string? AutoInput;

    public MainWindow()
    {
        InitializeComponent();
        lintTimer.Tick += (_, _) => { lintTimer.Stop(); RunLint(); };
        NewDoc(null, "");
        writer = new UiWriter(this);
        it = new Interpreter(writer);
        InitPaths();
        figures = new WpfBackend(Dispatcher);
        FigureWindow.CallbackSink = it.Graphics.RaiseCallback;
        FigureWindow.Interp = it;
        FigureWindow.EvalSink = c => Dispatcher.Invoke(() => Execute(c, ">> " + c + "\n"));
        it.Graphics.Backend = figures;
        it.InputReader = ReadInputLine;
        it.Paused += info => Dispatcher.Invoke(() => OnPaused(info));
        it.Resumed += () => Dispatcher.BeginInvoke(OnResumed);
        figures.WindowClosed += f => { try { it.Graphics.Close(f); } catch { } };
        AppendOutput($"MatOct {MatRewrite.Core.Builtins.BuiltinLibrary.Version} - a C# reimplementation of the GNU Octave language\n\n");
        Editor.Text = "% New script\nx = linspace(0, 2*pi, 100);\nplot(x, sin(x), 'r-', x, cos(x), 'b--');\ntitle('Demo'); legend('sin', 'cos'); grid on;\n";
        dirty = false; UpdateTab(doc);
        UpdateTitle();
        UpdateCwd();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Properties, (_, _) => OnRun(this, new RoutedEventArgs())));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnRunSelection(this, new RoutedEventArgs())), Key.F9, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnSave(this, new RoutedEventArgs())), Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnOpen(this, new RoutedEventArgs())), Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnNew(this, new RoutedEventArgs())), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnInterrupt(this, new RoutedEventArgs())), Key.Cancel, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnInterrupt(this, new RoutedEventArgs())), Key.C, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnDbStep(this, new RoutedEventArgs())), Key.F10, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnDbStepIn(this, new RoutedEventArgs())), Key.F11, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnDbStepOut(this, new RoutedEventArgs())), Key.F11, ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnDbQuit(this, new RoutedEventArgs())), Key.F5, ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnToggleBreakpoint(this, new RoutedEventArgs())), Key.F12, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnConditionalBreakpoint(this, new RoutedEventArgs())), Key.F12, ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnReplace(this, new RoutedEventArgs())), Key.H, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnGotoLine(this, new RoutedEventArgs())), Key.G, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnCloseTab(this, new RoutedEventArgs())), Key.W, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => SwitchTab(1)), Key.Tab, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => SwitchTab(-1)), Key.Tab, ModifierKeys.Control | ModifierKeys.Shift));
        InitPhase13();
        Loaded += (_, _) => Input.Focus();
    }

    internal void AppendOutput(string s)
    {
        Output.AppendText(s);
        if (Output.Text.Length > 400_000) Output.Text = Output.Text[^300_000..];
        Output.ScrollToEnd();
    }

    void UpdateTitle()
    {
        var name = doc.Name;
        EditorTitle.Text = "Editor - " + (currentFile ?? name) + (dirty ? " *" : "");
        Title = "MatOct - " + name;
        UpdateTab(doc);
    }


    void RefreshWorkspace()
    {
        var items = new List<WorkspaceItem>();
        foreach (var n in it.VariableNames)
        {
            var v = it.GetVar(n);
            if (v == null) continue;
            string val;
            try
            {
                val = v.Rows * v.Cols <= 10 || v.ClassName == "char" ? Display.Disp(v).Trim().Replace("\n", " ") : $"<{v.Rows}x{v.Cols} {v.ClassName}>";
                if (val.Length > 60) val = val[..60] + "…";
            }
            catch { val = "?"; }
            items.Add(new WorkspaceItem(n, val, $"{v.Rows}x{v.Cols}", v.ClassName));
        }
        WorkspaceList.ItemsSource = items;
        WorkspaceTitle.Text = paused ? $"Workspace - {(pauseFrame == "" ? "base" : pauseFrame)} (paused)" : "Workspace";
    }

    /// <summary>Run code on a worker thread so the UI (and figure windows) stay responsive.</summary>
    void Execute(string code, string? echo = null)
    {
        if (busy) { AppendOutput("(busy - wait for the current command to finish)\n"); return; }
        busy = true;
        Status.Text = "Busy";
        if (echo != null) AppendOutput(echo);
        worker = new Thread(() =>
        {
            it.ClearInterrupt();
            try { it.Eval(code); }
            catch (Exception ex) when (Interpreter.IsQuit(ex, out _)) { Dispatcher.BeginInvoke(Close); }
            catch (ThreadInterruptedException) { writer.Write("^C interrupted\n"); }
            catch (InterruptException ix) { writer.Write(ix.Message == "dbquit" ? "" : "^C interrupted\n"); }
            catch (Exception ex) { writer.Write("error: " + Interpreter.ErrorInfo(ex).msg + "\n" + Interpreter.FormatStack(ex)); }
            finally
            {
                writer.Flush();
                Dispatcher.BeginInvoke(() => { busy = false; paused = false; foreach (var d in docs) d.Bp.SetCurrentLine(-1); StackList.ItemsSource = null; Status.Text = "Ready"; RefreshWorkspace(); UpdateCwd(); RunFinished?.Invoke(); });
            }
        }, 64 * 1024 * 1024) { IsBackground = true, Name = "MatRewrite interpreter" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var code = Input.Text;
            Input.Clear();
            if (code.Trim().Length > 0) { history.Add(code); AddHistory(code); }
            historyPos = history.Count;
            if (waitingForInput)
            {
                AppendOutput(code + "\n");
                inputQueue.Add(code);
                e.Handled = true;
                return;
            }
            Execute(code, ">> " + code + "\n");
            e.Handled = true;
        }
        else if (e.Key == Key.Up && history.Count > 0)
        {
            historyPos = Math.Max(0, historyPos - 1);
            Input.Text = history[historyPos]; Input.CaretIndex = Input.Text.Length; e.Handled = true;
        }
        else if (e.Key == Key.Down && history.Count > 0)
        {
            historyPos = Math.Min(history.Count, historyPos + 1);
            Input.Text = historyPos < history.Count ? history[historyPos] : ""; Input.CaretIndex = Input.Text.Length; e.Handled = true;
        }
    }


    bool ConfirmDiscard(EditorDoc d)
    {
        if (!d.Dirty) return true;
        Activate(d);
        var r = MessageBox.Show(this, $"Save changes to {d.Name}?", "MatOct", MessageBoxButton.YesNoCancel);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return SaveTo(d.File);
        return true;
    }

    void OnNew(object sender, RoutedEventArgs e) => NewDoc(null, "");

    void OnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Octave/MATLAB files (*.m)|*.m|All files (*.*)|*.*", InitialDirectory = Environment.CurrentDirectory, Multiselect = true };
        if (dlg.ShowDialog(this) != true) return;
        foreach (var f in dlg.FileNames) LoadFile(f);
    }

    bool SaveTo(string? path)
    {
        if (path == null)
        {
            var dlg = new SaveFileDialog { Filter = "Octave/MATLAB files (*.m)|*.m", DefaultExt = ".m", InitialDirectory = Environment.CurrentDirectory, FileName = "untitled.m" };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
        }
        File.WriteAllText(path, Editor.Text, new UTF8Encoding(false));
        bool renamed = currentFile != null && !string.Equals(currentFile, path, StringComparison.OrdinalIgnoreCase);
        if (renamed) it.ClearBreakpoint(currentFile!);
        currentFile = path; dirty = false; UpdateTitle(); RunLint();
        SyncBreakpoints();
        return true;
    }

    void OnSave(object sender, RoutedEventArgs e) => SaveTo(currentFile);
    void OnSaveAs(object sender, RoutedEventArgs e) => SaveTo(null);
    void OnExit(object sender, RoutedEventArgs e) => Close();

    void OnRun(object sender, RoutedEventArgs e)
    {
        if (paused) { SendDebug("dbcont"); return; }
        if (busy) { AppendOutput("(busy - wait for the current command to finish)\n"); return; }
        if (currentFile == null && bpMargin.Lines.Count > 0)
        {
            // breakpoints need a file: run untitled scripts from a temp file
            untitledPath = BreakFileOf(doc)!;
            Directory.CreateDirectory(Path.GetDirectoryName(untitledPath)!);
            File.WriteAllText(untitledPath, Editor.Text, new UTF8Encoding(false));
            SyncBreakpoints();
            Execute($"run('{untitledPath.Replace("'", "''")}')", ">> % running untitled script (debug)\n");
            return;
        }
        if (currentFile != null)
        {
            if (dirty) SaveTo(currentFile);
            var dir = Path.GetDirectoryName(currentFile)!;
            if (!it.SearchPath.Contains(dir)) it.SearchPath.Add(dir);
            var file = currentFile.Replace("'", "''");
            Execute($"run('{file}')", $">> run('{Path.GetFileName(currentFile)}')\n");
        }
        else Execute(Editor.Text, ">> % running untitled script\n");
    }

    void OnRunSelection(object sender, RoutedEventArgs e)
    {
        var code = Editor.SelectedText;
        if (code.Length == 0)
        {
            var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
            code = Editor.Document.GetText(line.Offset, line.Length);
        }
        if (code.Trim().Length > 0) Execute(code, ">> " + code.TrimEnd() + "\n");
    }

    void OnInterrupt(object sender, RoutedEventArgs e)
    {
        if (!busy) return;
        if (waitingForInput) { inputQueue.Add(null); AppendOutput("^C\n"); }   // abort input()/debug prompt
        else it.RequestInterrupt();
    }
    void OnCloseFigures(object sender, RoutedEventArgs e) { if (!busy) Execute("close all"); }
    void OnClearOutput(object sender, RoutedEventArgs e) => Output.Clear();

    void OnWorkspaceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceList.SelectedItem is not WorkspaceItem w) return;
        if (busy && !paused) { AppendOutput("(busy - variables can be edited when idle or paused)\n"); return; }
        var v = it.GetVar(w.Name);
        if (v == null) return;
        new VariableEditorWindow(w.Name, v, (n, nv) =>
        {
            if (busy && !paused) { MessageBox.Show(this, "The interpreter is running; pause or wait first."); return; }
            it.SetVar(n, nv);
            RefreshWorkspace();
        }) { Owner = this }.Show();
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var d in docs.ToList()) if (!ConfirmDiscard(d)) { e.Cancel = true; return; }
        inputQueue.Add(null);
        try { it.CloseAllFiles(); } catch { }
        Application.Current.Shutdown();
    }
}

public partial class MainWindow
{
    internal event Action? RunFinished;
    string pauseFrame = "";

    internal void LoadFile(string path)
    {
        path = Path.GetFullPath(path);
        var existing = FindDoc(path);
        if (existing != null) { Activate(existing); return; }
        // reuse a pristine untitled tab
        var target = docs.Count == 1 && doc.File == null && !doc.Dirty && doc.Bp.Lines.Count == 0 ? doc : null;
        if (target == null) NewDoc(path, File.ReadAllText(path));
        else { Editor.Text = File.ReadAllText(path); currentFile = path; dirty = false; }
        bpMargin.Lines.Clear(); bpMargin.Conditions.Clear();
        if (it.Breakpoints.TryGetValue(Interpreter.BreakKey(path), out var set))
            foreach (var l in set) { bpMargin.Lines.Add(l); if (it.GetBreakCondition(path, l) is { } c) bpMargin.Conditions[l] = c; }
        bpMargin.SetCurrentLine(-1);
        UpdateTitle();
        RunLint();
    }

    EditorDoc? FindDoc(string path) => docs.FirstOrDefault(d => d.File != null && string.Equals(d.File, path, StringComparison.OrdinalIgnoreCase));

    EditorDoc NewDoc(string? path, string text)
    {
        var (ed, bp, lint) = EditorFactory.Create();
        var tab = new System.Windows.Controls.TabItem { Content = ed };
        var d = new EditorDoc { Editor = ed, Bp = bp, Lint = lint, Tab = tab, File = path };
        ed.Text = text;
        tab.Tag = d;
        ed.TextChanged += (_, _) =>
        {
            if (!d.Dirty) { d.Dirty = true; if (d == doc) UpdateTitle(); else UpdateTab(d); }
            lintTimer.Stop(); lintTimer.Start();
        };
        bp.Changed += () => SyncBreakpoints(d);
        bp.ConditionRequested += line => AskCondition(d, line);
        ed.TextArea.TextView.MouseHover += (s, e) => OnEditorHover(d, e);
        ed.TextArea.TextView.MouseHoverStopped += (_, _) => { hoverTip.IsOpen = false; };
        docs.Add(d);
        Tabs.Items.Add(tab);
        Activate(d);
        UpdateTab(d);
        return d;
    }

    void UpdateTab(EditorDoc d)
    {
        var header = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        header.Children.Add(new System.Windows.Controls.TextBlock { Text = d.Name + (d.Dirty ? " *" : ""), ToolTip = d.File ?? "untitled", VerticalAlignment = VerticalAlignment.Center });
        var close = new System.Windows.Controls.Button { Content = "×", Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(6, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 11 };
        close.Click += (_, _) => CloseDoc(d);
        header.Children.Add(close);
        d.Tab.Header = header;
    }

    void Activate(EditorDoc d)
    {
        doc = d;
        if (Tabs.SelectedItem != d.Tab) Tabs.SelectedItem = d.Tab;
        UpdateTitle();
        ShowProblems();
    }

    void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.Source != Tabs) return;
        if (Tabs.SelectedItem is System.Windows.Controls.TabItem { Tag: EditorDoc d } && d != doc) Activate(d);
    }

    void SwitchTab(int delta)
    {
        if (docs.Count < 2) return;
        int i = (docs.IndexOf(doc) + delta + docs.Count) % docs.Count;
        Activate(docs[i]);
    }

    void CloseDoc(EditorDoc d)
    {
        if (!ConfirmDiscard(d)) return;
        if (d.File != null && (!busy || paused)) it.ClearBreakpoint(d.File);
        docs.Remove(d); Tabs.Items.Remove(d.Tab);
        if (docs.Count == 0) NewDoc(null, "");
        else Activate(docs.Contains(doc) ? doc : docs[^1]);
    }

    void OnCloseTab(object sender, RoutedEventArgs e) => CloseDoc(doc);

    // ---------------------------------------------------------------- find / replace / goto
    void OnFind(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        ApplicationCommands.Find.Execute(null, Editor.TextArea);
    }

    void OnReplace(object sender, RoutedEventArgs e)
    {
        if (replaceWin == null || !replaceWin.IsLoaded)
        {
            replaceWin = new ReplaceWindow(() => Editor, Editor.SelectedText.Contains('\n') ? "" : Editor.SelectedText) { Owner = this };
            replaceWin.Show();
        }
        else replaceWin.Activate();
    }

    void OnGotoLine(object sender, RoutedEventArgs e)
    {
        var p = new PromptWindow("Go to line", $"Line number (1-{Editor.Document.LineCount}):", Editor.TextArea.Caret.Line.ToString()) { Owner = this };
        if (p.ShowDialog() == true && int.TryParse(p.Result, out var ln)) GotoLine(ln);
    }

    void GotoLine(int ln)
    {
        ln = Math.Clamp(ln, 1, Editor.Document.LineCount);
        Editor.ScrollToLine(ln); Editor.TextArea.Caret.Line = ln; Editor.TextArea.Caret.Column = 1; Editor.Focus();
    }

    // ---------------------------------------------------------------- lint
    void OnLintNow(object sender, RoutedEventArgs e) => RunLint();

    void RunLint()
    {
        foreach (var d in docs)
        {
            try { d.Lint.Messages = MatRewrite.Core.Analysis.Linter.Check(d.Editor.Text); }
            catch { d.Lint.Messages = new(); }
            d.Editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }
        ShowProblems();
    }

    void ShowProblems()
    {
        var msgs = doc.Lint.Messages;
        ProblemList.ItemsSource = msgs.Select(m => new ProblemItem(m.Line, (m.Severity == MatRewrite.Core.Analysis.LintSeverity.Error ? "error: " : "") + m.Message)).ToList();
        ProblemsTitle.Text = msgs.Count == 0 ? "Problems" : $"Problems ({msgs.Count})";
        int errs = msgs.Count(m => m.Severity == MatRewrite.Core.Analysis.LintSeverity.Error);
        LintText.Text = msgs.Count == 0 ? "No problems" : $"{errs} error(s), {msgs.Count - errs} warning(s)";
    }

    void OnProblemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProblemList.SelectedItem is ProblemItem p) GotoLine(p.Line);
    }

    // ---------------------------------------------------------------- hover: variable values & lint messages
    readonly System.Windows.Controls.ToolTip hoverTip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, FontFamily = new FontFamily("Consolas"), MaxWidth = 600 };

    void OnEditorHover(EditorDoc d, MouseEventArgs e)
    {
        var tv = d.Editor.TextArea.TextView;
        var pos = tv.GetPositionFloor(e.GetPosition(tv) + tv.ScrollOffset);
        if (pos == null) return;
        var lines = new List<string>();
        int off = d.Editor.Document.GetOffset(pos.Value.Location);
        var word = WordAt(d.Editor.Document.Text, off);
        if (word != null && (!busy || paused))
        {
            Value? v = null;
            try { v = it.GetVar(word); } catch { }
            if (v != null) lines.Add(HoverText(word, v));
        }
        foreach (var m in d.Lint.Messages.Where(m => m.Line == pos.Value.Line)) lines.Add((m.Severity == MatRewrite.Core.Analysis.LintSeverity.Error ? "error: " : "warning: ") + m.Message);
        if (lines.Count == 0) return;
        hoverTip.PlacementTarget = tv;
        hoverTip.Content = string.Join("\n", lines);
        hoverTip.IsOpen = true;
        e.Handled = true;
    }

    internal static string? WordAt(string text, int off)
    {
        if (off < 0 || off > text.Length) return null;
        int a = off, b = off;
        while (a > 0 && (char.IsLetterOrDigit(text[a - 1]) || text[a - 1] == '_')) a--;
        while (b < text.Length && (char.IsLetterOrDigit(text[b]) || text[b] == '_')) b++;
        if (b <= a || char.IsDigit(text[a])) return null;
        return text[a..b];
    }

    static string HoverText(string name, Value v)
    {
        string body;
        try
        {
            body = v.Rows * v.Cols <= 100 || v.ClassName == "char" ? Display.Disp(v).TrimEnd() : $"<{v.Rows}x{v.Cols} {v.ClassName}>";
        }
        catch { body = "?"; }
        var ls = body.Split('\n');
        if (ls.Length > 12) body = string.Join("\n", ls.Take(12)) + "\n…";
        if (body.Length > 1500) body = body[..1500] + "…";
        return $"{name}: {v.Rows}x{v.Cols} {v.ClassName}\n{body}";
    }

    // ---------------------------------------------------------------- call stack
    void ShowStack(int line)
    {
        try { StackList.ItemsSource = it.StackFrames(line).Select(f => new StackItem(f.Name, f.Line, f.File)).ToList(); }
        catch { StackList.ItemsSource = null; }
    }

    void OnStackDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (StackList.SelectedItem is not StackItem s || s.File == null || !File.Exists(s.File)) return;
        LoadFile(s.File);
        GotoLine(s.Line);
    }

    // ---------------------------------------------------------------- conditional breakpoints
    void AskCondition(EditorDoc d, int line)
    {
        d.Bp.Conditions.TryGetValue(line, out var cur);
        var p = new PromptWindow("Conditional breakpoint", $"Stop at line {line} only when this expression is true (empty = unconditional):", cur ?? "") { Owner = this };
        if (p.ShowDialog() != true) return;
        var cond = p.Result?.Trim();
        if (!string.IsNullOrEmpty(cond))
        {
            try { MatRewrite.Core.Parsing.Parser.ParseProgram(cond); }
            catch (Exception ex) { MessageBox.Show(this, "Invalid condition: " + ex.Message, "MatOct"); return; }
        }
        if (string.IsNullOrEmpty(cond)) { if (!d.Bp.Lines.Contains(line)) d.Bp.Toggle(line); else d.Bp.SetCondition(line, null); }
        else d.Bp.SetCondition(line, cond);
    }

    void OnConditionalBreakpoint(object sender, RoutedEventArgs e) => AskCondition(doc, Editor.TextArea.Caret.Line);

    internal void SetConditionalBreakpoint(int line, string cond) => doc.Bp.SetCondition(line, cond);

    internal void ToggleBreakpoint(int line) => bpMargin.Toggle(line);

    static string? BreakFileOf(EditorDoc d) => d.File ?? (d.Bp.Lines.Count > 0 ? (d.UntitledPath ??= Path.Combine(Path.GetTempPath(), "matrewrite_untitled_" + Environment.ProcessId + "_" + d.GetHashCode(), "untitled.m")) : d.UntitledPath);

    void SyncBreakpoints() { foreach (var d in docs) SyncBreakpoints(d); }

    /// <summary>Copy an editor margin's breakpoints into the interpreter (safe when idle or paused).</summary>
    void SyncBreakpoints(EditorDoc d)
    {
        var f = BreakFileOf(d);
        if (f == null) return;
        if (busy && !paused) { pendingSync = true; return; }
        it.ClearBreakpoint(f);
        foreach (var l in d.Bp.Lines)
        {
            d.Bp.Conditions.TryGetValue(l, out var c);
            try { it.SetBreakpoint(f, l, c); } catch { it.SetBreakpoint(f, l); }
        }
        if (d == docs[^1]) pendingSync = false;
    }
    bool pendingSync;

    /// <summary>InputReader for input()/keyboard/debug prompts; runs on the interpreter thread.</summary>
    string? ReadInputLine(string prompt)
    {
        writer.Write(prompt); writer.Flush();
        waitingForInput = true;
        Dispatcher.BeginInvoke(() =>
        {
            PromptText.Text = prompt.Length > 0 ? prompt : "? ";
            if (!paused) Status.Text = "Waiting for input";
            Input.Focus();
            if (AutoInput != null) { var a = AutoInput; AutoInput = null; Input.Text = a; }
        });
        try { return inputQueue.Take(); }
        finally
        {
            waitingForInput = false;
            Dispatcher.BeginInvoke(() => { PromptText.Text = ">> "; if (busy && !paused) Status.Text = "Busy"; });
        }
    }

    void OnPaused(PauseInfo info)
    {
        paused = true; pauseFrame = info.Frame;
        if (pendingSync) SyncBreakpoints();
        if (info.File != null)
        {
            var full = Path.GetFullPath(info.File);
            var target = docs.FirstOrDefault(d => d.File == null && d.UntitledPath != null && string.Equals(full, Path.GetFullPath(d.UntitledPath), StringComparison.OrdinalIgnoreCase)) ?? FindDoc(full);
            if (target == null && File.Exists(full)) { LoadFile(full); target = FindDoc(full); }
            foreach (var d in docs) d.Bp.SetCurrentLine(-1);
            bool shown = target != null;
            if (target != null) { Activate(target); bpMargin.SetCurrentLine(info.Line); }
            if (shown && info.Line >= 1 && info.Line <= Editor.Document.LineCount)
            {
                Editor.ScrollToLine(info.Line);
                Editor.TextArea.Caret.Line = info.Line;
            }
        }
        Status.Text = $"Paused ({info.Reason}) at line {info.Line} - F5 continue, F10 step, Shift+F5 quit";
        RefreshWorkspace();
        ShowStack(info.Line);
        if (AutoScreenshot != null)
        {
            var shot = AutoScreenshot; AutoScreenshot = null;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                try { SaveScreenshot(shot); } catch (Exception ex) { File.WriteAllText(shot + ".err.txt", ex.ToString()); }
                SendDebug("dbcont");
            };
            t.Start();
        }
    }

    void OnResumed()
    {
        paused = false;
        foreach (var d in docs) d.Bp.SetCurrentLine(-1);
        StackList.ItemsSource = null;
        if (busy) Status.Text = "Busy";
        WorkspaceTitle.Text = "Workspace";
    }

    void SendDebug(string cmd)
    {
        if (!paused || !waitingForInput) return;
        AppendOutput(cmd + "\n");
        inputQueue.Add(cmd);
    }

    void OnDbCont(object sender, RoutedEventArgs e) => SendDebug("dbcont");
    void OnDbStep(object sender, RoutedEventArgs e) => SendDebug("dbstep");
    void OnDbStepIn(object sender, RoutedEventArgs e) => SendDebug("dbstep in");
    void OnDbStepOut(object sender, RoutedEventArgs e) => SendDebug("dbstep out");
    void OnDbQuit(object sender, RoutedEventArgs e) => SendDebug("dbquit");
    void OnToggleBreakpoint(object sender, RoutedEventArgs e) => bpMargin.Toggle(Editor.TextArea.Caret.Line);
    void OnClearBreakpoints(object sender, RoutedEventArgs e)
    {
        foreach (var d in docs) { d.Bp.Lines.Clear(); d.Bp.Conditions.Clear(); d.Bp.InvalidateVisual(); }
        if (!busy || paused) { it.Breakpoints.Clear(); it.BreakConditions.Clear(); } else pendingSync = true;
    }

    internal void RunCurrent() => OnRun(this, new RoutedEventArgs());
    /// <summary>Automation: show the hover tooltip for a variable (screenshots).</summary>
    internal void ShowHoverFor(string name)
    {
        var v = it.GetVar(name);
        if (v == null) return;
        hoverTip.PlacementTarget = Editor.TextArea.TextView;
        hoverTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
        hoverTip.Content = HoverText(name, v);
        hoverTip.IsOpen = true;
    }

    /// <summary>Automation: open the variable editor for a workspace variable.</summary>
    internal Window OpenVariableEditor(string name)
    {
        var v = it.GetVar(name) ?? throw new InvalidOperationException("no variable " + name);
        var ve = new VariableEditorWindow(name, v, (n, nv) => { it.SetVar(n, nv); RefreshWorkspace(); }) { Owner = this };
        ve.Show();
        return ve;
    }

    internal static void SaveWindowPng(Window win, string path)
    {
        win.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(win);
        var fe = (FrameworkElement)win.Content;
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(fe.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(fe.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        rtb.Render(fe);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    /// <summary>Save a PNG of this window (used by --screenshot).</summary>
    internal void SaveScreenshot(string path)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX); var h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var rtb = new RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) dc.DrawRectangle(new VisualBrush((Visual)Content), null, new Rect(0, 0, ActualWidth, ActualHeight));
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}

sealed class RelayCommand : ICommand
{
    readonly Action a;
    public RelayCommand(Action a) { this.a = a; }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => a();
}
