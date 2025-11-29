using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NexChat.Services
{
    public class CloudflaredService : IDisposable
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";
        private const string EXECUTABLE_NAME = "cloudflared-windows-386.exe";
        private const string TARGET_FILENAME = "cloudflared.exe";
        
        private readonly string _cloudflareFolder;
        private readonly string _executablePath;
        private readonly HttpClient _httpClient;
        private Dictionary<string, Process> _processList = new Dictionary<string, Process>();
        private bool _disposed = false;

        public CloudflaredService()
        {
            _cloudflareFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Cloudflare"
            );

            _executablePath = Path.Combine(_cloudflareFolder, TARGET_FILENAME);
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "NexChat-CloudflaredService");
            
            // Crear carpeta si no existe
            Directory.CreateDirectory(_cloudflareFolder);
            
            Log.Information("CloudflaredService initialized. Executable path: {Path}", _executablePath);
            
            // Limpiar procesos huérfanos al iniciar
            CleanupOrphanedProcesses();
        }

        /// <summary>
        /// Limpia procesos cloudflared huérfanos que puedan estar corriendo
        /// </summary>
        private void CleanupOrphanedProcesses()
        {
            try
            {
                Log.Information("Checking for orphaned cloudflared processes...");
                
                var cloudflaredProcesses = Process.GetProcessesByName("cloudflared");
                
                if (cloudflaredProcesses.Length > 0)
                {
                    Log.Warning("Found {Count} orphaned cloudflared process(es). Cleaning up...", cloudflaredProcesses.Length);
                    
                    foreach (var process in cloudflaredProcesses)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                Log.Information("Killing orphaned process PID: {ProcessId}", process.Id);
                                process.Kill();
                                process.WaitForExit(5000); // Esperar máximo 5 segundos
                            }
                            process.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not kill orphaned process PID: {ProcessId}", process.Id);
                        }
                    }
                    
                    Log.Information("Orphaned process cleanup completed");
                }
                else
                {
                    Log.Information("No orphaned cloudflared processes found");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during orphaned process cleanup");
            }
        }

        /// <summary>
        /// Verifica si es necesario descargar o actualizar el ejecutable de cloudflared
        /// </summary>
        /// <returns>True si necesita actualización, False si está actualizado</returns>
        public async Task<bool> NeedsUpdate()
        {
            try
            {
                // Si no existe el ejecutable, necesitamos descargarlo
                if (!File.Exists(_executablePath))
                {
                    Log.Information("Cloudflared executable not found. Download needed");
                    return true;
                }

                // Obtener información del último release
                var latestRelease = await GetLatestReleaseInfo();
                if (latestRelease == null)
                {
                    Log.Warning("Could not fetch latest release info from GitHub");
                    return false;
                }

                // Buscar el asset correspondiente
                var asset = FindTargetAsset(latestRelease);
                if (asset == null)
                {
                    Log.Warning("Could not find {ExecutableName} in latest release", EXECUTABLE_NAME);
                    return false;
                }

                // Calcular hash del archivo local
                string localHash = CalculateFileHash(_executablePath);
                Log.Debug("Local file hash: {Hash}", localHash);
                Log.Debug("Remote file name: {Name}", asset.Name);
                
                // Si GitHub no proporciona hash, comparar por tamaño
                // El hash real solo se puede verificar descargando el archivo checksums si está disponible
                // Por simplicidad, comparamos por tamaño del archivo
                FileInfo localFileInfo = new FileInfo(_executablePath);
                
                if (localFileInfo.Length != asset.Size)
                {
                    Log.Information("File size mismatch. Local: {LocalSize}, Remote: {RemoteSize}", localFileInfo.Length, asset.Size);
                    return true;
                }

                Log.Information("Cloudflared executable is up to date");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for cloudflared updates");
                return false;
            }
        }

        /// <summary>
        /// Descarga el ejecutable de cloudflared desde GitHub
        /// </summary>
        /// <returns>True si la descarga fue exitosa, False en caso contrario</returns>
        public async Task<bool> DownloadExecutable()
        {
            try
            {
                Log.Information("Starting cloudflared download...");

                // Obtener información del último release
                var latestRelease = await GetLatestReleaseInfo();
                if (latestRelease == null)
                {
                    Log.Error("Could not fetch latest release info from GitHub");
                    return false;
                }

                Log.Information("Latest release: {TagName}", latestRelease.TagName);

                // Buscar el asset correspondiente
                var asset = FindTargetAsset(latestRelease);
                if (asset == null)
                {
                    Log.Error("Could not find {ExecutableName} in latest release", EXECUTABLE_NAME);
                    return false;
                }

                Log.Information("Found asset: {Name} ({Size})", asset.Name, FormatBytes(asset.Size));
                Log.Debug("Download URL: {Url}", asset.BrowserDownloadUrl);

                // Descargar el archivo
                Log.Information("Downloading...");
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(asset.BrowserDownloadUrl);

                // Verificar tamaño
                if (fileBytes.Length != asset.Size)
                {
                    Log.Warning("Downloaded size ({Downloaded}) doesn't match expected size ({Expected})", fileBytes.Length, asset.Size);
                }

                // Guardar temporalmente
                string tempPath = _executablePath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, fileBytes);
                Log.Debug("Downloaded to temporary file: {TempPath}", tempPath);

                // Si existe el archivo anterior, hacer backup
                if (File.Exists(_executablePath))
                {
                    string backupPath = _executablePath + ".backup";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    
                    File.Move(_executablePath, backupPath);
                    Log.Information("Previous version backed up");
                }

                // Mover el archivo temporal al destino final
                File.Move(tempPath, _executablePath);
                
                string fileHash = CalculateFileHash(_executablePath);
                Log.Information("Cloudflared downloaded successfully to: {Path}", _executablePath);
                Log.Information("File hash: {Hash}", fileHash);
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error downloading cloudflared");
                return false;
            }
        }

        /// <summary>
        /// Verifica si el ejecutable existe
        /// </summary>
        public bool IsExecutablePresent()
        {
            bool exists = File.Exists(_executablePath);
            Log.Debug("Cloudflared executable present: {Exists}", exists);
            return exists;
        }

        /// <summary>
        /// Obtiene la ruta completa del ejecutable
        /// </summary>
        public string GetExecutablePath()
        {
            return _executablePath;
        }

        public class OpenTunnelConnection
        {
            public bool Success;
            public string? ErrorMessage;
            public string? TunnelUrl;

            public OpenTunnelConnection(bool success, string errorMessage)
            {
                Success = success;
                ErrorMessage = errorMessage;
            }

            public OpenTunnelConnection(bool success)
            {
                Success = success;
            }
        }

        public async Task<OpenTunnelConnection> TryOpenTunnel(string ChatId, int Port)
        {
            Log.Information("Opening tunnel for ChatId: {ChatId} on Port: {Port}", ChatId, Port);

            // Verificar que el ejecutable existe
            if (!File.Exists(_executablePath))
            {
                Log.Error("Cloudflared executable not found at: {Path}", _executablePath);
                return new OpenTunnelConnection(false, "Cloudflared no está instalado. Por favor, descárgalo primero.");
            }

            // Cerrar túnel existente si hay uno
            if (_processList.ContainsKey(ChatId))
            {
                Log.Warning("Tunnel already exists for ChatId: {ChatId}. Closing existing tunnel first...", ChatId);
                await TryCloseTunnel(ChatId);
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"tunnel --url http://localhost:{Port} --no-autoupdate",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process? processLocal = null;

            try
            {
                processLocal = new Process { StartInfo = psi };
                processLocal.EnableRaisingEvents = true;
                
                // Manejar salida inesperada del proceso
                processLocal.Exited += (sender, e) =>
                {
                    var proc = sender as Process;
                    if (proc != null)
                    {
                        Log.Warning("Cloudflared process exited unexpectedly. PID: {ProcessId}, ExitCode: {ExitCode}", 
                            proc.Id, proc.ExitCode);
                        
                        // Remover de la lista si sale inesperadamente
                        if (_processList.ContainsValue(proc))
                        {
                            var key = _processList.FirstOrDefault(x => x.Value == proc).Key;
                            if (key != null)
                            {
                                _processList.Remove(key);
                                Log.Information("Removed exited process from tracking: {ChatId}", key);
                            }
                        }
                    }
                };
                
                processLocal.Start();
                
                int processId = processLocal.Id;
                Log.Debug("Cloudflared process started with PID: {ProcessId}", processId);
                
                // IMPORTANTE: NO agregar a _processList todavía, solo si tiene éxito
                
                // Timeout más largo para dar tiempo a cloudflared a conectar (30 segundos)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string? url = null;
                
                try
                {
                    url = await GetUrlTunnelAsync(processLocal, cts.Token);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error getting tunnel URL for ChatId: {ChatId}", ChatId);
                }

                if (string.IsNullOrWhiteSpace(url))
                {
                    Log.Error("Could not retrieve tunnel URL for ChatId: {ChatId}. Killing process immediately.", ChatId);
                    
                    // CRÍTICO: Matar el proceso INMEDIATAMENTE si no se obtuvo la URL
                    try
                    {
                        if (!processLocal.HasExited)
                        {
                            Log.Warning("Killing failed cloudflared process PID: {ProcessId}", processId);
                            processLocal.Kill();
                            
                            // Esperar a que termine
                            bool exited = processLocal.WaitForExit(5000);
                            
                            if (!exited)
                            {
                                Log.Error("Process {ProcessId} did not exit after Kill(). Attempting forced termination...", processId);
                                
                                // Intentar terminar usando taskkill como último recurso
                                try
                                {
                                    var killProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "taskkill",
                                            Arguments = $"/F /PID {processId}",
                                            UseShellExecute = false,
                                            CreateNoWindow = true
                                        }
                                    };
                                    killProcess.Start();
                                    killProcess.WaitForExit(3000);
                                    Log.Information("Forced process termination using taskkill for PID: {ProcessId}", processId);
                                }
                                catch (Exception killEx)
                                {
                                    Log.Error(killEx, "Could not force kill process using taskkill");
                                }
                            }
                            else
                            {
                                Log.Information("Process {ProcessId} terminated successfully", processId);
                            }
                        }
                        else
                        {
                            Log.Information("Process {ProcessId} already exited", processId);
                        }
                    }
                    catch (Exception killEx)
                    {
                        Log.Error(killEx, "Error killing failed cloudflared process PID: {ProcessId}", processId);
                    }
                    finally
                    {
                        // Siempre dispose del proceso
                        try
                        {
                            processLocal.Dispose();
                        }
                        catch (Exception disposeEx)
                        {
                            Log.Warning(disposeEx, "Error disposing failed process");
                        }
                    }
                    
                    return new OpenTunnelConnection(false, "No se pudo establecer la conexión con Cloudflare. El servicio puede estar caído o el túnel no se pudo crear.");
                }

                // ÉXITO: Agregar a la lista SOLO si obtuvimos la URL correctamente
                _processList.Add(ChatId, processLocal);
                
                Log.Information("Tunnel opened successfully. URL: {TunnelUrl}", url);

                return new OpenTunnelConnection(true) { TunnelUrl = url };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception opening tunnel for ChatId: {ChatId}", ChatId);
                
                // Intentar limpiar el proceso si se creó
                if (processLocal != null)
                {
                    try
                    {
                        int processId = processLocal.Id;
                        
                        if (!processLocal.HasExited)
                        {
                            Log.Warning("Killing process after exception, PID: {ProcessId}", processId);
                            processLocal.Kill();
                            processLocal.WaitForExit(5000);
                            
                            // Doble verificación con taskkill
                            try
                            {
                                var remainingProcess = Process.GetProcessById(processId);
                                if (remainingProcess != null && !remainingProcess.HasExited)
                                {
                                    Log.Warning("Process still running, using taskkill for PID: {ProcessId}", processId);
                                    var killProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "taskkill",
                                            Arguments = $"/F /PID {processId}",
                                            UseShellExecute = false,
                                            CreateNoWindow = true
                                        }
                                    };
                                    killProcess.Start();
                                    killProcess.WaitForExit(3000);
                                }
                            }
                            catch (ArgumentException)
                            {
                                // Proceso ya no existe, está bien
                                Log.Debug("Process {ProcessId} no longer exists", processId);
                            }
                        }
                        
                        processLocal.Dispose();
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Warning(cleanupEx, "Error cleaning up failed process");
                    }
                    
                    // Asegurarse de que NO está en la lista
                    _processList.Remove(ChatId);
                }
                
                return new OpenTunnelConnection(false, $"Error al iniciar el túnel: {ex.Message}");
            }
        }

        public async Task<OpenTunnelConnection> TryCloseTunnel(string ChatId)
        {
            Log.Information("Closing tunnel for ChatId: {ChatId}", ChatId);

            if (_processList.ContainsKey(ChatId))
            {
                Process processToClose = _processList[ChatId];
                
                try
                {
                    if (!processToClose.HasExited)
                    {
                        Log.Information("Killing tunnel process PID: {ProcessId} for ChatId: {ChatId}", 
                            processToClose.Id, ChatId);
                        
                        processToClose.Kill();
                        
                        // Esperar a que el proceso termine (máximo 5 segundos)
                        bool exited = processToClose.WaitForExit(5000);
                        
                        if (!exited)
                        {
                            Log.Warning("Process did not exit gracefully, forcing termination");
                            // Intentar forzar cierre de nuevo
                            try
                            {
                                processToClose.Kill();
                                processToClose.WaitForExit(2000);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Could not force kill process");
                            }
                        }
                        
                        Log.Information("Tunnel process terminated for ChatId: {ChatId}", ChatId);
                    }
                    else
                    {
                        Log.Debug("Tunnel process already exited for ChatId: {ChatId}", ChatId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error killing tunnel process for ChatId: {ChatId}", ChatId);
                }
                finally
                {
                    // Siempre limpiar el proceso y removerlo de la lista
                    try
                    {
                        processToClose.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error disposing process");
                    }
                    
                    _processList.Remove(ChatId);
                }
                
                return new OpenTunnelConnection(true);
            }
            else
            {
                Log.Warning("No active tunnel found for ChatId: {ChatId}", ChatId);
                return new OpenTunnelConnection(false, "No hay un túnel activo para este chat.");
            }
        }

        private async Task<string?> GetUrlTunnelAsync(Process proc, CancellationToken token)
        {
            try
            {
                var stream = proc.StandardError.BaseStream;
                var buffer = new byte[2048];
                var sb = new StringBuilder();

                while (!token.IsCancellationRequested && !proc.HasExited)
                {
                    // Usar token con timeout para ReadAsync
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read > 0)
                    {
                        string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                        sb.Append(chunk);
                        
                        // Log de output para debugging
                        Log.Debug("Cloudflared output: {Output}", chunk.Trim());

                        var match = Regex.Match(sb.ToString(), @"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");
                        if (match.Success)
                        {
                            Log.Debug("Tunnel URL extracted from cloudflared output: {Url}", match.Value);
                            return match.Value;
                        }
                    }
                    else
                    {
                        // No hay datos disponibles, esperar un poco
                        await Task.Delay(100, token);
                    }
                }
                
                // Si el proceso terminó antes de obtener la URL
                if (proc.HasExited)
                {
                    Log.Error("Cloudflared process exited before providing tunnel URL. Exit code: {ExitCode}", proc.ExitCode);
                }

                return null;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("Timeout while waiting for tunnel URL (30 seconds)");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting tunnel URL from cloudflared output");
                return null;
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseInfo()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);
                return release;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching release info from GitHub");
                return null;
            }
        }

        private GitHubAsset? FindTargetAsset(GitHubRelease release)
        {
            if (release.Assets == null)
                return null;

            foreach (var asset in release.Assets)
            {
                if (asset.Name.Equals(EXECUTABLE_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        private string CalculateFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // Clases para deserializar la respuesta de la API de GitHub
        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("assets")]
            public GitHubAsset[]? Assets { get; set; }
        }

        private class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Log.Information("Disposing CloudflaredService - closing all tunnels");
                
                // Cerrar todos los túneles activos
                var chatIds = _processList.Keys.ToList();
                foreach (var chatId in chatIds)
                {
                    try
                    {
                        TryCloseTunnel(chatId).Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error closing tunnel during dispose: {ChatId}", chatId);
                    }
                }
                
                _httpClient?.Dispose();
            }

            _disposed = true;
        }

        ~CloudflaredService()
        {
            Dispose(false);
        }

        /// <summary>
        /// Fuerza la limpieza de todos los procesos cloudflared (incluso los que no están en nuestra lista)
        /// </summary>
        public void ForceCleanupAllProcesses()
        {
            Log.Warning("Force cleanup of all cloudflared processes requested");
            
            try
            {
                // Primero cerrar los que tenemos en nuestra lista
                var chatIds = _processList.Keys.ToList();
                foreach (var chatId in chatIds)
                {
                    try
                    {
                        TryCloseTunnel(chatId).Wait(TimeSpan.FromSeconds(3));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error closing tracked tunnel during force cleanup: {ChatId}", chatId);
                    }
                }
                
                // Luego buscar y matar cualquier proceso cloudflared que quede
                CleanupOrphanedProcesses();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during force cleanup");
            }
        }
    }
}
