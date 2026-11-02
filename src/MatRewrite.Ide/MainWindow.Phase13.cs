// MatOct - GPL-3.0-or-later
// Phase 13 IDE additions: persistent command history panel, tab completion in the
// command window, %% code sections (Ctrl+Enter), and a help browser (F1).
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Ide;

public partial class MainWindow
{
    static string HistoryFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MatOct", "history.m");
    string? completionPrefix; List<string>? completionList; int completionIdx;

    void InitPhase13()
    {
        try
        {
            if (File.Exists(HistoryFile))
                foreach (var l in File.ReadAllLines(HistoryFile).TakeLast(2000)) if (l.Trim().Length > 0) history.Add(l);
        }
        catch { }
        historyPos = history.Count;
        RefreshHistory();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Input.IsKeyboardFocusWithin)
            { RunSection(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; }
        };
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnHelpBrowser(this, new RoutedEventArgs())), Key.F1, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => OnHelpWord(this, new RoutedEventArgs())), Key.F1, ModifierKeys.Control));
        Input.PreviewKeyDown += OnInputTab;
        Closed += (_, _) => SaveHistory();
    }

    internal void AddHistory(string code)
    {
        RefreshHistory();
        try { Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!); File.AppendAllText(HistoryFile, code.Replace("\r", "").Replace("\n", " ") + "\n"); } catch { }
    }

    void SaveHistory()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!); File.WriteAllLines(HistoryFile, history.TakeLast(2000)); } catch { }
    }

    void RefreshHistory()
    {
        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = history.ToList();
        if (history.Count > 0) HistoryList.ScrollIntoView(history[^1]);
    }

    void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is not string code) return;
        history.Add(code); historyPos = history.Count; AddHistory(code);
        Execute(code, ">> " + code + "\n");
    }

    // ---- tab completion -------------------------------------------------------------
    void OnInputTab(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab) { if (e.Key != Key.LeftShift && e.Key != Key.RightShift) completionList = null; return; }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        e.Handled = true;
        var text = Input.Text; int caret = Input.CaretIndex;
        int start = caret;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
        if (completionList == null)
        {
            completionPrefix = text[start..caret];
            if (completionPrefix.Length == 0) return;
            completionList = Candidates(completionPrefix).ToList();
            completionIdx = -1;
            if (completionList.Count == 0) { completionList = null; return; }
            if (completionList.Count > 1) AppendOutput(string.Join("  ", completionList.Take(60)) + (completionList.Count > 60 ? "  …" : "") + "\n");
        }
        completionIdx = (completionIdx + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) + completionList.Count) % completionList.Count;
        var word = completionList[completionIdx];
        Input.Text = text[..start] + word + text[caret..];
        Input.CaretIndex = start + word.Length;
    }

    internal IEnumerable<string> Candidates(string prefix)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in it.VariableNames) if (n.StartsWith(prefix, StringComparison.Ordinal)) set.Add(n);
        foreach (var n in it.Builtins.Keys) if (n.StartsWith(prefix, StringComparison.Ordinal) && !n.StartsWith("__")) set.Add(n);
        foreach (var n in MatRewrite.Core.Runtime.CoreMLib.Names) if (n.StartsWith(prefix, StringComparison.Ordinal)) set.Add(n);
        foreach (var kw in new[] { "function", "end", "endfunction", "if", "else", "elseif", "for", "while", "switch", "case", "otherwise", "return", "break", "continue", "try", "catch", "global", "persistent" })
            if (kw.StartsWith(prefix, StringComparison.Ordinal)) set.Add(kw);
        try
        {
            foreach (var d in it.SearchPath.Prepend(Directory.GetCurrentDirectory()).Distinct().Take(400))
                if (Directory.Exists(d))
                    foreach (var f in Directory.EnumerateFiles(d, prefix + "*.m")) set.Add(Path.GetFileNameWithoutExtension(f));
        }
        catch { }
        return set;
    }

    // ---- %% sections -----------------------------------------------------------------
    static bool IsSectionLine(string l) { var t = l.TrimStart(); return t.StartsWith("%%") || t.StartsWith("##"); }

    /// <summary>Line range (1-based, inclusive) of the %% section containing the given line.</summary>
    internal static (int first, int last) SectionAt(string[] lines, int line)
    {
        int a = Math.Clamp(line, 1, Math.Max(1, lines.Length));
        while (a > 1 && !IsSectionLine(lines[a - 1])) a--;
        int b = a;
        while (b < lines.Length && !IsSectionLine(lines[b])) b++;
        return (a, b);
    }

    void OnRunSection(object sender, RoutedEventArgs e) => RunSection(false);
    void OnRunSectionAdvance(object sender, RoutedEventArgs e) => RunSection(true);

    void RunSection(bool advance)
    {
        if (Input.IsKeyboardFocusWithin) return;
        var lines = Editor.Text.Replace("\r", "").Split('\n');
        var (a, b) = SectionAt(lines, Editor.TextArea.Caret.Line);
        var code = string.Join("\n", lines[(a - 1)..b]);
        var title = IsSectionLine(lines[a - 1]) ? lines[a - 1].Trim().TrimStart('%', '#').Trim() : "";
        if (code.Trim().Length > 0) Execute(code, $">> % run section {(title == "" ? "" : "'" + title + "' ")}(lines {a}-{b})\n");
        if (advance && b < lines.Length) { Editor.TextArea.Caret.Line = b + 1; Editor.TextArea.Caret.BringCaretToView(); }
    }

    // ---- help browser ----------------------------------------------------------------
    void OnHelpBrowser(object sender, RoutedEventArgs e) => OpenHelp(null);
    void OnHelpWord(object sender, RoutedEventArgs e)
    {
        var off = Editor.CaretOffset; var t = Editor.Text;
        int s = off, en = off;
        while (s > 0 && (char.IsLetterOrDigit(t[s - 1]) || t[s - 1] == '_')) s--;
        while (en < t.Length && (char.IsLetterOrDigit(t[en]) || t[en] == '_')) en++;
        OpenHelp(en > s ? t[s..en] : null);
    }

    internal HelpWindow OpenHelp(string? topic)
    {
        var w = new HelpWindow(this, topic) { Owner = this };
        w.Show();
        return w;
    }

    internal string HelpFor(string name)
    {
        try { return MatRewrite.Core.Builtins.BuiltinLibrary.HelpText(it, name); }
        catch (Exception ex) { return ex.Message; }
    }
}

/// <summary>Simple help browser: searchable function list on the left, help text on the right.</summary>
public sealed class HelpWindow : Window
{
    readonly MainWindow main;
    readonly TextBox search = new() { Margin = new Thickness(4) };
    readonly ListBox list = new() { FontFamily = new FontFamily("Consolas") };
    readonly TextBox text = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 13, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, Background = Brushes.White };
    readonly List<string> all;

    public HelpWindow(MainWindow m, string? topic)
    {
        main = m;
        Title = "MatOct Help"; Width = 900; Height = 600;
        all = m.Candidates("").Where(n => n.Length > 0 && char.IsLetter(n[0])).ToList();
        var left = new DockPanel { Width = 230 };
        DockPanel.SetDock(search, Dock.Top);
        left.Children.Add(search); left.Children.Add(list);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(left, 0);
        var sp = new GridSplitter { HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(sp, 1);
        Grid.SetColumn(text, 2);
        grid.Children.Add(left); grid.Children.Add(sp); grid.Children.Add(text);
        Content = grid;
        search.TextChanged += (_, _) => Filter();
        search.KeyDown += (_, e) => { if (e.Key == Key.Enter && search.Text.Trim().Length > 0) Show(search.Text.Trim()); };
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is string s) Show(s); };
        Filter();
        if (topic != null) { search.Text = topic; Show(topic); }
        else text.Text = "MatOct help browser\n\nType a function name on the left (Enter to look it up), or pick one from the list.\nIn the command window: help NAME, doc NAME, lookfor WORD.\n\n" + all.Count + " functions available.";
        Loaded += (_, _) => search.Focus();
    }

    void Filter()
    {
        var q = search.Text.Trim();
        list.ItemsSource = (q.Length == 0 ? (IEnumerable<string>)all : all.Where(n => n.Contains(q, StringComparison.OrdinalIgnoreCase)).OrderBy(n => !n.StartsWith(q, StringComparison.OrdinalIgnoreCase)).ThenBy(n => n)).Take(3000).ToList();
    }

    void Show(string name) { text.Text = main.HelpFor(name); Title = "MatOct Help - " + name; }

    public void SaveScreenshot(string path)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(ActualWidth * dpi.DpiScaleX), (int)(ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        rtb.Render(this);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
        using var fs = File.Create(path); enc.Save(fs);
    }
}
