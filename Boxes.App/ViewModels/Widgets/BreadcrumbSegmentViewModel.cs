using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels.Widgets;

public class BreadcrumbSegmentViewModel : ViewModelBase
{
    public string DisplayName { get; }
    public string RelativePath { get; }
    public bool IsLast { get; }

    public IRelayCommand NavigateCommand { get; }

    public BreadcrumbSegmentViewModel(string displayName, string relativePath, bool isLast, IRelayCommand navigateCommand)
    {
        DisplayName = displayName;
        RelativePath = relativePath;
        IsLast = isLast;
        NavigateCommand = navigateCommand;
    }
}
