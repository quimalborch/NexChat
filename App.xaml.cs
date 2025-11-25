using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using NexChat.Data;
using NexChat.Services;

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

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            _configurationService = new ConfigurationService();
            ApplyTheme();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }

        private void ApplyTheme()
        {
            var configuration = _configurationService.GetOrCreateConfiguration();
            var selectedTheme = configuration.paletaColoresSeleccionada;

            ElementTheme theme = selectedTheme switch
            {
                Configuration.PaletaColoresSeleccionada.Automatico => ElementTheme.Default,
                Configuration.PaletaColoresSeleccionada.Oscuro => ElementTheme.Dark,
                Configuration.PaletaColoresSeleccionada.Claro => ElementTheme.Light,
                // Para los temas de color personalizados, por ahora usamos el tema oscuro
                // TODO: Implementar paletas de colores personalizadas
                Configuration.PaletaColoresSeleccionada.Rojo => ElementTheme.Dark,
                Configuration.PaletaColoresSeleccionada.Verde => ElementTheme.Dark,
                Configuration.PaletaColoresSeleccionada.Morado => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            // Aplicar el tema a nivel de aplicación
            this.RequestedTheme = theme switch
            {
                ElementTheme.Dark => ApplicationTheme.Dark,
                ElementTheme.Light => ApplicationTheme.Light,
                _ => ApplicationTheme.Light // Default usa el tema del sistema
            };

            Console.WriteLine($"Applied theme: {selectedTheme} -> {this.RequestedTheme}");
        }
    }
}
