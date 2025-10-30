using System.Threading.Tasks;

namespace Boxes.App.Services;

public class DataMaintenanceService
{
    public DataMaintenanceService(string rootDirectory)
    {
        // No file purge yet; services handle their own cleaners.
    }

    public async Task ResetAllAsync()
    {
        await AppServices.BoxWindowManager.CloseAllWithoutSaveAsync().ConfigureAwait(false);
        await AppServices.BoxService.ResetAsync().ConfigureAwait(false);
        await AppServices.ScannedFileService.ResetAsync().ConfigureAwait(false);
        await AppServices.SettingsService.ResetAsync().ConfigureAwait(false);
        await AppServices.DesktopCleanupService.ResetAsync().ConfigureAwait(false);
    }
}

