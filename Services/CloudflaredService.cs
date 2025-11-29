using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    public class CloudflaredService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";
        private const string EXECUTABLE_NAME = "cloudflared-windows-386.exe";
        private const string TARGET_FILENAME = "cloudflared.exe";
        
        private readonly string _cloudflareFolder;
        private readonly string _executablePath;
        private readonly HttpClient _httpClient;
        private Dictionary<string, Process> _processList = new Dictionary<string, Process>();

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
            OpenTunnelConnection openTunnelConnection = new(false);

            Log.Information("Opening tunnel for ChatId: {ChatId} on Port: {Port}", ChatId, Port);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"tunnel --url http://localhost:{Port} --no-autoupdate",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process Processlocal = new Process { StartInfo = psi };
            Processlocal.Start();
            _processList.Add(ChatId, Processlocal);

            Log.Debug("Cloudflared process started with PID: {ProcessId}", Processlocal.Id);

            // Timeout para evitar bloqueos
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string? url = await GetUrlTunnelAsync(Processlocal, cts.Token);

            if (string.IsNullOrWhiteSpace(url))
            {
                Log.Error("Could not retrieve tunnel URL for ChatId: {ChatId}", ChatId);
                return new OpenTunnelConnection(false, "Could not retrieve tunnel URL.");
            }

            Log.Information("Tunnel opened successfully. URL: {TunnelUrl}", url);

            openTunnelConnection.Success = true;
            openTunnelConnection.TunnelUrl = url;
            return openTunnelConnection;
        }

        public async Task<OpenTunnelConnection> TryCloseTunnel(string ChatId)
        {
            OpenTunnelConnection openTunnelConnection = new(false);

            Log.Information("Closing tunnel for ChatId: {ChatId}", ChatId);

            if (_processList.ContainsKey(ChatId))
            {
                Process processToClose = _processList[ChatId];
                if (!processToClose.HasExited)
                {
                    processToClose.Kill();
                    processToClose.WaitForExit();
                    Log.Information("Tunnel process killed for ChatId: {ChatId}", ChatId);
                }
                else
                {
                    Log.Debug("Tunnel process already exited for ChatId: {ChatId}", ChatId);
                }
                _processList.Remove(ChatId);
                openTunnelConnection = new OpenTunnelConnection(true);
            }
            else
            {
                Log.Warning("No active tunnel found for ChatId: {ChatId}", ChatId);
                return new OpenTunnelConnection(false, "No active tunnel found for the given ChatId.");
            }
            return openTunnelConnection;
        }

        private async Task<string?> GetUrlTunnelAsync(Process proc, CancellationToken token)
        {
            try
            {
                var stream = proc.StandardError.BaseStream;
                var buffer = new byte[2048];
                var sb = new StringBuilder();

                while (!token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read > 0)
                    {
                        string chunk = Encoding.UTF8.GetString(buffer, 0, read);
                        sb.Append(chunk);

                        var match = Regex.Match(sb.ToString(), @"https://[a-zA-Z0-9\-]+\.trycloudflare\.com");
                        if (match.Success)
                        {
                            Log.Debug("Tunnel URL extracted from cloudflared output: {Url}", match.Value);
                            return match.Value;
                        }
                    }
                    else
                    {
                        await Task.Delay(50, token);
                    }
                }

                return null;
            }
            catch (TaskCanceledException)
            {
                Log.Warning("Timeout while waiting for tunnel URL");
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
    }
}
