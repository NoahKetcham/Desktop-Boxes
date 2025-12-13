namespace Boxes.App.ViewModels;

public class BoxTemplateOptionViewModel : ViewModelBase
{
    public string Key { get; }
    public string Name { get; }
    public string ShortDescription { get; }
    public string LongDescription { get; }

    public BoxTemplateOptionViewModel(string key, string name, string shortDescription, string longDescription)
    {
        Key = key;
        Name = name;
        ShortDescription = shortDescription;
        LongDescription = longDescription;
    }
}


