using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.Services;
using Boxes.App.ViewModels.Widgets;

namespace Boxes.App.Views.Widgets;

public partial class CommandCenterWindow : Window
{
    private const double ActionCellSize = 52;

    private Border? _outerBorder;
    private double _actionSpacingHorizontal;
    private double _actionSpacingVertical;
    private DispatcherTimer? _stateSaveTimer;
    private CommandCenterWindowViewModel? _boundViewModel;
    private ResizeDragMode _resizeDragMode;
    private IPointer? _activeResizePointer;
    private bool _hasInitializedLayout;
    private PixelRect _lastScreenBounds;

    private enum ResizeDragMode
    {
        None,
        Right,
        Bottom,
        BottomRight
    }

    public CommandCenterWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        DataContextChanged += OnDataContextChanged;
        Activated += (_, _) =>
        {
            if (!AppServices.BoxWindowManager.IsBurstActive)
            {
                this.SetAlwaysBelowApps();
            }
        };
        PositionChanged += OnPositionChanged;
        _outerBorder = this.FindControl<Border>("OuterBorder");
        Resources["CommandCenterActionMargin"] = new Thickness(0, 0, 0, 0);
        AppServices.SettingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        this.SetAlwaysBelowApps();
        this.HideDwmBorder();
        AttachViewModel(DataContext as CommandCenterWindowViewModel);
        ApplySettingsFromService();
        Dispatcher.UIThread.Post(() => SnapToNearestLayout(force: true));
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel(DataContext as CommandCenterWindowViewModel);
        Dispatcher.UIThread.Post(() => SnapToNearestLayout(force: true));
    }

    private void OnPositionChanged(object? sender, EventArgs e)
    {
        ScheduleStateSave();
        var screen = GetCurrentScreen();
        if (screen != null && screen.Bounds != _lastScreenBounds)
        {
            _lastScreenBounds = screen.Bounds;
            _ = AppServices.SettingsService.GetAsync().ContinueWith(task =>
            {
                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result is { } settings &&
                    settings.CommandCenterAutoScale)
                {
                    Dispatcher.UIThread.Post(() => ApplyAllSettings(settings));
                }
            });
        }
    }

    private void OnSettingsChanged(object? sender, ApplicationSettings settings)
    {
        ApplyAllSettings(settings);
    }

    private Screen? GetCurrentScreen()
    {
        var screens = Screens;
        if (screens?.All is not { } allScreens)
        {
            return null;
        }

        var position = Position;
        foreach (var screen in allScreens)
        {
            if (screen.Bounds.Contains(position))
            {
                return screen;
            }
        }

        return null;
    }

    private void ApplySettingsFromService()
    {
        _ = AppServices.SettingsService.GetAsync().ContinueWith(task =>
        {
            if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result is { } settings)
            {
                ApplyAllSettings(settings);
            }
        });
    }

    private void ApplyAllSettings(ApplicationSettings settings)
    {
        var opacity = Math.Clamp(settings.BoxesTransparencyPercent, 0, 100) / 100.0;
        var backgroundColorHex = settings.BoxBackgroundColor ?? "#1C2235";

        Dispatcher.UIThread.Post(() =>
        {
            if (_outerBorder == null)
            {
                return;
            }

            if (!Color.TryParse(backgroundColorHex, out var bgColor))
            {
                bgColor = Color.Parse("#1C2235");
            }

            var cornerRadius = Math.Clamp(settings.BoxCornerRadius, 0, 24);
            _outerBorder.CornerRadius = new CornerRadius(cornerRadius);
            _outerBorder.Background = new SolidColorBrush(bgColor, opacity);
            CanResize = false;

            _outerBorder.BorderBrush = settings.CommandCenterShowBorder
                ? new SolidColorBrush(bgColor)
                : Brushes.Transparent;
            _outerBorder.BorderThickness = settings.CommandCenterShowBorder ? new Thickness(1) : new Thickness(0);

            int padLeft, padRight, padV;
            double spacingH, spacingV;

            if (settings.CommandCenterAutoScale)
            {
                var screen = GetCurrentScreen();
                if (screen != null)
                {
                    var (padL, padR, padVVal, spH, spV) = CommandCenterScaleService.ComputeForScreen(
                        screen.Bounds.Width, screen.Bounds.Height, RenderScaling);
                    padLeft = Math.Clamp(padL, -16, 32);
                    padRight = Math.Clamp(padR, -16, 32);
                    padV = Math.Clamp(padVVal, 0, 32);
                    spacingH = Math.Clamp(spH, -16, 24);
                    spacingV = Math.Clamp(spV, -16, 24);
                    _lastScreenBounds = screen.Bounds;
                }
                else
                {
                    padLeft = Math.Clamp(settings.CommandCenterPaddingLeft, -16, 32);
                    padRight = Math.Clamp(settings.CommandCenterPaddingRight, -16, 32);
                    padV = Math.Clamp(settings.CommandCenterPaddingVertical, 0, 32);
                    spacingH = Math.Clamp(settings.CommandCenterActionSpacingHorizontal, -16, 24);
                    spacingV = Math.Clamp(settings.CommandCenterActionSpacingVertical, -16, 24);
                }
            }
            else
            {
                padLeft = Math.Clamp(settings.CommandCenterPaddingLeft, -16, 32);
                padRight = Math.Clamp(settings.CommandCenterPaddingRight, -16, 32);
                padV = Math.Clamp(settings.CommandCenterPaddingVertical, 0, 32);
                spacingH = Math.Clamp(settings.CommandCenterActionSpacingHorizontal, -16, 24);
                spacingV = Math.Clamp(settings.CommandCenterActionSpacingVertical, -16, 24);
            }

            _outerBorder.Padding = new Thickness(padLeft, padV, padRight, padV);

            _actionSpacingHorizontal = spacingH;
            _actionSpacingVertical = spacingV;
            var marginH = _actionSpacingHorizontal / 2.0;
            var marginV = _actionSpacingVertical / 2.0;
            Resources["CommandCenterActionMargin"] = new Thickness(marginH, marginV, marginH, marginV);

            var actionsControl = this.FindControl<ItemsControl>("ActionsItemsControl");
            if (actionsControl?.GetVisualDescendants().OfType<WrapPanel>().FirstOrDefault() is { } wrapPanel)
            {
                wrapPanel.ItemWidth = ActionCellSize + _actionSpacingHorizontal;
                wrapPanel.ItemHeight = ActionCellSize + _actionSpacingVertical;
            }

            ApplyActionButtonColors(this, settings);

            UpdateLayoutBounds();
            SnapToNearestLayout(force: _hasInitializedLayout);
        });
    }

    private void ApplyActionButtonColors(TopLevel topLevel, ApplicationSettings settings)
    {
        var primary = ResolveActionButtonPrimaryColor(topLevel, settings);
        var showBg = settings.CommandCenterActionButtonShowBackground;
        var secondary = showBg ? Color.FromArgb(0x14, primary.R, primary.G, primary.B) : Colors.Transparent;
        var secondaryHover = showBg ? Color.FromArgb(0x28, primary.R, primary.G, primary.B) : Colors.Transparent;
        var iconBg = Color.FromArgb(0x20, primary.R, primary.G, primary.B);

        Resources["CommandCenterActionPrimary"] = new SolidColorBrush(primary);
        Resources["CommandCenterActionSecondary"] = new SolidColorBrush(secondary);
        Resources["CommandCenterActionSecondaryHover"] = new SolidColorBrush(secondaryHover);
        Resources["CommandCenterActionIconBg"] = new SolidColorBrush(iconBg);
    }

    private static Color ResolveActionButtonPrimaryColor(TopLevel? topLevel, ApplicationSettings settings)
    {
        var source = settings.CommandCenterActionButtonColorSource ?? "App Accent";

        if (source == "Custom" && !string.IsNullOrWhiteSpace(settings.CommandCenterActionButtonCustomColor) &&
            Color.TryParse(settings.CommandCenterActionButtonCustomColor, out var customColor))
        {
            return customColor;
        }

        if (source == "System Accent" && topLevel != null)
        {
            try
            {
                var colorValues = topLevel.PlatformSettings?.GetColorValues();
                if (colorValues is { } cv)
                {
                    return cv.AccentColor1;
                }
            }
            catch
            {
                // Fall through to app accent
            }
        }

        // App Accent (default)
        if (!string.IsNullOrWhiteSpace(settings.AccentHex) && Color.TryParse(settings.AccentHex, out var accentColor))
        {
            return accentColor;
        }

        return Color.Parse("#3A8DFF");
    }

    private bool IsLocked => DataContext is CommandCenterWindowViewModel vm && vm.IsLocked;

    public void SnapToCurrentLayout()
    {
        Dispatcher.UIThread.Post(() => SnapToNearestLayout(force: true));
    }

    private void ScheduleStateSave()
    {
        _stateSaveTimer?.Stop();
        _stateSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _stateSaveTimer.Tick += async (_, _) =>
        {
            _stateSaveTimer?.Stop();
            _stateSaveTimer = null;
            await AppServices.WidgetWindowManager.SaveCommandCenterStateAsync(this).ConfigureAwait(false);
        };
        _stateSaveTimer.Start();
    }

    private void Chrome_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsLocked)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void Button_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void ResizeHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsLocked)
        {
            return;
        }

        if (sender is not Border border || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var resizeMode = border.Tag switch
        {
            "Right" => ResizeDragMode.Right,
            "Bottom" => ResizeDragMode.Bottom,
            "BottomRight" => ResizeDragMode.BottomRight,
            _ => ResizeDragMode.None
        };

        if (resizeMode == ResizeDragMode.None)
        {
            return;
        }

        _resizeDragMode = resizeMode;
        _activeResizePointer = e.Pointer;
        e.Pointer.Capture(this);
        UpdateResizeFromPointer(e);
        e.Handled = true;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ScheduleStateSave();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_activeResizePointer == null || !ReferenceEquals(e.Pointer, _activeResizePointer))
        {
            return;
        }

        UpdateResizeFromPointer(e);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_activeResizePointer == null || !ReferenceEquals(e.Pointer, _activeResizePointer))
        {
            return;
        }

        EndResizeInteraction();
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        EndResizeInteraction();
        _stateSaveTimer?.Stop();
        _stateSaveTimer = null;

        DetachViewModel();

        if (DataContext is CommandCenterWindowViewModel vm)
        {
            vm.Dispose();
        }

        AppServices.SettingsService.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }

    private void AttachViewModel(CommandCenterWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_boundViewModel, viewModel))
        {
            return;
        }

        DetachViewModel();
        _boundViewModel = viewModel;
        if (_boundViewModel != null)
        {
            _boundViewModel.Actions.CollectionChanged += OnActionsCollectionChanged;
        }
    }

    private void DetachViewModel()
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.Actions.CollectionChanged -= OnActionsCollectionChanged;
            _boundViewModel = null;
        }
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => SnapToNearestLayout(force: true));
    }

    private void UpdateResizeFromPointer(PointerEventArgs e)
    {
        if (_resizeDragMode == ResizeDragMode.None)
        {
            return;
        }

        var actionCount = GetActualActionCount();
        if (actionCount <= 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        var targetWidth = Math.Max(GetHorizontalChrome() + ActionCellSize, point.X);
        var targetHeight = Math.Max(GetVerticalChrome() + ActionCellSize, point.Y);

        var layout = _resizeDragMode switch
        {
            ResizeDragMode.Right => ComputeLayoutFromWidth(actionCount, targetWidth),
            ResizeDragMode.Bottom =>
                GetLayoutForColumns(actionCount, ComputeColumnsFromRows(actionCount, ComputeLayoutFromHeight(actionCount, targetHeight).Rows)),
            ResizeDragMode.BottomRight => GetBestLayout(actionCount, targetWidth, targetHeight, ResizeDragMode.BottomRight),
            _ => GetBestLayout(actionCount, Width, Height, ResizeDragMode.BottomRight)
        };

        ApplySnappedSize(layout.Columns);
    }

    private void EndResizeInteraction()
    {
        _activeResizePointer?.Capture(null);
        _activeResizePointer = null;
        _resizeDragMode = ResizeDragMode.None;
    }

    private void SnapToNearestLayout(bool force)
    {
        var actionCount = GetActualActionCount();
        if (actionCount <= 0)
        {
            return;
        }

        UpdateLayoutBounds();

        if (!force && _hasInitializedLayout)
        {
            return;
        }

        var targetWidth = Width > 0 ? Width : MaxWidth;
        var targetHeight = Height > 0 ? Height : MinHeight;
        var layout = GetBestLayout(actionCount, targetWidth, targetHeight, ResizeDragMode.BottomRight);
        ApplySnappedSize(layout.Columns);
        _hasInitializedLayout = true;
    }

    private void UpdateLayoutBounds()
    {
        var actionCount = Math.Max(GetActualActionCount(), 1);
        var narrowest = GetLayoutForColumns(actionCount, 1);
        var widest = GetLayoutForColumns(actionCount, actionCount);

        MinWidth = narrowest.Width;
        MaxWidth = widest.Width;
        MinHeight = widest.Height;
        MaxHeight = narrowest.Height;
    }

    private void ApplySnappedSize(int columns)
    {
        var actionCount = Math.Max(GetActualActionCount(), 1);
        var layout = GetLayoutForColumns(actionCount, columns);

        Width = layout.Width;
        Height = layout.Height;
    }

    private (int Columns, int Rows, double Width, double Height) ComputeLayoutFromWidth(int actionCount, double width)
    {
        return GetBestLayout(actionCount, width, Height, ResizeDragMode.Right);
    }

    private (int Columns, int Rows, double Width, double Height) ComputeLayoutFromHeight(int actionCount, double height)
    {
        return GetBestLayout(actionCount, Width, height, ResizeDragMode.Bottom);
    }

    private (int Columns, int Rows, double Width, double Height) GetBestLayout(
        int actionCount,
        double targetWidth,
        double targetHeight,
        ResizeDragMode resizeMode)
    {
        var clampedActionCount = Math.Max(actionCount, 1);
        var bestLayout = GetLayoutForColumns(clampedActionCount, 1);
        var bestScore = double.MaxValue;

        for (var columns = 1; columns <= clampedActionCount; columns++)
        {
            var layout = GetLayoutForColumns(clampedActionCount, columns);
            var score = resizeMode switch
            {
                ResizeDragMode.Right => Math.Abs(layout.Width - targetWidth),
                ResizeDragMode.Bottom => Math.Abs(layout.Height - targetHeight),
                ResizeDragMode.BottomRight => Math.Abs(layout.Width - targetWidth) + Math.Abs(layout.Height - targetHeight),
                _ => Math.Abs(layout.Width - targetWidth) + Math.Abs(layout.Height - targetHeight)
            };

            if (score < bestScore)
            {
                bestScore = score;
                bestLayout = layout;
            }
        }

        return bestLayout;
    }

    private (int Columns, int Rows, double Width, double Height) GetLayoutForColumns(int actionCount, int columns)
    {
        var clampedActionCount = Math.Max(actionCount, 1);
        var clampedColumns = Math.Clamp(columns, 1, clampedActionCount);
        var rows = (int)Math.Ceiling(clampedActionCount / (double)clampedColumns);

        var cellWidth = ActionCellSize + _actionSpacingHorizontal;
        var cellHeight = ActionCellSize + _actionSpacingVertical;

        return (
            clampedColumns,
            rows,
            GetHorizontalChrome() + (clampedColumns * cellWidth),
            GetVerticalChrome() + (rows * cellHeight));
    }

    private int ComputeColumnsFromRows(int actionCount, int rows)
    {
        var clampedRows = Math.Max(rows, 1);
        return (int)Math.Ceiling(actionCount / (double)clampedRows);
    }

    private int GetActualActionCount()
    {
        return _boundViewModel?.Actions.Count ?? (DataContext as CommandCenterWindowViewModel)?.Actions.Count ?? 0;
    }

    private double GetHorizontalChrome()
    {
        var borderThickness = _outerBorder?.BorderThickness ?? default;
        var padding = _outerBorder?.Padding ?? default;
        return padding.Left + padding.Right + borderThickness.Left + borderThickness.Right;
    }

    private double GetVerticalChrome()
    {
        var borderThickness = _outerBorder?.BorderThickness ?? default;
        var padding = _outerBorder?.Padding ?? default;
        return padding.Top + padding.Bottom + borderThickness.Top + borderThickness.Bottom;
    }
}
