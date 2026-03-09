using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels.Widgets;

public partial class CommandCenterActionButtonViewModel : ObservableObject
{
    [ObservableProperty]
    private string _icon;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _showTitle = true;

    public string Key { get; }
    public ICommand Command { get; }

    public CommandCenterActionButtonViewModel(string key, string title, string description, string icon, bool showTitle, ICommand command)
    {
        Key = key;
        _icon = icon;
        _title = title;
        _description = description;
        _showTitle = showTitle;
        Command = command;
    }
}
