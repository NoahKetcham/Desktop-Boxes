namespace Boxes.App.ViewModels.Widgets;

public class NoteHubEntryViewModel : ViewModelBase
{
    public string Name { get; }
    public string RelativePath { get; }
    public bool IsFolder { get; }
    public string DisplayIcon => IsFolder ? "📁" : "📄";

    public CommunityToolkit.Mvvm.Input.IRelayCommand NavigateCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand RenameCommand { get; }

    public NoteHubEntryViewModel(string name, string relativePath, bool isFolder,
        CommunityToolkit.Mvvm.Input.IRelayCommand navigateCommand,
        CommunityToolkit.Mvvm.Input.IRelayCommand renameCommand)
    {
        Name = name;
        RelativePath = relativePath;
        IsFolder = isFolder;
        NavigateCommand = navigateCommand;
        RenameCommand = renameCommand;
    }
}
