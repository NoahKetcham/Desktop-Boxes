using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.ViewModels.Widgets;
using Boxes.App.Views.Widgets;

namespace Boxes.App.Services;

public class WidgetWindowManager
{
    private const string NotepadPreviewKey = "notepadPreview";
    private const string NotepadEditorKey = "notepadEditor";

    private readonly Dictionary<string, NotepadPreviewWindow> _previewWindows = new();
    private readonly Dictionary<string, NotepadEditorWindow> _editorWindows = new();
    private NotepadWindowViewModel? _notepadVm;

    public async Task ShowNotepadAsync()
    {
        if (_previewWindows.TryGetValue(NotepadPreviewKey, out var existing))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                existing.Show();
                existing.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync(NotepadPreviewKey).ConfigureAwait(false);
        if (state is null)
        {
            state = await AppServices.WidgetStateService.GetAsync("notepad").ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = new NotepadWindowViewModel();
            _notepadVm = vm;

            var window = new NotepadPreviewWindow
            {
                DataContext = vm
            };

            if (state is not null)
            {
                if (state.Width > 0 && state.Height > 0)
                {
                    window.Width = state.Width;
                    window.Height = state.Height;
                }
                if (!double.IsNaN(state.X) && !double.IsNaN(state.Y))
                {
                    window.Position = new PixelPoint((int)state.X, (int)state.Y);
                }
                if (state.IsCollapsed && state.ExpandedHeight > 0)
                {
                    window.SetRestoreState(state.IsCollapsed, state.ExpandedHeight);
                }
            }

            EventHandler? closeHandler = null;
            closeHandler = (_, _) => ClosePreviewAndEditor(window, vm);
            vm.RequestClose += closeHandler;

            vm.RequestOpenEditor += OnRequestOpenEditor;
            vm.RequestCloseEditor += OnRequestCloseEditor;

            window.Closed += async (_, _) =>
            {
                vm.RequestClose -= closeHandler;
                vm.RequestOpenEditor -= OnRequestOpenEditor;
                vm.RequestCloseEditor -= OnRequestCloseEditor;
                if (_notepadVm == vm)
                {
                    _notepadVm = null;
                }
                vm.SaveImmediately();
                if (_editorWindows.TryGetValue(NotepadEditorKey, out var editor))
                {
                    editor.Close();
                    _editorWindows.Remove(NotepadEditorKey);
                }
                var w = window;
                var (isCollapsed, expandedHeight) = w.GetCollapseState();
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y,
                    IsCollapsed = isCollapsed,
                    ExpandedHeight = expandedHeight
                };
                await AppServices.WidgetStateService.SaveAsync(NotepadPreviewKey, stateToSave).ConfigureAwait(false);
                _previewWindows.Remove(NotepadPreviewKey);
            };

            _previewWindows[NotepadPreviewKey] = window;
            window.Show();
            window.Activate();
        });
    }

    private void ClosePreviewAndEditor(NotepadPreviewWindow preview, NotepadWindowViewModel vm)
    {
        preview.Close();
        if (_editorWindows.TryGetValue(NotepadEditorKey, out var editor))
        {
            editor.Close();
        }
    }

    private void OnRequestOpenEditor(object? sender, EventArgs e)
    {
        _ = ShowNotepadEditorAsync();
    }

    private void OnRequestCloseEditor(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_editorWindows.TryGetValue(NotepadEditorKey, out var editor))
            {
                editor.Close();
            }
        });
    }

    private async Task ShowNotepadEditorAsync()
    {
        if (_notepadVm is null)
            return;

        if (_editorWindows.TryGetValue(NotepadEditorKey, out var existing))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                existing.Show();
                existing.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync(NotepadEditorKey).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_notepadVm is null)
                return;

            var window = new NotepadEditorWindow
            {
                DataContext = _notepadVm,
                Width = 400,
                Height = 300
            };

            if (state is not null && state.Width > 0 && state.Height > 0 && !double.IsNaN(state.X) && !double.IsNaN(state.Y))
            {
                window.Width = state.Width;
                window.Height = state.Height;
                window.Position = new PixelPoint((int)state.X, (int)state.Y);
            }
            else
            {
                var working = window.Screens?.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
                const int margin = 16;
                var x = working.Right - (int)window.Width - margin;
                var y = working.Bottom - (int)window.Height - margin;
                window.Position = new PixelPoint(Math.Max(working.X, x), Math.Max(working.Y, y));
            }

            window.Closed += async (_, _) =>
            {
                _notepadVm.SaveImmediately();
                var w = window;
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y
                };
                await AppServices.WidgetStateService.SaveAsync(NotepadEditorKey, stateToSave).ConfigureAwait(false);
                _editorWindows.Remove(NotepadEditorKey);
            };

            _editorWindows[NotepadEditorKey] = window;
            window.Show();
            window.Activate();
        });
    }
}
