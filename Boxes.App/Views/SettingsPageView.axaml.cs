using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Boxes.App.ViewModels;

namespace Boxes.App.Views;

public partial class SettingsPageView : UserControl
{
    private bool _isScrollingProgrammatically;
    private ScrollViewer? _scrollViewer;
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, Border> _sections = new();

    public SettingsPageView()
    {
        InitializeComponent();
        this.Loaded += OnLoaded;
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SettingsPageViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsPageViewModel.SelectedCategory))
        {
            if (sender is SettingsPageViewModel vm && vm.SelectedCategory is { } category)
            {
                ScrollToSection(category);
                UpdateActiveNavButton(category);
            }
        }
    }

    private void ScrollToSection(string category)
    {
        if (_scrollViewer == null) return;
        if (!_sections.TryGetValue(category, out var targetSection)) return;

        _isScrollingProgrammatically = true;

        var transform = targetSection.TransformToVisual(_scrollViewer);
        if (transform == null)
        {
            _isScrollingProgrammatically = false;
            return;
        }

        var position = transform.Value.Transform(new Point(0, 0));
        var currentOffset = _scrollViewer.Offset;
        var targetOffset = currentOffset.Y + position.Y - 20;

        _scrollViewer.Offset = new Vector(currentOffset.X, Math.Max(0, targetOffset));

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _isScrollingProgrammatically = false;
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UpdateActiveNavButton(string activeCategory)
    {
        foreach (var (category, button) in _navButtons)
        {
            if (category == activeCategory)
            {
                button.Classes.Add("active");
            }
            else
            {
                button.Classes.Remove("active");
            }
        }
    }

    private string? GetVisibleSection()
    {
        if (_scrollViewer == null) return null;

        var scrollOffset = _scrollViewer.Offset.Y;
        var viewportHeight = _scrollViewer.Viewport.Height;
        var viewportMiddle = scrollOffset + (viewportHeight / 3);

        string? visibleSection = null;
        double closestDistance = double.MaxValue;

        foreach (var (category, section) in _sections)
        {
            var transform = section.TransformToVisual(_scrollViewer);
            if (transform == null) continue;

            var position = transform.Value.Transform(new Point(0, 0));
            var sectionTop = _scrollViewer.Offset.Y + position.Y;
            var distance = Math.Abs(sectionTop - viewportMiddle);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                visibleSection = category;
            }
        }

        return visibleSection;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isScrollingProgrammatically) return;

        var visibleSection = GetVisibleSection();
        if (visibleSection != null)
        {
            UpdateActiveNavButton(visibleSection);
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _scrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer");
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        _navButtons["appearance"] = this.FindControl<Button>("NavAppearance")!;
        _navButtons["behavior"] = this.FindControl<Button>("NavBehavior")!;
        _navButtons["customization"] = this.FindControl<Button>("NavCustomization")!;
        _navButtons["commandcenter"] = this.FindControl<Button>("NavCommandCenter")!;
        _navButtons["integrations"] = this.FindControl<Button>("NavIntegrations")!;
        _navButtons["notehub"] = this.FindControl<Button>("NavNoteHub")!;
        _navButtons["developer"] = this.FindControl<Button>("NavDeveloper")!;

        _sections["appearance"] = this.FindControl<Border>("AppearanceSection")!;
        _sections["behavior"] = this.FindControl<Border>("BehaviorSection")!;
        _sections["customization"] = this.FindControl<Border>("CustomizationSection")!;
        _sections["commandcenter"] = this.FindControl<Border>("CommandCenterSection")!;
        _sections["integrations"] = this.FindControl<Border>("IntegrationsSection")!;
        _sections["notehub"] = this.FindControl<Border>("NoteHubSection")!;
        _sections["developer"] = this.FindControl<Border>("DeveloperSection")!;

        UpdateActiveNavButton("appearance");
    }
}

