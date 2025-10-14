using System;
using Avalonia.Controls;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow(string boxName)
    {
        InitializeComponent();
        AttachContext(boxName);
    }

    private void AttachContext(string boxName)
    {
        if (DataContext is ConfirmDeleteDialogViewModel vm)
        {
            vm.SetWindow(this);
            vm.BoxName = boxName;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ConfirmDeleteDialogViewModel vm)
        {
            vm.SetWindow(this);
        }
    }
}

