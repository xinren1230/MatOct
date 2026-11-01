// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MatRewrite.Core.Runtime;
using MatRewrite.Plotting.Wpf;
using Microsoft.Win32;

namespace MatRewrite.Ide;

public sealed record WorkspaceItem(string Name, string Value, string Size, string Class);

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
    string? currentFile;
    bool dirty, busy;
    Thread? worker;

    public MainWindow()
    {
        InitializeComponent();
        writer = new UiWriter(this);
        it = new Interpreter(writer);
        figures = new WpfBackend(Dispatcher);
        it.Graphics.Backend = figures;
        figures.WindowClosed += f => { try { it.Graphics.Close(f); } catch { } };
        AppendOutput($"MatRewrite {MatRewrite.Core.Builtins.BuiltinLibrary.Version} - a C# reimplementation of the GNU Octave language\n\n");
        Editor.Text = "% New script\nx = linspace(0, 2*pi, 100);\nplot(x, sin(x), 'r-', x, cos(x), 'b--');\ntitle('Demo'); legend('sin', 'cos'); grid on;\n";
        dirty = false;
        UpdateTitle();
        UpdateCwd();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Properties, (_, _) => OnRun(this, new RoutedEventArgs())));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnRunSelection(this, new RoutedEventArgs())), Key.F9, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnSave(this, new RoutedEventArgs())), Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnOpen(this, new RoutedEventArgs())), Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnNew(this, new RoutedEventArgs())), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnInterrupt(this, new RoutedEventArgs())), Key.Cancel, ModifierKeys.Control));
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
        var name = currentFile == null ? "untitled.m" : Path.GetFileName(currentFile);
        EditorTitle.Text = "Editor - " + name + (dirty ? " *" : "");
        Title = "MatRewrite IDE - " + name;
    }

    void UpdateCwd() => CwdText.Text = Environment.CurrentDirectory;

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
            try { it.Eval(code); }
            catch (Exception ex) when (Interpreter.IsQuit(ex, out _)) { Dispatcher.BeginInvoke(Close); }
            catch (ThreadInterruptedException) { writer.Write("^C interrupted\n"); }
            catch (Exception ex) { writer.Write("error: " + Interpreter.ErrorInfo(ex).msg + "\n" + Interpreter.FormatStack(ex)); }
            finally
            {
                writer.Flush();
                Dispatcher.BeginInvoke(() => { busy = false; Status.Text = "Ready"; RefreshWorkspace(); UpdateCwd(); });
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
            if (code.Trim().Length > 0) { history.Add(code); }
            historyPos = history.Count;
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

    void OnEditorChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (!dirty) { dirty = true; UpdateTitle(); } }

    bool ConfirmDiscard()
    {
        if (!dirty) return true;
        var r = MessageBox.Show(this, "Save changes to the current script?", "MatRewrite", MessageBoxButton.YesNoCancel);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return SaveTo(currentFile);
        return true;
    }

    void OnNew(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        Editor.Text = ""; currentFile = null; dirty = false; UpdateTitle();
    }

    void OnOpen(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Octave/MATLAB files (*.m)|*.m|All files (*.*)|*.*", InitialDirectory = Environment.CurrentDirectory };
        if (dlg.ShowDialog(this) != true) return;
        Editor.Text = File.ReadAllText(dlg.FileName);
        currentFile = dlg.FileName; dirty = false; UpdateTitle();
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
        currentFile = path; dirty = false; UpdateTitle();
        return true;
    }

    void OnSave(object sender, RoutedEventArgs e) => SaveTo(currentFile);
    void OnSaveAs(object sender, RoutedEventArgs e) => SaveTo(null);
    void OnExit(object sender, RoutedEventArgs e) => Close();

    void OnRun(object sender, RoutedEventArgs e)
    {
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
            int line = Editor.GetLineIndexFromCharacterIndex(Editor.CaretIndex);
            code = line >= 0 ? Editor.GetLineText(line) : "";
        }
        if (code.Trim().Length > 0) Execute(code, ">> " + code.TrimEnd() + "\n");
    }

    void OnInterrupt(object sender, RoutedEventArgs e) { if (busy) worker?.Interrupt(); }
    void OnCloseFigures(object sender, RoutedEventArgs e) { if (!busy) Execute("close all"); }
    void OnClearOutput(object sender, RoutedEventArgs e) => Output.Clear();

    void OnWorkspaceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WorkspaceList.SelectedItem is WorkspaceItem w) Execute(w.Name, ">> " + w.Name + "\n");
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscard()) { e.Cancel = true; return; }
        try { it.CloseAllFiles(); } catch { }
        Application.Current.Shutdown();
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
