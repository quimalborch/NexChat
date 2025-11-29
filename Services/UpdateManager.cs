using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;
using Serilog;

namespace NexChat.Services
{
    public class UpdateManager
    {
        private readonly Velopack.UpdateManager updateManager;
        private UpdateInfo? updateInfo;

        public UpdateManager()
        {
            try
            {
                Log.Information("Initializing UpdateManager");
                updateManager = new Velopack.UpdateManager(new GithubSource("https://github.com/quimalborch/NexChat", null, false));
                Log.Information("UpdateManager initialized successfully with GitHub source: https://github.com/quimalborch/NexChat");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize UpdateManager");
                throw;
            }
        }

        public async Task<returnMessageUpdateInfo> CheckActualizacionDisponible()
        {
            try
            {
                Log.Information("Checking for available updates...");
                
#if DEBUG
                Log.Warning("Running in DEBUG mode - returning mock update info");
                return new returnMessageUpdateInfo(true, "DEV");
#endif

                if (updateManager is null)
                {
                    Log.Error("UpdateManager is null - cannot check for updates");
                    throw new Exception("update manager no instanciado");
                }

                Log.Debug("Calling CheckForUpdatesAsync...");
                updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (updateInfo is null)
                {
                    Log.Information("No updates available - updateInfo is null");
                    return new returnMessageUpdateInfo(false);
                }

                var currentVersion = updateManager.CurrentVersion?.ToString() ?? "Unknown";
                var targetVersion = updateInfo.TargetFullRelease.Version.ToString();
                
                Log.Information("Update found! Current: {CurrentVersion}, Target: {TargetVersion}", 
                    currentVersion, targetVersion);
                Log.Debug("Update details - IsDowngrade: {IsDowngrade}", updateInfo.IsDowngrade);

                return new returnMessageUpdateInfo(true, targetVersion);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates. Exception type: {ExceptionType}", ex.GetType().Name);
                return new returnMessageUpdateInfo($"Error: {ex.Message}");
            }
        }

        public async Task ForceUpdate()
        {
            try
            {
                Log.Information("=== Starting Force Update Process ===");
                
                if (updateManager is null)
                {
                    Log.Error("Cannot apply updates: UpdateManager is null");
                    Debug.WriteLine("Cannot apply updates: UpdateManager is null");
                    return;
                }

#if !DEBUG
                if (updateInfo is null)
                {
                    Log.Error("Cannot apply updates: updateInfo is null. Must call CheckActualizacionDisponible first");
                    Debug.WriteLine("Cannot apply updates: update is null");
                    return;
                }
                
                Log.Information("Downloading update: {Version}", updateInfo.TargetFullRelease.Version);
                Log.Debug("Notes Markdown: {Url}", updateInfo.TargetFullRelease.NotesMarkdown);
                Log.Debug("Package size: {Size} bytes", updateInfo.TargetFullRelease.Size);
#else
                Log.Warning("Running in DEBUG mode - skipping actual update download");
#endif

                await updateManager.DownloadUpdatesAsync(updateInfo);
                Log.Information("Update downloaded successfully");
                
                Log.Information("Applying updates and restarting application...");
                updateManager.ApplyUpdatesAndRestart(updateInfo);
                
                // Este código probablemente nunca se ejecute porque la app se reinicia
                Log.Information("ApplyUpdatesAndRestart called - application should restart now");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during update process. Exception type: {ExceptionType}, Message: {Message}", 
                    ex.GetType().Name, ex.Message);
                Log.Debug("Stack trace: {StackTrace}", ex.StackTrace);
                Debug.WriteLine($"Error during update: {ex.Message}");
            }
        }
    }

    public class returnMessageUpdateInfo
    {
        public bool updateAvaliable = false;
        public string? version;
        public string? messageError;

        public returnMessageUpdateInfo(bool udpateAvaliable)
        {
            this.updateAvaliable = udpateAvaliable;
        }

        public returnMessageUpdateInfo(bool udpateAvaliable, string version)
        {
            this.updateAvaliable = udpateAvaliable;
            this.version = version;
        }

        public returnMessageUpdateInfo(string messageError)
        {
            this.messageError = messageError;
        }
    }
}
