using System;
using Avalonia.Controls;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class InputDialog : Window
{
    public InputDialog()
    {
        InitializeComponent();
        AttachContext();
    }

    private void AttachContext()
    {
        if (DataContext is InputDialogViewModel vm)
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
