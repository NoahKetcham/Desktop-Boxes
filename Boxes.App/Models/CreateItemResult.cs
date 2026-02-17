namespace Boxes.App.Models;

public enum CreateItemType
{
    Box,
    Notepad
}

public class CreateItemResult
{
    public CreateItemType Type { get; set; }
    public DesktopBox? Box { get; set; }
}
