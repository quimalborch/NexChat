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
                    Console.WriteLine("Cloudflared executable not found. Download needed.");
                    return true;
                }

                // Obtener información del último release
                var latestRelease = await GetLatestReleaseInfo();
                if (latestRelease == null)
                {
                    Console.WriteLine("Could not fetch latest release info from GitHub.");
                    return false;
                }

                // Buscar el asset correspondiente
                var asset = FindTargetAsset(latestRelease);
                if (asset == null)
                {
                    Console.WriteLine($"Could not find {EXECUTABLE_NAME} in latest release.");
                    return false;
                }

                // Calcular hash del archivo local
                string localHash = CalculateFileHash(_executablePath);
                Console.WriteLine($"Local file hash: {localHash}");
                Console.WriteLine($"Remote file name: {asset.Name}");
                
                // Si GitHub no proporciona hash, comparar por tamaño
                // El hash real solo se puede verificar descargando el archivo checksums si está disponible
                // Por simplicidad, comparamos por tamaño del archivo
                FileInfo localFileInfo = new FileInfo(_executablePath);
                
                if (localFileInfo.Length != asset.Size)
                {
                    Console.WriteLine($"File size mismatch. Local: {localFileInfo.Length}, Remote: {asset.Size}");
                    return true;
                }

                Console.WriteLine("Cloudflared executable is up to date.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
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
                Console.WriteLine("Starting cloudflared download...");

                // Obtener información del último release
                var latestRelease = await GetLatestReleaseInfo();
                if (latestRelease == null)
                {
                    Console.WriteLine("Could not fetch latest release info from GitHub.");
                    return false;
                }

                Console.WriteLine($"Latest release: {latestRelease.TagName}");

                // Buscar el asset correspondiente
                var asset = FindTargetAsset(latestRelease);
                if (asset == null)
                {
                    Console.WriteLine($"Could not find {EXECUTABLE_NAME} in latest release.");
                    return false;
                }

                Console.WriteLine($"Found asset: {asset.Name} ({FormatBytes(asset.Size)})");
                Console.WriteLine($"Download URL: {asset.BrowserDownloadUrl}");

                // Descargar el archivo
                Console.WriteLine("Downloading...");
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(asset.BrowserDownloadUrl);

                // Verificar tamaño
                if (fileBytes.Length != asset.Size)
                {
                    Console.WriteLine($"Warning: Downloaded size ({fileBytes.Length}) doesn't match expected size ({asset.Size})");
                }

                // Guardar temporalmente
                string tempPath = _executablePath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, fileBytes);
                Console.WriteLine($"Downloaded to temporary file: {tempPath}");

                // Si existe el archivo anterior, hacer backup
                if (File.Exists(_executablePath))
                {
                    string backupPath = _executablePath + ".backup";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    
                    File.Move(_executablePath, backupPath);
                    Console.WriteLine("Previous version backed up.");
                }

                // Mover el archivo temporal al destino final
                File.Move(tempPath, _executablePath);
                
                Console.WriteLine($"✓ Cloudflared downloaded successfully to: {_executablePath}");
                Console.WriteLine($"✓ File hash: {CalculateFileHash(_executablePath)}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error downloading cloudflared: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si el ejecutable existe
        /// </summary>
        public bool IsExecutablePresent()
        {
            return File.Exists(_executablePath);
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

            // Timeout para evitar bloqueos
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string? url = await GetUrlTunnelAsync(Processlocal, cts.Token);

            if (string.IsNullOrWhiteSpace(url))
                return new OpenTunnelConnection(false, "Could not retrieve tunnel URL.");


            openTunnelConnection.Success = true;
            openTunnelConnection.TunnelUrl = url;
            return openTunnelConnection;
        }

        public async Task<OpenTunnelConnection> TryCloseTunnel(string ChatId)
        {
            OpenTunnelConnection openTunnelConnection = new(false);

            if (_processList.ContainsKey(ChatId))
            {
                Process processToClose = _processList[ChatId];
                if (!processToClose.HasExited)
                {
                    processToClose.Kill();
                    processToClose.WaitForExit();
                }
                _processList.Remove(ChatId);
                openTunnelConnection = new OpenTunnelConnection(true);
            }
            else
            {
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
                            return match.Value;
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
                Console.WriteLine($"Error fetching release info: {ex.Message}");
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
