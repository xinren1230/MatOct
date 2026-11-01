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
    public int CurrentLine { get; private set; } = -1;
    public event Action? Changed;

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
        if (!Lines.Remove(line)) Lines.Add(line);
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
            if (Lines.Contains(ln)) dc.DrawEllipse(Brushes.Red, new Pen(Brushes.DarkRed, 1), new Point(8, y + h / 2), 5.5, 5.5);
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

public static class EditorFactory
{
    public static (TextEditor ed, BreakpointMargin bp) Create()
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
        return (ed, bp);
    }
}
