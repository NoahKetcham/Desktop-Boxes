using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels.Widgets;

public partial class NoteHubWindowViewModel : ViewModelBase
{
    private const int SaveDebounceMs = 500;

    private DispatcherTimer? _saveDebounceTimer;

    [ObservableProperty]
    private string _currentRelativePath = string.Empty;

    [ObservableProperty]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private bool _isFileMode;

    public ObservableCollection<BreadcrumbSegmentViewModel> BreadcrumbSegments { get; } = new();
    public ObservableCollection<NoteHubEntryViewModel> CurrentEntries { get; } = new();

    public IRelayCommand CloseCommand { get; }
    public IRelayCommand CloseEditorCommand { get; }
    public IRelayCommand OpenEditCommand { get; }
    public IRelayCommand<string> NavigateToCommand { get; }
    public IRelayCommand CreateNewNoteCommand { get; }
    public IRelayCommand CreateFolderCommand { get; }

    public event EventHandler? RequestClose;
    public event EventHandler? RequestCloseEditor;
    public event EventHandler? RequestOpenEditor;

    public string DisplayName => IsFileMode ? GetFileName(CurrentRelativePath) : "NoteHub";

    public NoteHubWindowViewModel(string? initialPath = null)
    {
        CurrentRelativePath = initialPath ?? string.Empty;
        CloseCommand = new RelayCommand(OnClose);
        CloseEditorCommand = new RelayCommand(OnCloseEditor);
        OpenEditCommand = new RelayCommand(OnOpenEdit);
        NavigateToCommand = new RelayCommand<string>(NavigateTo);
        CreateNewNoteCommand = new RelayCommand(CreateNewNote);
        CreateFolderCommand = new RelayCommand(CreateFolder);

        _ = RefreshAsync();
    }

    partial void OnCurrentRelativePathChanged(string value)
    {
        _ = RefreshAsync();
    }

    partial void OnMarkdownTextChanged(string value)
    {
        if (IsFileMode)
            ScheduleDebouncedSave();
    }

    private static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "NoteHub";
        var idx = path.Replace('\\', '/').LastIndexOf('/');
        return idx >= 0 ? path[(idx + 1)..] : path;
    }

    private bool IsFilePath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshAsync()
    {
        IsFileMode = IsFilePath(CurrentRelativePath);
        RebuildBreadcrumb();

        if (IsFileMode)
        {
            var content = await AppServices.NoteHubService.GetContentAsync(CurrentRelativePath).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MarkdownText = content;
            });
        }
        else
        {
            var entries = await AppServices.NoteHubService.GetEntriesAsync(CurrentRelativePath).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentEntries.Clear();
                foreach (var e in entries)
                {
                    var path = e.RelativePath;
                    var cmd = new RelayCommand(() => NavigateTo(path));
                    var renameCmd = new RelayCommand(async () => await RenameEntryAsync(e.RelativePath, e.Name, e.IsFolder));
                    CurrentEntries.Add(new NoteHubEntryViewModel(e.Name, e.RelativePath, e.IsFolder, cmd, renameCmd));
                }
            });
        }

        OnPropertyChanged(nameof(DisplayName));
    }

    private void RebuildBreadcrumb()
    {
        BreadcrumbSegments.Clear();
        var parts = CurrentRelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSoFar = string.Empty;

        BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel("NoteHub", "", parts.Length == 0, new RelayCommand(() => NavigateTo(""))));

        for (var i = 0; i < parts.Length; i++)
        {
            pathSoFar = string.IsNullOrEmpty(pathSoFar) ? parts[i] : pathSoFar + "/" + parts[i];
            var isLast = i == parts.Length - 1;
            var path = pathSoFar;
            BreadcrumbSegments.Add(new BreadcrumbSegmentViewModel(parts[i], path, isLast, new RelayCommand(() => NavigateTo(path))));
        }
    }

    private void NavigateTo(string? relativePath)
    {
        if (relativePath == null) return;
        CurrentRelativePath = relativePath;
    }

    private void CreateNewNote()
    {
        var baseName = "untitled";
        var ext = ".md";
        var dir = string.IsNullOrEmpty(CurrentRelativePath) ? "" : CurrentRelativePath.TrimEnd('/') + "/";
        var candidate = dir + baseName + ext;
        var n = 1;
        while (CurrentEntries.Any(e => e.RelativePath.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = dir + baseName + n + ext;
            n++;
        }
        CurrentRelativePath = candidate;
    }

    private async void CreateFolder()
    {
        var baseName = "New folder";
        var dir = string.IsNullOrEmpty(CurrentRelativePath) ? "" : CurrentRelativePath.TrimEnd('/') + "/";
        var candidate = baseName;
        var n = 0;
        while (CurrentEntries.Any(e => e.IsFolder && e.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            n++;
            candidate = n == 1 ? baseName + " 1" : baseName + " " + n;
        }
        var name = await DialogService.ShowInputDialogAsync("New folder", "Enter folder name:", candidate, "Folder name", "Create").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name)) return;

        var relativePath = string.IsNullOrEmpty(dir) ? name : dir + name;
        await AppServices.NoteHubService.CreateFolderAsync(relativePath).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(RefreshAsync);
    }

    private async Task RenameEntryAsync(string relativePath, string currentName, bool isFolder)
    {
        var name = await DialogService.ShowInputDialogAsync("Rename", "Enter new name:", currentName, "Name", "Rename").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(name)) return;

        var newName = isFolder ? name.Trim() : (name.Trim().EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name.Trim() : name.Trim() + ".md");
        await AppServices.NoteHubService.RenameAsync(relativePath, newName).ConfigureAwait(false);

        if (CurrentRelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
        {
            var parts = relativePath.Replace('\\', '/').Split('/');
            var dir = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : "";
            CurrentRelativePath = string.IsNullOrEmpty(dir) ? newName : dir + "/" + newName;
        }

        await Dispatcher.UIThread.InvokeAsync(RefreshAsync);
    }

    private void ScheduleDebouncedSave()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(SaveDebounceMs)
        };
        _saveDebounceTimer.Tick += async (_, _) =>
        {
            _saveDebounceTimer?.Stop();
            _saveDebounceTimer = null;
            await AppServices.NoteHubService.SaveContentAsync(CurrentRelativePath, MarkdownText).ConfigureAwait(false);
        };
        _saveDebounceTimer.Start();
    }

    private void OnClose()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseEditor()
    {
        RequestCloseEditor?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenEdit()
    {
        RequestOpenEditor?.Invoke(this, EventArgs.Empty);
    }

    public void SaveImmediately()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = null;
        if (IsFileMode)
            _ = AppServices.NoteHubService.SaveContentAsync(CurrentRelativePath, MarkdownText);
    }
}
