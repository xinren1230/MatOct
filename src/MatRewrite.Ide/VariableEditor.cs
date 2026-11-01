// MatRewrite - GPL-3.0-or-later
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MatRewrite.Core.Runtime;

namespace MatRewrite.Ide;

/// <summary>Spreadsheet-like editor for a real numeric/logical matrix; other values are shown read-only.</summary>
public sealed class VariableEditorWindow : Window
{
    readonly DataTable table = new();
    readonly string name;
    readonly Action<string, Value> apply;
    readonly bool editable;

    public VariableEditorWindow(string name, Value v, Action<string, Value> apply)
    {
        this.name = name; this.apply = apply;
        Title = $"Variable Editor - {name}";
        Width = 640; Height = 420;
        var dock = new DockPanel();
        editable = v is MatrixValue m0 && !m0.IsChar && !m0.IsComplex && !m0.IsSparse && m0.ND == null;
        var info = new TextBlock { Margin = new Thickness(6), Text = $"{name}: {v.Rows}x{v.Cols} {v.ClassName}" + (editable ? "   (edit cells, then Apply)" : "   (read-only)") };
        DockPanel.SetDock(info, Dock.Top);
        dock.Children.Add(info);
        if (editable)
        {
            var m = (MatrixValue)v;
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6) };
            var ok = new Button { Content = "Apply", Width = 80, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
            var close = new Button { Content = "Close", Width = 80, Margin = new Thickness(4, 0, 0, 0), IsCancel = true };
            ok.Click += (_, _) => Apply();
            close.Click += (_, _) => Close();
            buttons.Children.Add(ok); buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Bottom);
            dock.Children.Add(buttons);
            for (int j = 0; j < m.C; j++) table.Columns.Add((j + 1).ToString(), typeof(string));
            for (int i = 0; i < m.R; i++)
            {
                var row = table.NewRow();
                for (int j = 0; j < m.C; j++) row[j] = m.Re[i + j * m.R].ToString("G15", CultureInfo.InvariantCulture);
                table.Rows.Add(row);
            }
            var grid = new DataGrid { ItemsSource = table.DefaultView, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.All };
            grid.LoadingRow += (_, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            dock.Children.Add(grid);
        }
        else
        {
            dock.Children.Add(new TextBox
            {
                Text = Display.Disp(v), IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            });
        }
        Content = dock;
    }

    void Apply()
    {
        int R = table.Rows.Count, C = table.Columns.Count;
        var m = new MatrixValue(R, C);
        for (int i = 0; i < R; i++)
            for (int j = 0; j < C; j++)
            {
                var s = table.Rows[i][j]?.ToString() ?? "0";
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    MessageBox.Show(this, $"Cell ({i + 1},{j + 1}) is not a number: '{s}'", "Variable Editor");
                    return;
                }
                m.Re[i + j * R] = d;
            }
        apply(name, m);
    }
}
