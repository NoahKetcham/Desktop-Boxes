using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace Boxes.App.ViewModels;

public partial class ShortcutSelectionViewModel : ViewModelBase
{
    public ObservableCollection<ShortcutSelectionItemViewModel> Shortcuts { get; }
    public string BoxName { get; }

    public event EventHandler<bool>? CloseRequested;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public ShortcutSelectionViewModel(string boxName, IEnumerable<ScannedFile> files, IEnumerable<Guid> selected)
    {
        BoxName = boxName;
        var selectedSet = new HashSet<Guid>(selected);
        Shortcuts = new ObservableCollection<ShortcutSelectionItemViewModel>(
            files.Select(f => new ShortcutSelectionItemViewModel(f, selectedSet.Contains(f.Id))));

        SaveCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    public IEnumerable<ScannedFile> GetSelectedFiles()
    {
        return Shortcuts.Where(s => s.IsSelected).Select(s => s.File);
    }
}
