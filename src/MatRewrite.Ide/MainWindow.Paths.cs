// MatRewrite - GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Input;
using MatRewrite.Core.Runtime;
using Microsoft.Win32;

namespace MatRewrite.Ide;

public sealed record FolderItem(string FullPath, bool IsDir, bool OnPath)
{
    public string Display => (IsDir ? "\U0001F4C1 " : "    ") + Path.GetFileName(FullPath) + (IsDir && OnPath ? "   (on path)" : "");
}

/// <summary>Current-folder browser, folder box and Set Path dialog wiring.</summary>
public partial class MainWindow
{
    SetPathWindow? setPathWin;

    void InitPaths()
    {
        PathManager.LoadStartup(it);
        it.PathChanged += () => Dispatcher.BeginInvoke(() => { if (!busy) RefreshFolder(); });
        it.PathToolRequested = () => Dispatcher.BeginInvoke(() => OpenSetPath());
    }

    void UpdateCwd()
    {
        CwdText.Text = Environment.CurrentDirectory;
        if (!FolderBox.IsKeyboardFocusWithin) FolderBox.Text = Environment.CurrentDirectory;
        RefreshFolder();
    }

    void RefreshFolder()
    {
        var cwd = Environment.CurrentDirectory;
        var items = new List<FolderItem>();
        try
        {
            var onPath = new HashSet<string>(it.SearchPath, PathManager.Cmp);
            foreach (var d in Directory.GetDirectories(cwd).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (!Path.GetFileName(d).StartsWith('.')) items.Add(new FolderItem(d, true, onPath.Contains(d)));
            foreach (var f in Directory.GetFiles(cwd).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                items.Add(new FolderItem(f, false, false));
        }
        catch { }
        FolderList.ItemsSource = items;
    }

    bool ChangeFolder(string dir)
    {
        if (busy) { AppendOutput("(busy - wait for the current command to finish)\n"); return false; }
        try { dir = Path.GetFullPath(Environment.ExpandEnvironmentVariables(dir.Trim().Trim('"'))); } catch { }
        if (!Directory.Exists(dir)) { MessageBox.Show(this, $"Folder not found:\n{dir}", "Current Folder"); FolderBox.Text = Environment.CurrentDirectory; return false; }
        Execute($"cd('{dir.Replace("'", "''")}')");
        return true;
    }

    void OnFolderBoxKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ChangeFolder(FolderBox.Text); Keyboard.ClearFocus(); }
        else if (e.Key == Key.Escape) { FolderBox.Text = Environment.CurrentDirectory; Keyboard.ClearFocus(); }
    }
    void OnFolderUp(object sender, RoutedEventArgs e)
    {
        var p = Directory.GetParent(Environment.CurrentDirectory);
        if (p != null) ChangeFolder(p.FullName);
    }
    void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select current folder", InitialDirectory = Environment.CurrentDirectory };
        if (dlg.ShowDialog(this) == true) ChangeFolder(dlg.FolderName);
    }

    List<FolderItem> SelectedFolderItems() => FolderList.SelectedItems.Cast<FolderItem>().ToList();

    void OnFolderItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FolderList.SelectedItem is FolderItem fi) OpenItem(fi);
    }
    void OpenItem(FolderItem fi)
    {
        if (fi.IsDir) ChangeFolder(fi.FullPath);
        else if (fi.FullPath.EndsWith(".m", StringComparison.OrdinalIgnoreCase)) LoadFile(fi.FullPath);
        else try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fi.FullPath) { UseShellExecute = true }); } catch { }
    }
    void OnFolderOpen(object sender, RoutedEventArgs e) { foreach (var fi in SelectedFolderItems().Take(1)) OpenItem(fi); }

    void AddSelected(bool sub)
    {
        if (busy) { AppendOutput("(busy - wait for the current command to finish)\n"); return; }
        var dirs = SelectedFolderItems().Where(f => f.IsDir).Select(f => f.FullPath).ToList();
        if (dirs.Count == 0) return;
        var all = sub ? dirs.SelectMany(d => PathManager.GenPath(d)).ToList() : dirs;
        PathManager.Add(it, all, false);
        AppendOutput($"% added {all.Count} folder(s) to the path (use Set Path > Save to keep them)\n");
        RefreshFolder(); setPathWinRefresh();
    }
    void OnAddSelectedToPath(object sender, RoutedEventArgs e) => AddSelected(false);
    void OnAddSelectedSubToPath(object sender, RoutedEventArgs e) => AddSelected(true);
    void OnAddCurrentToPath(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        PathManager.Add(it, new[] { Environment.CurrentDirectory }, false); RefreshFolder(); setPathWinRefresh();
    }
    void OnRemoveSelectedFromPath(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        PathManager.Remove(it, SelectedFolderItems().Where(f => f.IsDir).Select(f => f.FullPath)); RefreshFolder(); setPathWinRefresh();
    }
    void OnShowInExplorer(object sender, RoutedEventArgs e)
    {
        var fi = SelectedFolderItems().FirstOrDefault();
        var arg = fi == null ? Environment.CurrentDirectory : fi.IsDir ? fi.FullPath : "/select,\"" + fi.FullPath + "\"";
        try { System.Diagnostics.Process.Start("explorer.exe", arg); } catch { }
    }
    void OnFolderRefresh(object sender, RoutedEventArgs e) => RefreshFolder();

    void setPathWinRefresh() { if (setPathWin != null) { setPathWin.Close(); setPathWin = null; } }

    void OnSetPath(object sender, RoutedEventArgs e) => OpenSetPath();
    internal SetPathWindow OpenSetPath()
    {
        if (setPathWin != null) { setPathWin.Activate(); return setPathWin; }
        setPathWin = new SetPathWindow(it, () => !busy) { Owner = this };
        setPathWin.Closed += (_, _) => { setPathWin = null; RefreshFolder(); };
        setPathWin.Show();
        return setPathWin;
    }
}
