using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Boxes.App.Views;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var colorPickerButton = this.FindControl<Button>("ColorPickerButton");
        var colorPickerPopup = this.FindControl<Popup>("ColorPickerPopup");
        if (colorPickerButton != null && colorPickerPopup != null)
        {
            colorPickerPopup.PlacementTarget = colorPickerButton;
        }

        var accentColorPickerButton = this.FindControl<Button>("AccentColorPickerButton");
        var accentColorPickerPopup = this.FindControl<Popup>("AccentColorPickerPopup");
        if (accentColorPickerButton != null && accentColorPickerPopup != null)
        {
            accentColorPickerPopup.PlacementTarget = accentColorPickerButton;
        }
    }
}

