using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Boxes.App.Views.Controls;

public class AnimatedCollapsePresenter : ContentControl
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(45);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AnimatedCollapsePresenter, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<AnimatedCollapsePresenter, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(380));

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    private ContentPresenter? _presenter;
    private CancellationTokenSource? _animationCts;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        ApplyInstantState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty)
        {
            if (change.NewValue is bool expanded)
            {
                _ = AnimateToStateAsync(expanded);
            }
        }
    }

    private void ApplyInstantState()
    {
        if (_presenter is null)
        {
            return;
        }

        if (IsExpanded)
        {
            _presenter.Opacity = 1;
            _presenter.Height = double.NaN; // Auto
            _presenter.IsHitTestVisible = true;
        }
        else
        {
            _presenter.Opacity = 0;
            _presenter.Height = 0;
            _presenter.IsHitTestVisible = false;
        }
    }

    private async Task AnimateToStateAsync(bool expanded)
    {
        if (_presenter is null)
        {
            return;
        }

        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        try
        {
            // Small debounce/hysteresis to avoid rapid toggle jitter and end-of-animation flicker.
            await Task.Delay(DebounceDelay, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (IsExpanded != expanded)
            {
                return;
            }

            // Ensure we're on the UI thread and have a render pass so width is known for accurate height measurement.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            if (expanded)
            {
                await AnimateExpandAsync(token);
            }
            else
            {
                await AnimateCollapseAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Swallow: a newer animation request superseded this one.
        }
    }

    private async Task AnimateExpandAsync(CancellationToken token)
    {
        if (_presenter is null)
        {
            return;
        }

        token.ThrowIfCancellationRequested();
        _presenter.IsHitTestVisible = true;

        // Measure desired height at the current width.
        var width = _presenter.Bounds.Width;
        if (width <= 0)
        {
            width = Bounds.Width > 0 ? Bounds.Width : 800;
        }

        _presenter.Height = double.NaN; // Auto for measurement
        _presenter.Opacity = 0;
        _presenter.Measure(new Size(width, double.PositiveInfinity));
        var targetHeight = _presenter.DesiredSize.Height;
        if (double.IsNaN(targetHeight) || targetHeight < 0)
        {
            targetHeight = 0;
        }

        // Start collapsed.
        _presenter.Height = 0;

        var animation = new Animation
        {
            Duration = AnimationDuration,
            Easing = new CubicEaseOut()
        };

        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(0d),
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 0d),
                new Setter(Visual.OpacityProperty, 0d)
            }
        });

        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(1d),
            Setters =
            {
                new Setter(Layoutable.HeightProperty, targetHeight),
                new Setter(Visual.OpacityProperty, 1d)
            }
        });

        await animation.RunAsync(_presenter, token);

        // Keep a stable height to avoid a one-frame "collapse" flash after switching back to auto.
        _presenter.Height = targetHeight;
        _presenter.Opacity = 1;
    }

    private async Task AnimateCollapseAsync(CancellationToken token)
    {
        if (_presenter is null)
        {
            return;
        }

        token.ThrowIfCancellationRequested();
        var fromHeight = _presenter.Bounds.Height;
        if (fromHeight <= 0 && !double.IsNaN(_presenter.Height))
        {
            fromHeight = _presenter.Height;
        }

        if (double.IsNaN(fromHeight) || fromHeight < 0)
        {
            fromHeight = 0;
        }

        // Fix the height so it can animate down.
        _presenter.Height = fromHeight;
        _presenter.Opacity = 1;

        var animation = new Animation
        {
            Duration = AnimationDuration,
            Easing = new CubicEaseIn()
        };

        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(0d),
            Setters =
            {
                new Setter(Layoutable.HeightProperty, fromHeight),
                new Setter(Visual.OpacityProperty, 1d)
            }
        });

        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(1d),
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 0d),
                new Setter(Visual.OpacityProperty, 0d)
            }
        });

        await animation.RunAsync(_presenter, token);

        _presenter.Height = 0;
        _presenter.Opacity = 0;
        _presenter.IsHitTestVisible = false;
    }
}


