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
            try
            {
                VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Velopack initialization error: {ex.Message}");
            }


            InitializeComponent();
            _configurationService = new ConfigurationService();
            
            // Aplicar tema base solo en el primer inicio
            SetInitialTheme();
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

                Console.WriteLine($"Setting initial base theme for: {selectedTheme}");

                // Establecer el tema base de la aplicación (Dark/Light/Default)
                // Esto SOLO se puede hacer antes de crear ventanas
                switch (selectedTheme)
                {
                    case Configuration.PaletaColoresSeleccionada.Automatico:
                        // No establecer RequestedTheme para usar el tema del sistema
                        Console.WriteLine("Initial theme: Automatic (System Default)");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Claro:
                        this.RequestedTheme = ApplicationTheme.Light;
                        Console.WriteLine("Initial theme: Light");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Oscuro:
                    case Configuration.PaletaColoresSeleccionada.Rojo:
                    case Configuration.PaletaColoresSeleccionada.Verde:
                    case Configuration.PaletaColoresSeleccionada.Morado:
                        // Todos los temas personalizados usan base Dark
                        this.RequestedTheme = ApplicationTheme.Dark;
                        Console.WriteLine("Initial theme: Dark (for standard or custom themes)");
                        break;
                        
                    default:
                        this.RequestedTheme = ApplicationTheme.Dark;
                        Console.WriteLine("Initial theme: Dark (Default fallback)");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting initial theme: {ex.Message}");
                this.RequestedTheme = ApplicationTheme.Dark; // Fallback seguro
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Aplicar tema personalizado si es necesario (ANTES de crear la ventana)
            ApplyCustomTheme();
            
            _window = new MainWindow();
            _window.Activate();
            
            _isFirstLaunch = false;
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

                Console.WriteLine($"Applying custom theme: {selectedTheme}");

                // Remove previous custom theme if exists
                if (_currentThemeDictionary != null && Resources.MergedDictionaries.Contains(_currentThemeDictionary))
                {
                    Resources.MergedDictionaries.Remove(_currentThemeDictionary);
                    _currentThemeDictionary = null;
                    Console.WriteLine("Previous custom theme removed");
                }

                // Solo cargar ResourceDictionary para temas personalizados
                switch (selectedTheme)
                {
                    case Configuration.PaletaColoresSeleccionada.Rojo:
                        LoadCustomTheme("ms-appx:///Themes/RedTheme.xaml");
                        Console.WriteLine("Applied custom theme: Red");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Verde:
                        LoadCustomTheme("ms-appx:///Themes/GreenTheme.xaml");
                        Console.WriteLine("Applied custom theme: Green");
                        break;
                        
                    case Configuration.PaletaColoresSeleccionada.Morado:
                        LoadCustomTheme("ms-appx:///Themes/PurpleTheme.xaml");
                        Console.WriteLine("Applied custom theme: Purple");
                        break;
                        
                    default:
                        Console.WriteLine("No custom theme to apply");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying custom theme: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void LoadCustomTheme(string themeUri)
        {
            try
            {
                Console.WriteLine($"Loading custom theme from: {themeUri}");
                
                _currentThemeDictionary = new ResourceDictionary
                {
                    Source = new Uri(themeUri)
                };
                
                Resources.MergedDictionaries.Add(_currentThemeDictionary);
                
                Console.WriteLine($"✓ Custom theme loaded successfully: {themeUri}");
                Console.WriteLine($"✓ Total merged dictionaries: {Resources.MergedDictionaries.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error loading custom theme {themeUri}: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                Console.WriteLine("UpdateTheme called - reloading custom theme");
                
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
                        Console.WriteLine("⚠️ Theme change requires app restart (base theme mismatch)");
                    }
                }
                
                // Aplicar solo los cambios de ResourceDictionary (temas personalizados)
                ApplyCustomTheme();
                
                // Forzar actualización del layout de todas las ventanas
                if (_window?.Content is FrameworkElement rootElement)
                {
                    rootElement.UpdateLayout();
                    Console.WriteLine("Window content layout updated");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating theme: {ex.Message}");
            }
        }
    }
}
