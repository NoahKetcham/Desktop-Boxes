using Boxes.App.Models;

namespace Boxes.App.ViewModels;

public class DesktopFileViewModel : ViewModelBase
{
    public string FilePath { get; }
    public string FileName { get; }

    public DesktopFileViewModel(ScannedFile model)
    {
        FilePath = model.FilePath;
        FileName = model.FileName;
    }
}
