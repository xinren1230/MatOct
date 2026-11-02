// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MatRewrite.Core.Runtime;
using Microsoft.Win32;

namespace MatRewrite.Ide;

/// <summary>MATLAB-style "Set Path" dialog. Changes apply to the session immediately; Save writes pathdef.m.</summary>
public sealed class SetPathWindow : Window
{
    readonly Interpreter it;
    readonly Func<bool> canEdit;
    readonly ListBox list = new() { SelectionMode = SelectionMode.Extended, FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 12 };
    readonly TextBlock status = new() { Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    readonly List<string> atOpen;
    List<string> saved;

    public SetPathWindow(Interpreter it, Func<bool> canEdit)
    {
        this.it = it; this.canEdit = canEdit;
        Title = "Set Path"; Width = 760; Height = 480; MinWidth = 500; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        atOpen = it.SearchPath.ToList();
        saved = PathManager.ReadPathDef(PathManager.PathDefFile) ?? new List<string>();

        var root = new DockPanel { Margin = new Thickness(10) };
        var head = new TextBlock { Text = "All changes take effect immediately.", Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        bottom.Children.Add(Btn("Save", OnSave)); bottom.Children.Add(Btn("Close", (_, _) => Close()));
        bottom.Children.Add(Btn("Revert", OnRevert)); bottom.Children.Add(Btn("Default", OnDefault));
        var bottomDock = new DockPanel(); bottomDock.Children.Add(bottom);
        DockPanel.SetDock(bottomDock, Dock.Bottom);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(bottomDock); root.Children.Add(status);

        var left = new StackPanel { Width = 190, Margin = new Thickness(0, 0, 10, 0) };
        left.Children.Add(Btn("Add Folder...", OnAddFolder, true));
        left.Children.Add(Btn("Add with Subfolders...", OnAddSub, true));
        left.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        left.Children.Add(Btn("Move to Top", (_, _) => Move(int.MinValue), true));
        left.Children.Add(Btn("Move Up", (_, _) => Move(-1), true));
        left.Children.Add(Btn("Move Down", (_, _) => Move(1), true));
        left.Children.Add(Btn("Move to Bottom", (_, _) => Move(int.MaxValue), true));
        left.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        left.Children.Add(Btn("Remove", OnRemove, true));
        DockPanel.SetDock(left, Dock.Left); root.Children.Add(left);

        var right = new DockPanel();
        var lbl = new TextBlock { Text = "MATLAB search path:", Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(lbl, Dock.Top); right.Children.Add(lbl); right.Children.Add(list);
        root.Children.Add(right);
        Content = root;
        list.MouseDoubleClick += (_, _) => { if (list.SelectedItem is string s && Directory.Exists(s)) System.Diagnostics.Process.Start("explorer.exe", s); };
        Closing += OnClosingAsk;
        Refresh();
    }

    static Button Btn(string text, RoutedEventHandler h, bool wide = false)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 3, 10, 3), Margin = wide ? new Thickness(0, 0, 0, 6) : new Thickness(6, 0, 0, 0), MinWidth = 80 };
        if (wide) b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.Click += h; return b;
    }

    void Refresh(IEnumerable<string>? select = null)
    {
        list.Items.Clear();
        foreach (var d in it.SearchPath) list.Items.Add(d);
        if (select != null) foreach (var s in select) if (list.Items.Contains(s)) list.SelectedItems.Add(s);
        bool dirty = !it.SearchPath.SequenceEqual(saved, PathManager.Cmp);
        status.Text = $"{it.SearchPath.Count} folders  ·  current folder ({Directory.GetCurrentDirectory()}) is always searched first, built-in CoreLib last"
            + (dirty ? "  ·  not saved" : "") + "\n" + PathManager.PathDefFile;
    }

    bool Guard()
    {
        if (canEdit()) return true;
        MessageBox.Show(this, "The interpreter is busy. Wait for the current command to finish.", "Set Path");
        return false;
    }

    List<string> Selected() => list.SelectedItems.Cast<string>().ToList();

    void Apply(List<string> l, IEnumerable<string>? select = null) { PathManager.Set(it, l); Refresh(select); }

    void OnAddFolder(object s, RoutedEventArgs e) => AddDlg(false);
    void OnAddSub(object s, RoutedEventArgs e) => AddDlg(true);
    void AddDlg(bool sub)
    {
        if (!Guard()) return;
        var dlg = new OpenFolderDialog { Title = sub ? "Add to Path with Subfolders" : "Add Folder to Path", Multiselect = true, InitialDirectory = Directory.GetCurrentDirectory() };
        if (dlg.ShowDialog(this) != true) return;
        var dirs = dlg.FolderNames.SelectMany(d => sub ? PathManager.GenPath(d) : new List<string> { PathManager.Normalize(d) }).ToList();
        PathManager.Add(it, dirs, false);
        Refresh(dirs);
    }

    void Move(int delta)
    {
        if (!Guard()) return;
        var sel = Selected(); if (sel.Count == 0) return;
        var l = it.SearchPath.ToList();
        if (delta == int.MinValue) { l.RemoveAll(sel.Contains); l.InsertRange(0, sel); }
        else if (delta == int.MaxValue) { l.RemoveAll(sel.Contains); l.AddRange(sel); }
        else
        {
            var idx = sel.Select(x => l.IndexOf(x)).OrderBy(i => delta < 0 ? i : -i).ToList();
            foreach (var i in idx)
            {
                int j = i + delta;
                if (j < 0 || j >= l.Count || sel.Contains(l[j])) continue;
                (l[i], l[j]) = (l[j], l[i]);
            }
        }
        Apply(l, sel);
    }

    void OnRemove(object s, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var sel = Selected(); if (sel.Count == 0) return;
        Apply(it.SearchPath.Where(x => !sel.Contains(x)).ToList());
    }

    void OnRevert(object s, RoutedEventArgs e) { if (Guard()) Apply(atOpen.ToList()); }
    void OnDefault(object s, RoutedEventArgs e)
    {
        if (!Guard()) return;
        if (MessageBox.Show(this, "Restore the default search path? Your folders will be removed from the path (not deleted from disk).", "Set Path", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        PathManager.RestoreDefault(it); Refresh();
    }
    void OnSave(object s, RoutedEventArgs e)
    {
        try { PathManager.Save(it); saved = it.SearchPath.ToList(); Refresh(); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save pathdef.m:\n" + ex.Message, "Set Path"); }
    }

    internal bool SuppressCloseQuestion;
    void OnClosingAsk(object? s, System.ComponentModel.CancelEventArgs e)
    {
        if (SuppressCloseQuestion || it.SearchPath.SequenceEqual(saved, PathManager.Cmp)) return;
        var r = MessageBox.Show(this, "Save the path for use in future sessions?", "Set Path", MessageBoxButton.YesNoCancel);
        if (r == MessageBoxResult.Cancel) e.Cancel = true;
        else if (r == MessageBoxResult.Yes) OnSave(this, new RoutedEventArgs());
    }

    internal void SaveScreenshot(string path)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        var c = (FrameworkElement)Content;
        var w = c.ActualWidth; var h = c.ActualHeight;
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(w * dpi.DpiScaleX), (int)Math.Ceiling(h * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(SystemColors.ControlBrush, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(c), null, new Rect(0, 0, w, h));
        }
        rtb.Render(dv);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }
}
