using System;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.Input;

namespace Boxes.App.ViewModels;

public class DesktopBoxWindowViewModel : ViewModelBase
{
    public DesktopBox Model { get; private set; }

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name != value)
            {
                Model.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => Model.Description;
        set
        {
            if (Model.Description != value)
            {
                Model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public RelayCommand CloseCommand { get; }
    public event EventHandler? RequestClose;

    public DesktopBoxWindowViewModel(DesktopBox model)
    {
        Model = model;
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public void Update(DesktopBox model)
    {
        Model = model;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

