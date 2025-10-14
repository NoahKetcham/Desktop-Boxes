using System;
using Avalonia.Controls;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class NewBoxWindow : Window
{
    public NewBoxWindow()
    {
        InitializeComponent();
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

