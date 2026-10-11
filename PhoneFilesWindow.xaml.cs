using DexBridge.Models;
using DexBridge.Services;
using Loc = DexBridge.Services.LocalizationService;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace DexBridge;

public partial class PhoneFilesWindow : Window
{
    private readonly AdbService _adb;
    private readonly string _serial;
    private readonly ObservableCollection<PhoneFileEntry> _entries = new();
    private bool _busy;

    public PhoneFilesWindow(AdbService adb, string serial)
    {
        InitializeComponent();
        Loc.Apply(this);
        Loc.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.LanguageChanged -= OnLanguageChanged;
        ThemeService.ApplyWindowPalette(this);
        ThemeService.ApplyWindowChrome(this);
        _adb = adb;
        _serial = serial;
        FileList.ItemsSource = _entries;
        Loaded += async (_, _) =>
        {
            ThemeService.ApplyWindowChrome(this);
            await RefreshAsync();
        };
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        Loc.Apply(this);
        FileList.Items.Refresh();
        if (!_busy) await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        SetButtons(false);
        try
        {
            StatusText.Text = Loc.T("Reading phone storage…");
            var entries = await _adb.ListDirectoryAsync(_serial, PathBox.Text);
            _entries.Clear();
            foreach (var entry in entries) _entries.Add(entry);
            StatusText.Text = Loc.F("{0} item(s)", entries.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.T("Unable to list folder: ") + ex.Message;
        }
        finally
        {
            _busy = false;
            SetButtons(true);
        }
    }

    private void SetButtons(bool enabled)
    {
        FileList.IsEnabled = enabled;
    }

    private async Task OpenSelectedAsync()
    {
        if (FileList.SelectedItem is not PhoneFileEntry entry || !entry.IsDirectory) return;
        PathBox.Text = entry.FullPath;
        await RefreshAsync();
    }

    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        PathBox.Text = AdbService.ParentRemotePath(PathBox.Text);
        await RefreshAsync();
    }

    private async void Go_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await OpenSelectedAsync();

    private async void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await RefreshAsync();
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not PhoneFileEntry entry)
        {
            StatusText.Text = Loc.T("Select a file first.");
            return;
        }
        if (entry.IsDirectory)
        {
            StatusText.Text = Loc.T("Folder download is not enabled yet; open the folder and select a file.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = entry.Name,
            Title = Loc.T("Download from Galaxy"),
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        _busy = true;
        SetButtons(false);
        try
        {
            StatusText.Text = Loc.F("Downloading {0}…", entry.Name);
            var result = await _adb.PullAsync(_serial, entry.FullPath, dialog.FileName);
            StatusText.Text = result.Success ? Loc.F("Downloaded to {0}", dialog.FileName) : Loc.T("Download failed: ") + result.Details;
        }
        finally
        {
            _busy = false;
            SetButtons(true);
        }
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Upload to Galaxy"),
            Multiselect = true,
            CheckFileExists = true,
            Filter = Loc.T("All files|*.*"),
        };
        if (dialog.ShowDialog(this) != true) return;
        await UploadFilesAsync(dialog.FileNames);
    }

    private async Task UploadFilesAsync(IEnumerable<string> files)
    {
        var localFiles = files.Where(File.Exists).ToArray();
        if (localFiles.Length == 0) return;

        _busy = true;
        SetButtons(false);
        try
        {
            var success = 0;
            foreach (var file in localFiles)
            {
                StatusText.Text = Loc.F("Uploading {0}…", Path.GetFileName(file));
                var result = await _adb.PushAsync(_serial, file, PathBox.Text);
                if (result.Success) success++;
                else StatusText.Text = Loc.T("Upload failed: ") + result.Details;
            }
            if (success == localFiles.Length)
                StatusText.Text = Loc.F("Uploaded {0} file(s).", success);
            await RefreshAsync();
        }
        finally
        {
            _busy = false;
            SetButtons(true);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            await UploadFilesAsync(files);
    }
}
