using System.Threading;
using System.Threading.Tasks;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace Boxes.App.ViewModels;

public partial class DashboardBoxSettingsViewModel : ViewModelBase
{
    private readonly DashboardPageViewModel _dashboard;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    [ObservableProperty]
    private BoxSummaryViewModel? currentBox;

    [ObservableProperty]
    private string boxName = string.Empty;

    [ObservableProperty]
    private string boxDescription = string.Empty;

    public bool HasCurrentBox => CurrentBox != null;

    public IRelayCommand ClearCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    public DashboardBoxSettingsViewModel(DashboardPageViewModel dashboard)
    {
        _dashboard = dashboard;
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => CurrentBox != null);
        ClearCommand = new RelayCommand(Clear);
    }

    partial void OnCurrentBoxChanged(BoxSummaryViewModel? value)
    {
        OnPropertyChanged(nameof(HasCurrentBox));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnBoxNameChanged(string value)
    {
        _ = SaveCurrentAsync();
    }

    partial void OnBoxDescriptionChanged(string value)
    {
        _ = SaveCurrentAsync();
    }

    public void LoadBox(BoxSummaryViewModel box)
    {
        CurrentBox = box;
        BoxName = box.Name;
        BoxDescription = box.Description;
    }

    public void Clear()
    {
        CurrentBox = null;
        BoxName = string.Empty;
        BoxDescription = string.Empty;
    }

    private async Task SaveCurrentAsync()
    {
        if (CurrentBox is null)
        {
            return;
        }

        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CurrentBox.Name = BoxName;
            CurrentBox.Description = BoxDescription;

            var model = CurrentBox.ToModel();
            var updated = await AppServices.BoxService.AddOrUpdateAsync(model).ConfigureAwait(false);
            await AppServices.BoxWindowManager.UpdateAsync(updated).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentBox?.UpdateFromModel(updated);
            });
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task DeleteAsync()
    {
        if (CurrentBox is null)
        {
            return;
        }

        var confirmed = await DialogService.ShowDeleteConfirmationAsync(CurrentBox.Name);
        if (!confirmed)
        {
            return;
        }

        var boxToDelete = CurrentBox;
        Clear();

        await AppServices.BoxService.DeleteAsync(boxToDelete.Id);
        await AppServices.BoxWindowManager.CloseAsync(boxToDelete.Id);
        _dashboard.Boxes.Remove(boxToDelete);

        if (_dashboard.SelectedBox == boxToDelete)
        {
            _dashboard.SelectedBox = _dashboard.Boxes.Count > 0 ? _dashboard.Boxes[0] : null;
        }
    }
}
