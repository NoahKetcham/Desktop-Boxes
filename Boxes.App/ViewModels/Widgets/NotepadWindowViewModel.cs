using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels.Widgets;

public partial class NotepadWindowViewModel : ViewModelBase
{
    private const int SaveDebounceMs = 500;

    private DispatcherTimer? _saveDebounceTimer;

    [ObservableProperty]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    private double _splitterPosition = 0.5;

    public IRelayCommand CloseCommand { get; }

    public event EventHandler? RequestClose;

    public NotepadWindowViewModel()
    {
        CloseCommand = new RelayCommand(OnClose);
        _ = LoadContentAsync();
    }

    partial void OnMarkdownTextChanged(string value)
    {
        ScheduleDebouncedSave();
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
            await AppServices.NotepadService.SaveContentAsync(MarkdownText).ConfigureAwait(false);
        };
        _saveDebounceTimer.Start();
    }

    private async Task LoadContentAsync()
    {
        var content = await AppServices.NotepadService.GetContentAsync().ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MarkdownText = content;
        });
    }

    private void OnClose()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void SaveImmediately()
    {
        _saveDebounceTimer?.Stop();
        _saveDebounceTimer = null;
        _ = AppServices.NotepadService.SaveContentAsync(MarkdownText);
    }
}
