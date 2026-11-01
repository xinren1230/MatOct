// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace MatRewrite.Ide;

/// <summary>Octave/MATLAB syntax highlighting definition for AvalonEdit.</summary>
public static class OctaveSyntax
{
    const string Xshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Octave" extensions=".m" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="#228B22" />
  <Color name="String" foreground="#A020F0" />
  <Color name="Keyword" foreground="#0000FF" />
  <Color name="Number" foreground="#B8860B" />
  <Color name="Builtin" foreground="#008B8B" />
  <RuleSet ignoreCase="false">
    <Span color="Comment" multiline="true">
      <Begin>^\s*%\{\s*$</Begin>
      <End>^\s*%\}\s*$</End>
    </Span>
    <Span color="Comment"><Begin>[%#]</Begin></Span>
    <Span color="String"><Begin>"</Begin><End>"</End><RuleSet><Span begin="\\" end="." /></RuleSet></Span>
    <!-- a quote is a string start only when it does not follow an identifier, number, ')' ']' '}' or '.' (transpose) -->
    <Span color="String"><Begin>(?&lt;![\w\)\]\}\.'])'</Begin><End>'(?!')</End></Span>
    <Keywords color="Keyword">
      <Word>if</Word><Word>elseif</Word><Word>else</Word><Word>end</Word><Word>endif</Word>
      <Word>for</Word><Word>endfor</Word><Word>parfor</Word><Word>while</Word><Word>endwhile</Word>
      <Word>do</Word><Word>until</Word><Word>switch</Word><Word>case</Word><Word>otherwise</Word><Word>endswitch</Word>
      <Word>function</Word><Word>endfunction</Word><Word>return</Word><Word>break</Word><Word>continue</Word>
      <Word>try</Word><Word>catch</Word><Word>end_try_catch</Word><Word>unwind_protect</Word><Word>unwind_protect_cleanup</Word><Word>end_unwind_protect</Word>
      <Word>global</Word><Word>persistent</Word><Word>classdef</Word><Word>properties</Word><Word>methods</Word><Word>events</Word><Word>enumeration</Word>
      <Word>endclassdef</Word><Word>endproperties</Word><Word>endmethods</Word>
    </Keywords>
    <Keywords color="Builtin">
      <Word>disp</Word><Word>printf</Word><Word>fprintf</Word><Word>sprintf</Word><Word>plot</Word><Word>figure</Word><Word>zeros</Word><Word>ones</Word>
      <Word>size</Word><Word>numel</Word><Word>length</Word><Word>error</Word><Word>input</Word><Word>keyboard</Word><Word>pi</Word><Word>true</Word><Word>false</Word>
      <Word>surf</Word><Word>mesh</Word><Word>plot3</Word><Word>contour</Word><Word>hold</Word><Word>title</Word><Word>xlabel</Word><Word>ylabel</Word><Word>zlabel</Word>
    </Keywords>
    <Rule color="Number">\b0[xX][0-9a-fA-F]+|(\b\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?[ij]?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    static IHighlightingDefinition? def;
    public static IHighlightingDefinition Definition
    {
        get
        {
            if (def != null) return def;
            using var r = XmlReader.Create(new StringReader(Xshd));
            def = HighlightingLoader.Load(r, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Octave", new[] { ".m" }, def);
            return def;
        }
    }
}

/// <summary>Left margin showing breakpoints (red dots, click to toggle) and the current execution line (yellow arrow).</summary>
public sealed class BreakpointMargin : AbstractMargin
{
    public readonly SortedSet<int> Lines = new();
    /// <summary>Optional conditions of conditional breakpoints (line -> expression).</summary>
    public readonly Dictionary<int, string> Conditions = new();
    public int CurrentLine { get; private set; } = -1;
    public event Action? Changed;
    /// <summary>Raised on right-click with the clicked line (the host asks for a condition).</summary>
    public event Action<int>? ConditionRequested;

    public void SetCondition(int line, string? cond)
    {
        if (string.IsNullOrWhiteSpace(cond)) Conditions.Remove(line);
        else { Conditions[line] = cond.Trim(); Lines.Add(line); }
        InvalidateVisual();
        Changed?.Invoke();
    }

    public void ClearAll() { Lines.Clear(); Conditions.Clear(); InvalidateVisual(); Changed?.Invoke(); }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (TextView == null) return;
        var vl = TextView.GetVisualLineFromVisualTop(e.GetPosition(TextView).Y + TextView.VerticalOffset);
        if (vl == null) return;
        ConditionRequested?.Invoke(vl.FirstDocumentLine.LineNumber);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 0);

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView != null) oldTextView.VisualLinesChanged -= Redraw;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null) newTextView.VisualLinesChanged += Redraw;
        InvalidateVisual();
    }
    void Redraw(object? s, EventArgs e) => InvalidateVisual();

    public void SetCurrentLine(int line) { CurrentLine = line; InvalidateVisual(); TextView?.InvalidateLayer(KnownLayer.Background); }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (TextView == null) return;
        var pos = e.GetPosition(TextView);
        var vl = TextView.GetVisualLineFromVisualTop(pos.Y + TextView.VerticalOffset);
        if (vl == null) return;
        Toggle(vl.FirstDocumentLine.LineNumber);
        e.Handled = true;
    }

    public void Toggle(int line)
    {
        if (!Lines.Remove(line)) Lines.Add(line); else Conditions.Remove(line);
        InvalidateVisual();
        Changed?.Invoke();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240, 240, 240)), null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
        if (TextView == null || !TextView.VisualLinesValid) return;
        foreach (var vl in TextView.VisualLines)
        {
            int ln = vl.FirstDocumentLine.LineNumber;
            double y = vl.VisualTop - TextView.VerticalOffset, h = vl.Height;
            if (Lines.Contains(ln))
            {
                bool cond = Conditions.ContainsKey(ln);
                dc.DrawEllipse(cond ? Brushes.Orange : Brushes.Red, new Pen(cond ? Brushes.DarkOrange : Brushes.DarkRed, 1), new Point(8, y + h / 2), 5.5, 5.5);
                if (cond) dc.DrawRectangle(Brushes.White, null, new Rect(5, y + h / 2 - 1, 6, 2));
            }
            if (ln == CurrentLine)
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(3, y + h / 2 - 4), true, true);
                    c.LineTo(new Point(13, y + h / 2), true, false);
                    c.LineTo(new Point(3, y + h / 2 + 4), true, false);
                }
                dc.DrawGeometry(Brushes.Gold, new Pen(Brushes.DarkGoldenrod, 1), g);
            }
        }
    }
}

/// <summary>Paints the current debug line background.</summary>
public sealed class CurrentLineRenderer : IBackgroundRenderer
{
    readonly BreakpointMargin margin;
    public CurrentLineRenderer(BreakpointMargin m) { margin = m; }
    public KnownLayer Layer => KnownLayer.Background;
    public void Draw(TextView textView, DrawingContext dc)
    {
        if (margin.CurrentLine < 1 || !textView.VisualLinesValid) return;
        foreach (var vl in textView.VisualLines)
        {
            if (vl.FirstDocumentLine.LineNumber != margin.CurrentLine) continue;
            double y = vl.VisualTop - textView.VerticalOffset;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 255, 230, 80)), null, new Rect(0, y, textView.ActualWidth + textView.HorizontalOffset, vl.Height));
        }
    }
}

/// <summary>Wavy underlines for lint messages (red = error, orange = warning).</summary>
public sealed class LintRenderer : IBackgroundRenderer
{
    public List<MatRewrite.Core.Analysis.LintMessage> Messages = new();
    public KnownLayer Layer => KnownLayer.Selection;
    public void Draw(TextView textView, DrawingContext dc)
    {
        if (Messages.Count == 0 || !textView.VisualLinesValid) return;
        var byLine = Messages.GroupBy(m => m.Line).ToDictionary(g => g.Key, g => g.Any(m => m.Severity == MatRewrite.Core.Analysis.LintSeverity.Error));
        foreach (var vl in textView.VisualLines)
        {
            var dl = vl.FirstDocumentLine;
            if (!byLine.TryGetValue(dl.LineNumber, out var isErr)) continue;
            var text = textView.Document.GetText(dl);
            int lead = text.Length - text.TrimStart().Length;
            int len = text.TrimEnd().Length;
            if (len <= lead) { lead = 0; len = Math.Max(1, text.Length); }
            var p0 = vl.GetVisualPosition(vl.GetVisualColumn(lead), VisualYPosition.LineBottom) - textView.ScrollOffset;
            var p1 = vl.GetVisualPosition(vl.GetVisualColumn(Math.Min(len, dl.Length)), VisualYPosition.LineBottom) - textView.ScrollOffset;
            if (p1.X <= p0.X) p1.X = p0.X + 8;
            var pen = new Pen(isErr ? Brushes.Red : Brushes.DarkOrange, 1);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(p0.X, p0.Y - 1), false, false);
                bool up = true;
                for (double x = p0.X + 2; x <= p1.X; x += 2, up = !up) c.LineTo(new Point(x, p0.Y - (up ? 3 : 1)), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(null, pen, g);
        }
    }
}

/// <summary>Find / replace dialog for the active editor.</summary>
public sealed class ReplaceWindow : Window
{
    readonly Func<TextEditor> editor;
    readonly System.Windows.Controls.TextBox find = new() { Width = 260, Margin = new Thickness(4) };
    readonly System.Windows.Controls.TextBox repl = new() { Width = 260, Margin = new Thickness(4) };
    readonly System.Windows.Controls.CheckBox matchCase = new() { Content = "Match case", Margin = new Thickness(4) };
    readonly System.Windows.Controls.CheckBox whole = new() { Content = "Whole word", Margin = new Thickness(4) };
    readonly System.Windows.Controls.TextBlock info = new() { Margin = new Thickness(4), Foreground = Brushes.Gray };

    public ReplaceWindow(Func<TextEditor> ed, string initial)
    {
        editor = ed;
        Title = "Find and Replace"; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        find.Text = initial;
        var grid = new System.Windows.Controls.StackPanel { Margin = new Thickness(6) };
        System.Windows.Controls.StackPanel Row(string label, UIElement el)
        {
            var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Width = 70, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(el); return sp;
        }
        grid.Children.Add(Row("Find:", find));
        grid.Children.Add(Row("Replace:", repl));
        var opts = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        opts.Children.Add(matchCase); opts.Children.Add(whole); grid.Children.Add(opts);
        var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        System.Windows.Controls.Button B(string t, Action a) { var b = new System.Windows.Controls.Button { Content = t, Margin = new Thickness(4), Padding = new Thickness(8, 2, 8, 2) }; b.Click += (_, _) => a(); buttons.Children.Add(b); return b; }
        B("Find next", () => FindNext()).IsDefault = true;
        B("Replace", Replace);
        B("Replace all", ReplaceAll);
        B("Close", Close);
        grid.Children.Add(buttons); grid.Children.Add(info);
        Content = grid;
        Loaded += (_, _) => { find.Focus(); find.SelectAll(); };
    }

    System.Text.RegularExpressions.Regex? Pattern()
    {
        if (find.Text.Length == 0) return null;
        var p = System.Text.RegularExpressions.Regex.Escape(find.Text);
        if (whole.IsChecked == true) p = @"\b" + p + @"\b";
        return new System.Text.RegularExpressions.Regex(p, matchCase.IsChecked == true ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public bool FindNext()
    {
        var ed = editor(); var re = Pattern(); if (re == null) return false;
        int start = ed.SelectionStart + ed.SelectionLength;
        var m = re.Match(ed.Text, Math.Min(start, ed.Text.Length));
        if (!m.Success) m = re.Match(ed.Text);
        if (!m.Success) { info.Text = "Not found"; return false; }
        ed.Select(m.Index, m.Length); ed.ScrollTo(ed.Document.GetLineByOffset(m.Index).LineNumber, 0);
        info.Text = "";
        return true;
    }

    void Replace()
    {
        var ed = editor(); var re = Pattern(); if (re == null) return;
        if (ed.SelectionLength > 0 && re.Match(ed.SelectedText) is { Success: true } m && m.Length == ed.SelectionLength)
            ed.Document.Replace(ed.SelectionStart, ed.SelectionLength, repl.Text);
        FindNext();
    }

    void ReplaceAll()
    {
        var ed = editor(); var re = Pattern(); if (re == null) return;
        int n = re.Matches(ed.Text).Count;
        if (n == 0) { info.Text = "Not found"; return; }
        var replacement = repl.Text.Replace("$", "$$");
        ed.Document.Text = re.Replace(ed.Text, replacement);
        info.Text = $"Replaced {n} occurrence(s)";
    }
}

/// <summary>One-line text prompt.</summary>
public sealed class PromptWindow : Window
{
    readonly System.Windows.Controls.TextBox box = new() { Width = 320, Margin = new Thickness(4) };
    public string? Result;
    public PromptWindow(string title, string label, string initial)
    {
        Title = title; SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(8) };
        sp.Children.Add(new System.Windows.Controls.TextBlock { Text = label, Margin = new Thickness(4) });
        box.Text = initial; sp.Children.Add(box);
        var ok = new System.Windows.Controls.Button { Content = "OK", IsDefault = true, Width = 70, Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => { Result = box.Text; DialogResult = true; };
        sp.Children.Add(ok);
        Content = sp;
        Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
    }
}

public static class EditorFactory
{
    public static (TextEditor ed, BreakpointMargin bp, LintRenderer lint) Create()
    {
        var ed = new TextEditor
        {
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            FontSize = 13,
            ShowLineNumbers = true,
            SyntaxHighlighting = OctaveSyntax.Definition,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };
        ed.Options.ConvertTabsToSpaces = true;
        ed.Options.IndentationSize = 2;
        var bp = new BreakpointMargin();
        ed.TextArea.LeftMargins.Insert(0, bp);
        ed.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineRenderer(bp));
        var lint = new LintRenderer();
        ed.TextArea.TextView.BackgroundRenderers.Add(lint);
        ICSharpCode.AvalonEdit.Search.SearchPanel.Install(ed);   // Ctrl+F incremental find
        return (ed, bp, lint);
    }
}
