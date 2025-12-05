using System;
using Boxes.App.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels;

public partial class DesktopBuildViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private DateTime createdAt;

    [ObservableProperty]
    private int boxCount;

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("g");

    public static DesktopBuildViewModel FromModel(DesktopBuild build)
    {
        return new DesktopBuildViewModel
        {
            Id = build.Id,
            Name = build.Name,
            CreatedAt = build.CreatedAt,
            BoxCount = build.Boxes.Count
        };
    }
}

