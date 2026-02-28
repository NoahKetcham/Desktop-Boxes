using System;
using Avalonia.Controls;
using Boxes.App.Models;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class NewBoxWindow : Window
{
    public NewBoxWindow() : this(CreateItemType.Box) { }

    public NewBoxWindow(CreateItemType defaultType)
    {
        InitializeComponent();
        Title = defaultType == CreateItemType.Notepad ? "Create Notepad" : "Create Box";
        if (DataContext is NewBoxDialogViewModel vm)
        {
            vm.SelectedType = defaultType;
        }
        AttachContext();
    }

    private void AttachContext()
    {
        if (DataContext is NewBoxDialogViewModel vm)
        {
            vm.SetWindow(this);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        AttachContext();
    }
}

