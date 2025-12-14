using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Boxes.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Boxes.App.ViewModels;

public partial class OverviewPageViewModel : ViewModelBase
{
    public string Headline => "Your workspace at a glance";
    public string Subtitle => "Track how your boxes are organizing the desktop";

    [ObservableProperty]
    private int activeBoxCount;

    [ObservableProperty]
    private int totalItemCount;

    [ObservableProperty]
    private int scannedFilesCount;

    [ObservableProperty]
    private bool isDesktopClean;

    [ObservableProperty]
    private int desktopBuildsCount;

    public ObservableCollection<RecentActivityItem> RecentActivity { get; } = new();

    public OverviewPageViewModel()
    {
        _ = RefreshDataAsync();
        AppServices.BoxUpdated += (_, _) => _ = RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        var boxes = await AppServices.BoxService.GetBoxesAsync();
        var scannedFiles = await AppServices.ScannedFileService.GetScannedFilesAsync();
        var builds = await AppServices.DesktopBuildService.GetAllAsync();
        var isClean = await AppServices.DesktopCleanupService.IsDesktopCleanAsync();

        ActiveBoxCount = boxes.Count;
        TotalItemCount = boxes.Sum(b => b.ShortcutIds.Count);
        ScannedFilesCount = scannedFiles.Count;
        IsDesktopClean = isClean;
        DesktopBuildsCount = builds.Count;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            RecentActivity.Clear();
            
            // Add activity items based on current state
            if (boxes.Count > 0)
            {
                RecentActivity.Add(new RecentActivityItem($"📦 {boxes.Count} box{(boxes.Count == 1 ? "" : "es")} active", "Managing your desktop shortcuts"));
            }
            
            if (scannedFiles.Count > 0)
            {
                RecentActivity.Add(new RecentActivityItem($"📂 {scannedFiles.Count} files discovered", "From your desktop and folders"));
            }

            if (isClean)
            {
                RecentActivity.Add(new RecentActivityItem("✨ Desktop is clean", "Files are archived and accessible through boxes"));
            }

            if (builds.Count > 0)
            {
                RecentActivity.Add(new RecentActivityItem($"💾 {builds.Count} saved build{(builds.Count == 1 ? "" : "s")}", "Ready to restore anytime"));
            }

            if (RecentActivity.Count == 0)
            {
                RecentActivity.Add(new RecentActivityItem("👋 Welcome to Boxes!", "Create your first box to get started"));
            }
        });
    }
}

public record RecentActivityItem(string Title, string Description);

