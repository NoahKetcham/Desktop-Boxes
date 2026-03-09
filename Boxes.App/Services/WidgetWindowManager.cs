using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Boxes.App.Extensions;
using Boxes.App.Models;
using Boxes.App.ViewModels.Widgets;
using Boxes.App.Views.Widgets;

namespace Boxes.App.Services;

public class WidgetWindowManager
{
    private static string PreviewKey(Guid id) => $"notepadPreview-{id:N}";
    private static string EditorKey(Guid id) => $"notepadEditor-{id:N}";

    private readonly Dictionary<Guid, NotepadPreviewWindow> _previewWindows = new();
    private readonly Dictionary<Guid, NotepadEditorWindow> _editorWindows = new();
    private readonly Dictionary<Guid, NotepadWindowViewModel> _viewModels = new();

    private NoteHubPreviewWindow? _noteHubPreview;
    private NoteHubEditorWindow? _noteHubEditor;
    private NoteHubWindowViewModel? _noteHubViewModel;
    private CommandCenterWindow? _commandCenterWindow;
    private CommandCenterWindowViewModel? _commandCenterViewModel;

    public async Task ShowNotepadAsync(Notepad notepad)
    {
        var id = notepad.Id;
        if (_previewWindows.TryGetValue(id, out var existing))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                existing.Show();
                existing.Activate();
            });
            return;
        }

        await AppServices.WidgetStateService.MigrateLegacyNotepadStateAsync(id).ConfigureAwait(false);
        var state = await AppServices.WidgetStateService.GetAsync(PreviewKey(id)).ConfigureAwait(false);
        if (state is null)
        {
            state = await AppServices.WidgetStateService.GetAsync("notepadPreview").ConfigureAwait(false);
        }
        if (state is null)
        {
            state = await AppServices.WidgetStateService.GetAsync("notepad").ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = new NotepadWindowViewModel(notepad.Id, notepad.Name);
            _viewModels[id] = vm;

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
            closeHandler = (_, _) => ClosePreviewAndEditor(window, vm, id);
            vm.RequestClose += closeHandler;

            vm.RequestOpenEditor += OnRequestOpenEditor;
            vm.RequestCloseEditor += OnRequestCloseEditor;

            window.Closed += async (_, _) =>
            {
                vm.RequestClose -= closeHandler;
                vm.RequestOpenEditor -= OnRequestOpenEditor;
                vm.RequestCloseEditor -= OnRequestCloseEditor;
                _viewModels.Remove(id);
                vm.SaveImmediately();
                if (_editorWindows.TryGetValue(id, out var editor))
                {
                    editor.Close();
                    _editorWindows.Remove(id);
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
                await AppServices.WidgetStateService.SaveAsync(PreviewKey(id), stateToSave).ConfigureAwait(false);
                _previewWindows.Remove(id);
            };

            _previewWindows[id] = window;
            window.Show();
            window.Activate();
        });
    }

    public async Task CloseNotepadAsync(Guid id)
    {
        if (!_previewWindows.TryGetValue(id, out var preview))
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            preview.Close();
        });
    }

    private void ClosePreviewAndEditor(NotepadPreviewWindow preview, NotepadWindowViewModel vm, Guid id)
    {
        preview.Close();
        if (_editorWindows.TryGetValue(id, out var editor))
        {
            editor.Close();
        }
    }

    private void OnRequestOpenEditor(object? sender, EventArgs e)
    {
        if (sender is NotepadWindowViewModel vm)
        {
            _ = ShowNotepadEditorAsync(vm.NotepadId);
        }
    }

    private void OnRequestCloseEditor(object? sender, EventArgs e)
    {
        if (sender is NotepadWindowViewModel vm)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_editorWindows.TryGetValue(vm.NotepadId, out var editor))
                {
                    editor.Close();
                }
            });
        }
    }

    private async Task ShowNotepadEditorAsync(Guid notepadId)
    {
        if (!_viewModels.TryGetValue(notepadId, out var vm))
            return;

        if (_editorWindows.TryGetValue(notepadId, out var existing))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                existing.Show();
                existing.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync(EditorKey(notepadId)).ConfigureAwait(false);
        if (state is null)
        {
            state = await AppServices.WidgetStateService.GetAsync("notepadEditor").ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!_viewModels.TryGetValue(notepadId, out var vm2))
                return;

            const int editorHeight = 380;
            const int editorWidth = 600;

            var window = new NotepadEditorWindow
            {
                DataContext = vm2,
                Width = state is not null && state.Width > 0 ? state.Width : editorWidth,
                Height = state is not null && state.Height > 0 ? state.Height : editorHeight
            };

            var working = window.Screens?.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
            const int margin = 16;
            var x = working.X + (working.Width - (int)window.Width) / 2;
            var y = working.Bottom - (int)window.Height - margin;
            window.Position = new PixelPoint(Math.Max(working.X, x), Math.Max(working.Y, y));

            window.Closed += async (_, _) =>
            {
                vm2.SaveImmediately();
                var w = window;
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y
                };
                await AppServices.WidgetStateService.SaveAsync(EditorKey(notepadId), stateToSave).ConfigureAwait(false);
                _editorWindows.Remove(notepadId);
            };

            _editorWindows[notepadId] = window;
            window.Show();
            window.Activate();
        });
    }

    public async Task ShowNoteHubAsync(string? initialPath = null)
    {
        if (_noteHubPreview != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _noteHubPreview.Show();
                _noteHubPreview.Activate();
                if (!string.IsNullOrEmpty(initialPath))
                {
                    _noteHubViewModel!.CurrentRelativePath = initialPath;
                }
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync("notehubPreview").ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _noteHubViewModel = new NoteHubWindowViewModel(initialPath);

            var window = new NoteHubPreviewWindow
            {
                DataContext = _noteHubViewModel
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
            }

            _noteHubViewModel.RequestClose += OnNoteHubRequestClose;
            _noteHubViewModel.RequestOpenEditor += OnNoteHubRequestOpenEditor;
            _noteHubViewModel.RequestCloseEditor += OnNoteHubRequestCloseEditor;

            window.Closed += async (_, _) =>
            {
                _noteHubViewModel.RequestClose -= OnNoteHubRequestClose;
                _noteHubViewModel.RequestOpenEditor -= OnNoteHubRequestOpenEditor;
                _noteHubViewModel.RequestCloseEditor -= OnNoteHubRequestCloseEditor;
                _noteHubViewModel.SaveImmediately();
                if (_noteHubEditor != null)
                {
                    _noteHubEditor.Close();
                    _noteHubEditor = null;
                }
                var w = window;
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y
                };
                await AppServices.WidgetStateService.SaveAsync("notehubPreview", stateToSave).ConfigureAwait(false);
                _noteHubPreview = null;
                _noteHubViewModel = null;
            };

            _noteHubPreview = window;
            window.Show();
            window.Activate();
        });
    }

    public async Task ShowCommandCenterAsync()
    {
        if (_commandCenterWindow != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _commandCenterWindow.Show();
                _commandCenterWindow.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync("commandCenter").ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var viewModel = new CommandCenterWindowViewModel();
            _commandCenterViewModel = viewModel;
            var window = new CommandCenterWindow
            {
                DataContext = viewModel
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
            }

            window.Closed += async (_, _) =>
            {
                await SaveCommandCenterStateAsync(window).ConfigureAwait(false);
                _commandCenterWindow = null;
                _commandCenterViewModel = null;
            };

            _commandCenterWindow = window;
            window.Show();
            window.Activate();
            window.SnapToCurrentLayout();
        });
    }

    public async Task CloseCommandCenterAsync()
    {
        if (_commandCenterWindow == null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _commandCenterWindow?.Close();
        });
    }

    public async Task SaveCommandCenterStateAsync(CommandCenterWindow window)
    {
        var state = await Dispatcher.UIThread.InvokeAsync(() => new WidgetStateData
        {
            Width = window.Width,
            Height = window.Height,
            X = window.Position.X,
            Y = window.Position.Y
        });

        await AppServices.WidgetStateService.SaveAsync("commandCenter", state).ConfigureAwait(false);
    }

    public void SetCommandCenterTopMost(bool enable)
    {
        if (_commandCenterWindow == null)
        {
            return;
        }

        _commandCenterWindow.SetTopMost(enable);
    }

    public void RestoreCommandCenterBelowApps()
    {
        if (_commandCenterWindow == null)
        {
            return;
        }

        _commandCenterWindow.SetTopMost(false);
        _commandCenterWindow.SetAlwaysBelowApps();
    }

    public bool OwnsCommandCenterWindow(IntPtr handle)
    {
        return _commandCenterWindow?.GetWindowHandle() == handle;
    }

    private void OnNoteHubRequestClose(object? sender, EventArgs e)
    {
        _noteHubPreview?.Close();
    }

    private void OnNoteHubRequestOpenEditor(object? sender, EventArgs e)
    {
        _ = ShowNoteHubEditorAsync();
    }

    private void OnNoteHubRequestCloseEditor(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _noteHubEditor?.Close();
        });
    }

    private async Task ShowNoteHubEditorAsync()
    {
        if (_noteHubViewModel == null) return;

        if (_noteHubEditor != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _noteHubEditor.Show();
                _noteHubEditor.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync("notehubEditor").ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_noteHubViewModel == null) return;

            const int editorHeight = 380;
            const int editorWidth = 600;

            var window = new NoteHubEditorWindow
            {
                DataContext = _noteHubViewModel,
                Width = state is not null && state.Width > 0 ? state.Width : editorWidth,
                Height = state is not null && state.Height > 0 ? state.Height : editorHeight
            };

            var working = window.Screens?.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
            const int margin = 16;
            var x = working.X + (working.Width - (int)window.Width) / 2;
            var y = working.Bottom - (int)window.Height - margin;
            window.Position = new PixelPoint(Math.Max(working.X, x), Math.Max(working.Y, y));

            window.Closed += async (_, _) =>
            {
                _noteHubViewModel.SaveImmediately();
                var w = window;
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y
                };
                await AppServices.WidgetStateService.SaveAsync("notehubEditor", stateToSave).ConfigureAwait(false);
                _noteHubEditor = null;
            };

            _noteHubEditor = window;
            window.Show();
            window.Activate();
        });
    }
}
