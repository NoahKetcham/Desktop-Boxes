using System;
using Avalonia.Controls;
using Boxes.App.ViewModels;

namespace Boxes.App.Views.Dialogs;

public partial class DesktopBuildNameDialog : Window
{
    public DesktopBuildNameDialog()
    {
        InitializeComponent();
        AttachContext();
    }

    private void AttachContext()
    {
        if (DataContext is DesktopBuildNameDialogViewModel vm)
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

