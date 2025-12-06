namespace Boxes.App.ViewModels;

public class NavigationItemViewModel : ViewModelBase
{
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
    public ViewModelBase Content { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public NavigationItemViewModel(string title, string description, ViewModelBase content, string icon = "📁")
    {
        Title = title;
        Description = description;
        Content = content;
        Icon = icon;
    }
}

