using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NexChat.Data;
using NexChat.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Velopack;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Serilog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NexChat
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private ConfigurationService _configurationService;
        private ResourceDictionary? _currentThemeDictionary;
        private bool _isFirstLaunch = true;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Configurar Serilog ANTES de cualquier otra cosa
            ConfigureSerilog();

            try
            {
                VelopackApp.Build().Run();
                Log.Information("Velopack initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Velopack initialization error");
            }


            InitializeComponent();
            _configurationService = new ConfigurationService();
            
            // Aplicar tema base solo en el primer inicio
            SetInitialTheme();

            Log.Information("NexChat application initialized");
        }

        /// <summary>
        /// Configura Serilog con logs por ejecución, rotación de 7 días
        /// </summary>
        private void ConfigureSerilog()
        {
            try
            {
                // Ruta de la carpeta de logs en LocalApplicationData
                string logFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NexChat",
                    "Logs"
                );

                // Crear la carpeta si no existe
                Directory.CreateDirectory(logFolder);

                // Nombre del archivo de log con fecha y hora de ejecución
                string logFileName = $"nexchat_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                string logFilePath = System.IO.Path.Combine(logFolder, logFileName);

                // Configurar Serilog
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        logFilePath,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Infinite, // Un archivo por ejecución
                        retainedFileCountLimit: null, // Gestionaremos la limpieza manualmente
                        buffered: false // Escribir inmediatamente
                    )
                    .CreateLogger();

                Log.Information("=== NexChat Started ===");
                Log.Information("Application Version: {Version}", typeof(App).Assembly.GetName().Version);
                Log.Information("Log file: {LogPath}", logFilePath);

                // Limpiar logs antiguos (más de 7 días)
                CleanupOldLogs(logFolder, 7);
            }
            catch (Exception ex)
            {
                // Si falla Serilog, al menos escribir en Debug
                System.Diagnostics.Debug.WriteLine($"Error configuring Serilog: {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina archivos de log más antiguos de X días
        /// </summary>
        private void CleanupOldLogs(string logFolder, int daysToKeep)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logFolder, "nexchat_*.log");

                int deletedCount = 0;
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not delete old log file: {LogFile}", logFile);
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    Log.Information("Cleaned up {Count} old log file(s)", deletedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during log cleanup");
            }
        }

        /// <summary>
        /// Establece el tema base de la aplicación (solo se puede hacer una vez, antes de crear ventanas)
        /// </summary>
        private void SetInitialTheme()
        {
            try
            {
                var configuration = _configurationService.GetOrCreateConfiguration();
                var selectedTheme = configuration.paletaColoresSeleccionada;

                Log.Information("Setting initial base theme for: {Theme}", selectedTheme);

                // Establecer el tema base de la aplicación (Dark/Light/Default)
                // Esto SOLO se puede hacer antes de crear ventanas
                switch (selectedTheme)
                {
                    case Configuration.PaletaColoresSeleccionada.Automatico:
                        // No establecer RequestedTheme para usar el tema del sistema
                        Log.Information("Initial theme: Automatic (System Default)");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Claro:
                        this.RequestedTheme = ApplicationTheme.Light;
                        Log.Information("Initial theme: Light");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Oscuro:
                    case Configuration.PaletaColoresSeleccionada.Rojo:
                    case Configuration.PaletaColoresSeleccionada.Verde:
                    case Configuration.PaletaColoresSeleccionada.Morado:
                        // Todos los temas personalizados usan base Dark
                        this.RequestedTheme = ApplicationTheme.Dark;
                        Log.Information("Initial theme: Dark (for standard or custom themes)");
                        break;
                        
                    default:
                        this.RequestedTheme = ApplicationTheme.Dark;
                        Log.Information("Initial theme: Dark (Default fallback)");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting initial theme");
                this.RequestedTheme = ApplicationTheme.Dark; // Fallback seguro
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Information("Application launched");
            
            // Suscribirse a excepciones no manejadas para cleanup
            this.UnhandledException += App_UnhandledException;
            
            // Aplicar tema personalizado si es necesario (ANTES de crear la ventana)
            ApplyCustomTheme();
            
            _window = new MainWindow();
            _window.Activate();
            
            _isFirstLaunch = false;
            
            Log.Information("Main window created and activated");
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled exception in application");
            
            // Intentar cleanup antes de que la app muera
            CleanupCloudflaredProcesses();
            
            // Marcar como manejado para evitar crash inmediato y permitir logging
            e.Handled = true;
        }

        private void CleanupCloudflaredProcesses()
        {
            try
            {
                Log.Information("Cleaning up cloudflared processes");
                
                var cloudflaredProcesses = System.Diagnostics.Process.GetProcessesByName("cloudflared");
                
                if (cloudflaredProcesses.Length > 0)
                {
                    Log.Warning("Found {Count} cloudflared process(es). Cleaning up...", 
                        cloudflaredProcesses.Length);
                    
                    foreach (var process in cloudflaredProcesses)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                Log.Information("Killing cloudflared process PID: {ProcessId}", process.Id);
                                process.Kill();
                                process.WaitForExit(5000);
                            }
                            process.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not kill cloudflared process");
                        }
                    }
                }
                
                Log.Information("Cloudflared process cleanup completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during cloudflared cleanup");
            }
        }

        /// <summary>
        /// Aplica solo los temas personalizados (ResourceDictionaries)
        /// </summary>
        private void ApplyCustomTheme()
        {
            try
            {
                var configuration = _configurationService.GetOrCreateConfiguration();
                var selectedTheme = configuration.paletaColoresSeleccionada;

                Log.Information("Applying custom theme: {Theme}", selectedTheme);

                // Remove previous custom theme if exists
                if (_currentThemeDictionary != null && Resources.MergedDictionaries.Contains(_currentThemeDictionary))
                {
                    Resources.MergedDictionaries.Remove(_currentThemeDictionary);
                    _currentThemeDictionary = null;
                    Log.Debug("Previous custom theme removed");
                }

                // Solo cargar ResourceDictionary para temas personalizados
                switch (selectedTheme)
                {
                    case Configuration.PaletaColoresSeleccionada.Rojo:
                        LoadCustomTheme("ms-appx:///Themes/RedTheme.xaml");
                        Log.Information("Applied custom theme: Red");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Verde:
                        LoadCustomTheme("ms-appx:///Themes/GreenTheme.xaml");
                        Log.Information("Applied custom theme: Green");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Morado:
                        LoadCustomTheme("ms-appx:///Themes/PurpleTheme.xaml");
                        Log.Information("Applied custom theme: Purple");
                        break;
                        
                    default:
                        Log.Debug("No custom theme to apply");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying custom theme");
            }
        }

        private void LoadCustomTheme(string themeUri)
        {
            try
            {
                Log.Debug("Loading custom theme from: {ThemeUri}", themeUri);
                
                _currentThemeDictionary = new ResourceDictionary
                {
                    Source = new Uri(themeUri)
                };
                
                Resources.MergedDictionaries.Add(_currentThemeDictionary);
                
                Log.Information("Custom theme loaded successfully: {ThemeUri}", themeUri);
                Log.Debug("Total merged dictionaries: {Count}", Resources.MergedDictionaries.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading custom theme {ThemeUri}", themeUri);
            }
        }

        /// <summary>
        /// Actualiza el tema (solo para cambios dinámicos de ResourceDictionary)
        /// NOTA: No se puede cambiar el ApplicationTheme después del primer lanzamiento
        /// </summary>
        public void UpdateTheme()
        {
            try
            {
                Log.Information("UpdateTheme called - reloading custom theme");
                
                var configuration = _configurationService.GetOrCreateConfiguration();
                var selectedTheme = configuration.paletaColoresSeleccionada;
                
                // Advertencia si se intenta cambiar entre Light/Dark después del primer lanzamiento
                if (!_isFirstLaunch)
                {
                    var currentBaseTheme = this.RequestedTheme;
                    var needsRestart = false;
                    
                    if (selectedTheme == Configuration.PaletaColoresSeleccionada.Claro && currentBaseTheme != ApplicationTheme.Light)
                    {
                        needsRestart = true;
                    }
                    else if ((selectedTheme == Configuration.PaletaColoresSeleccionada.Oscuro || 
                              selectedTheme == Configuration.PaletaColoresSeleccionada.Rojo ||
                              selectedTheme == Configuration.PaletaColoresSeleccionada.Verde ||
                              selectedTheme == Configuration.PaletaColoresSeleccionada.Morado) && 
                             currentBaseTheme != ApplicationTheme.Dark)
                    {
                        needsRestart = true;
                    }
                    
                    if (needsRestart)
                    {
                        Log.Warning("Theme change requires app restart (base theme mismatch)");
                    }
                }
                
                // Aplicar solo los cambios de ResourceDictionary (temas personalizados)
                ApplyCustomTheme();
                
                // Forzar actualización del layout de todas las ventanas
                if (_window?.Content is FrameworkElement rootElement)
                {
                    rootElement.UpdateLayout();
                    Log.Debug("Window content layout updated");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating theme");
            }
        }
    }
}
