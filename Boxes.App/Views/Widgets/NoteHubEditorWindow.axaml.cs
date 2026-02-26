using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.Services;
using Boxes.App.ViewModels.Widgets;
using TextMateSharp.Grammars;

namespace Boxes.App.Views.Widgets;

public partial class NoteHubEditorWindow : Window
{
    private bool _headerDragging;
    private PixelPoint _dragStartWindow;
    private PixelPoint _dragStartScreenPosition;
    private bool _isUpdatingFromEditor;
    private bool _editorAttached;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _textMateInstallation;

    public NoteHubEditorWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        DataContextChanged += OnDataContextChanged;
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is NoteHubWindowViewModel vm)
            vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void AttachEditor()
    {
        if (_editorAttached) return;
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor == null || DataContext is not NoteHubWindowViewModel vm) return;

        _editorAttached = true;
        editor.Text = vm.MarkdownText;
        editor.Document.TextChanged += Editor_TextChanged;

        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = editor.InstallTextMate(_registryOptions);
        try
        {
            var mdLang = _registryOptions.GetLanguageByExtension(".md");
            if (mdLang != null)
            {
                var scopeName = _registryOptions.GetScopeByLanguageId(mdLang.Id);
                _textMateInstallation.SetGrammar(scopeName);
            }
        }
        catch
        {
            // Markdown grammar may not be available; fall back to plain text
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NoteHubWindowViewModel.MarkdownText) || _isUpdatingFromEditor) return;
        if (DataContext is not NoteHubWindowViewModel vm) return;

        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor != null && editor.Text != vm.MarkdownText)
            editor.Text = vm.MarkdownText;
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor == null || DataContext is not NoteHubWindowViewModel vm) return;

        _isUpdatingFromEditor = true;
        try
        {
            vm.MarkdownText = editor.Text ?? string.Empty;
        }
        finally
        {
            _isUpdatingFromEditor = false;
        }
    }

    private NoteHubWindowViewModel ViewModel => (NoteHubWindowViewModel)DataContext!;

    private void OnOpened(object? sender, EventArgs e)
    {
        this.SetAlwaysBelowApps();
        ApplyTransparencyFromSettings();
        AttachEditor();
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings e)
    {
        ApplyAllSettings(e);
    }

    private void ApplyTransparencyFromSettings()
    {
        _ = AppServices.SettingsService.GetAsync().ContinueWith(t =>
        {
            if (t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && t.Result is { } s)
            {
                ApplyAllSettings(s);
            }
        });
    }

    private void ApplyAllSettings(ApplicationSettings settings)
    {
        var opacity = Math.Clamp(settings.BoxesTransparencyPercent, 0, 100) / 100.0;
        var backgroundColorHex = settings.BoxBackgroundColor ?? "#1C2235";

        Dispatcher.UIThread.Post(() =>
        {
            if (!Color.TryParse(backgroundColorHex, out var parsedColor))
            {
                parsedColor = Color.Parse("#1C2235");
            }
            var bgColor = parsedColor;

            var contentPadding = Math.Clamp(settings.BoxContentPadding, 0, 64);
            var verticalPadding = Math.Clamp(settings.BoxContentVerticalPadding, 0, 64);
            var contentArea = this.FindControl<Border>("ContentArea");
            if (contentArea != null)
            {
                var vertical = verticalPadding > 0 ? verticalPadding : 12;
                contentArea.Padding = new Thickness(contentPadding, vertical, contentPadding, vertical);
            }

            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 20);
            var rootGrid = this.FindControl<Grid>("RootGrid");
            if (rootGrid != null)
            {
                rootGrid.Background = new SolidColorBrush(bgColor, opacity);
                rootGrid.ClipToBounds = true;
            }

            var r = bgColor.R / 255.0;
            var g = bgColor.G / 255.0;
            var b = bgColor.B / 255.0;
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            Color headerStyleColor;
            if (luminance < 0.5)
            {
                headerStyleColor = Color.FromRgb(
                    (byte)Math.Min(255, bgColor.R + (255 - bgColor.R) * 0.1),
                    (byte)Math.Min(255, bgColor.G + (255 - bgColor.G) * 0.1),
                    (byte)Math.Min(255, bgColor.B + (255 - bgColor.B) * 0.1)
                );
            }
            else
            {
                headerStyleColor = Color.FromRgb(
                    (byte)(bgColor.R * 0.8),
                    (byte)(bgColor.G * 0.8),
                    (byte)(bgColor.B * 0.8)
                );
            }

            var headerBar = this.FindControl<Border>("HeaderBar");
            if (headerBar != null)
            {
                headerBar.IsVisible = settings.ShowBoxHeader;
                var headerHeight = Math.Clamp(settings.BoxHeaderHeight, 30, 60);
                headerBar.MinHeight = headerHeight;
                headerBar.Padding = new Thickness(10, (headerHeight - 26) / 2);
                headerBar.CornerRadius = settings.ShowBoxHeader
                    ? new CornerRadius(cornerRadius, cornerRadius, 0, 0)
                    : new CornerRadius(cornerRadius, cornerRadius, 0, 0);
                headerBar.Background = new SolidColorBrush(headerStyleColor, opacity);
            }

            if (contentArea != null)
            {
                contentArea.CornerRadius = settings.ShowBoxHeader
                    ? new CornerRadius(0, 0, cornerRadius, cornerRadius)
                    : new CornerRadius(cornerRadius);
            }
        });
    }

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void InsertMarkdownWrap(string before, string after)
    {
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor == null) return;

        var text = editor.Text ?? string.Empty;
        var start = editor.SelectionStart;
        var end = start + editor.SelectionLength;
        var selected = start < end ? text.Substring(start, end - start) : "";

        string newText;
        int newCaret;
        if (selected.Length > 0)
        {
            newText = text.Substring(0, start) + before + selected + after + text.Substring(end);
            newCaret = start + before.Length + selected.Length + after.Length;
        }
        else
        {
            newText = text.Substring(0, start) + before + after + text.Substring(start);
            newCaret = start + before.Length;
        }

        editor.Text = newText;
        editor.CaretOffset = newCaret;
        editor.SelectionStart = newCaret;
        editor.SelectionLength = 0;
        ViewModel.MarkdownText = newText;
    }

    private void InsertMarkdownLinePrefix(string prefix)
    {
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor == null) return;

        var text = editor.Text ?? string.Empty;
        var caret = editor.CaretOffset;
        var searchFrom = caret > 0 ? caret - 1 : 0;
        var lineStart = text.LastIndexOf('\n', searchFrom) + 1;
        var newText = text.Substring(0, lineStart) + prefix + text.Substring(lineStart);
        editor.Text = newText;
        editor.CaretOffset = caret + prefix.Length;
        editor.SelectionStart = editor.CaretOffset;
        editor.SelectionLength = 0;
        ViewModel.MarkdownText = newText;
    }

    private void InsertMarkdownColorStyle(string style)
    {
        InsertMarkdownWrap($"%{{{style}}}", "%");
    }

    private void InsertMarkdownBlock(string block)
    {
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor == null) return;

        var text = editor.Text ?? string.Empty;
        var caret = editor.CaretOffset;
        var insert = (caret > 0 && text[caret - 1] != '\n' ? "\n" : "") + block + "\n";
        var newText = text.Substring(0, caret) + insert + text.Substring(caret);
        editor.Text = newText;
        editor.CaretOffset = caret + insert.Length;
        editor.SelectionStart = editor.CaretOffset;
        editor.SelectionLength = 0;
        ViewModel.MarkdownText = newText;
    }

    private void MarkdownBold_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("**", "**");
    private void MarkdownItalic_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("*", "*");
    private void MarkdownStrikethrough_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("~~", "~~");
    private void MarkdownCode_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("`", "`");
    private void MarkdownCodeBlock_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("\n```\n", "\n```\n");
    private void MarkdownLink_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownWrap("[", "](url)");
    private void MarkdownHeading1_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("# ");
    private void MarkdownHeading2_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("## ");
    private void MarkdownHeading3_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("### ");
    private void MarkdownBullet_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("- ");
    private void MarkdownNumbered_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("1. ");
    private void MarkdownBlockquote_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownLinePrefix("> ");
    private void MarkdownHr_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownBlock("---");
    private void MarkdownHighlight_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("background:yellow");
    private void MarkdownColorRed_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:red");
    private void MarkdownColorGreen_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:green");
    private void MarkdownColorBlue_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:blue");
    private void MarkdownColorOrange_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:orange");
    private void MarkdownColorYellow_OnClick(object? sender, RoutedEventArgs e) => InsertMarkdownColorStyle("color:yellow");

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var pointerRelative = e.GetPosition(this);
        var screenX = Position.X + (int)Math.Round(pointerRelative.X);
        var screenY = Position.Y + (int)Math.Round(pointerRelative.Y);

        _headerDragging = true;
        _dragStartWindow = Position;
        _dragStartScreenPosition = new PixelPoint(screenX, screenY);
        e.Pointer.Capture((IInputElement)sender!);
        e.Handled = true;
    }

    private void Header_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_headerDragging)
            return;

        var currentPointerRelative = e.GetPosition(this);
        var currentScreenX = Position.X + (int)Math.Round(currentPointerRelative.X);
        var currentScreenY = Position.Y + (int)Math.Round(currentPointerRelative.Y);
        var currentScreenPos = new PixelPoint(currentScreenX, currentScreenY);

        var deltaX = currentScreenPos.X - _dragStartScreenPosition.X;
        var deltaY = currentScreenPos.Y - _dragStartScreenPosition.Y;

        var newX = _dragStartWindow.X + deltaX;
        var newY = _dragStartWindow.Y + deltaY;
        Position = new PixelPoint(newX, newY);
        e.Handled = true;
    }

    private void Header_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_headerDragging)
        {
            _headerDragging = false;
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        WindowEdge? edge = border.Tag switch
        {
            "Right" => WindowEdge.East,
            "Bottom" => WindowEdge.South,
            "BottomRight" => WindowEdge.SouthEast,
            _ => null
        };

        if (edge.HasValue)
        {
            BeginResizeDrag(edge.Value, e);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is NoteHubWindowViewModel vm)
            vm.PropertyChanged -= ViewModel_PropertyChanged;
        var editor = this.FindControl<TextEditor>("EditorTextBox");
        if (editor != null)
            editor.Document.TextChanged -= Editor_TextChanged;
        base.OnClosed(e);
        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
    }
}
