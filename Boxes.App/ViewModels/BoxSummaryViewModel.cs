using System;
using Boxes.App.Models;

namespace Boxes.App.ViewModels;

public class BoxSummaryViewModel : ViewModelBase
{
    public Guid Id { get; private set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private int _itemCount;
    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public static BoxSummaryViewModel FromModel(DesktopBox model)
    {
        return new BoxSummaryViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            ItemCount = model.ItemCount
        };
    }

    public void UpdateFromModel(DesktopBox model)
    {
        Id = model.Id;
        Name = model.Name;
        Description = model.Description;
        ItemCount = model.ItemCount;
    }

    public DesktopBox ToModel()
    {
        return new DesktopBox
        {
            Id = Id,
            Name = Name,
            Description = Description,
            ItemCount = ItemCount
        };
    }
}

