using System;
using Boxes.App.Models;

namespace Boxes.App.ViewModels;

public class NotepadSummaryViewModel : ViewModelBase
{
    public Guid Id { get; private set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public static NotepadSummaryViewModel FromModel(Notepad model)
    {
        return new NotepadSummaryViewModel
        {
            Id = model.Id,
            Name = model.Name
        };
    }

    public void UpdateFromModel(Notepad model)
    {
        Id = model.Id;
        Name = model.Name;
    }

    public Notepad ToModel()
    {
        return new Notepad
        {
            Id = Id,
            Name = Name
        };
    }
}
