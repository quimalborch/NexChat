using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace NexChat
{
    public class UpdateManager
    {
        private readonly Velopack.UpdateManager updateManager;
        private UpdateInfo? updateInfo;

        public UpdateManager()
        {
            try
            {
                updateManager = new Velopack.UpdateManager(new GithubSource("https://github.com/quimalborch/NexChat", null, false));
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<returnMessageUpdateInfo> CheckActualizacionDisponible()
        {
            try
            {
#if DEBUG
                return new returnMessageUpdateInfo(true, "DEV");
#endif

                if (updateManager is null)
                {
                    throw new Exception("update manager no instanciado");
                }

                updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (updateInfo is null)
                {
                    return new returnMessageUpdateInfo(false);
                }

                return new returnMessageUpdateInfo(true, updateInfo.TargetFullRelease.Version.ToString());
            }
            catch (Exception ex)
            {
                return new returnMessageUpdateInfo($"Error: {ex.Message}");
            }
        }

            public async Task ForceUpdate()
        {
            try
            {
                if (updateManager is null)
                {
                    Debug.WriteLine("Cannot apply updates: UpdateManager is null");
                    return;
                }

#if !DEBUG
                if (updateInfo is null)
                {
                    Debug.WriteLine("Cannot apply updates: update is null");
                    return;
                }
#endif

                await updateManager.DownloadUpdatesAsync(updateInfo);
                updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
            catch (Exception ex)
            {
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
