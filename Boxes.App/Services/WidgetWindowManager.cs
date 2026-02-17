using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Boxes.App.Models;
using Boxes.App.ViewModels.Widgets;
using Boxes.App.Views.Widgets;

namespace Boxes.App.Services;

public class WidgetWindowManager
{
    private const string NotepadWidgetId = "notepad";

    private readonly Dictionary<string, NotepadWindow> _notepadWindows = new();

    public async Task ShowNotepadAsync()
    {
        if (_notepadWindows.TryGetValue(NotepadWidgetId, out var existing))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                existing.Show();
                existing.Activate();
            });
            return;
        }

        var state = await AppServices.WidgetStateService.GetAsync(NotepadWidgetId).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = new NotepadWindowViewModel();
            var window = new NotepadWindow
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
                    window.Position = new Avalonia.PixelPoint((int)state.X, (int)state.Y);
                }
            }

            EventHandler? closeHandler = null;
            closeHandler = (_, _) => window.Close();
            vm.RequestClose += closeHandler;

            window.Closed += async (_, _) =>
            {
                vm.RequestClose -= closeHandler;
                if (vm is NotepadWindowViewModel nvm)
                {
                    nvm.SaveImmediately();
                }
                var w = window;
                var stateToSave = new WidgetStateData
                {
                    Width = w.Width,
                    Height = w.Height,
                    X = w.Position.X,
                    Y = w.Position.Y
                };
                await AppServices.WidgetStateService.SaveAsync(NotepadWidgetId, stateToSave).ConfigureAwait(false);
                _notepadWindows.Remove(NotepadWidgetId);
            };

            _notepadWindows[NotepadWidgetId] = window;
            window.Show();
            window.Activate();
        });
    }
}
