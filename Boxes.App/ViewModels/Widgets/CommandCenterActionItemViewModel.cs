using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels.Widgets;

public partial class CommandCenterActionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled;

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }

    public CommandCenterActionItemViewModel(
        string key,
        string title,
        string description,
        string icon,
        bool isEnabled,
        Action moveUp,
        Action moveDown)
    {
        Key = key;
        Title = title;
        Description = description;
        Icon = icon;
        _isEnabled = isEnabled;
        MoveUpCommand = new RelayCommand(moveUp);
        MoveDownCommand = new RelayCommand(moveDown);
    }
}
