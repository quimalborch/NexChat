using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NexChat.Data;
using NexChat.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NexChat
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConfigWindow : Window
    {
        private MainWindow _ventanaPrincipal;
        private ConfigurationService _configurationService;
        private bool _isLoadingTheme = false;

        public ConfigWindow(MainWindow ventanaPrincipal, ConfigurationService configurationService)
        {
            InitializeComponent();
            _ventanaPrincipal = ventanaPrincipal;
            _configurationService = configurationService;
            
            // Configurar tamaño de la ventana
            var size = new Windows.Graphics.SizeInt32();
            size.Width = 768;
            size.Height = 600;
            this.AppWindow.Resize(size);
            
            // Establecer configuración de la ventana
            var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }
            
            LoadConfigurationUI();
            this.AppWindow.Closing += (_, __) => _ventanaPrincipal.AppWindow.Show();
        }

        private void BtnIdentidad_Click(object sender, RoutedEventArgs e)
        {
            IdentidadContent.Visibility = Visibility.Visible;
            AparienciaContent.Visibility = Visibility.Collapsed;
            
            BtnIdentidad.Style = (Style)Application.Current.Resources["NavigationButtonSelectedStyle"];
            BtnApariencia.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
        }

        private void BtnApariencia_Click(object sender, RoutedEventArgs e)
        {
            IdentidadContent.Visibility = Visibility.Collapsed;
            AparienciaContent.Visibility = Visibility.Visible;
            
            BtnApariencia.Style = (Style)Application.Current.Resources["NavigationButtonSelectedStyle"];
            BtnIdentidad.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
        }

        private void TextBoxIdentidad_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TextBoxIdentidad.Text))
            {
                BtnConfirmarNewIdentidad.Visibility = Visibility.Visible;
            }
            else
            {
                BtnConfirmarNewIdentidad.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnConfirmarNewIdentidad_Click(object sender, RoutedEventArgs e)
        {
            string nombreUsuario = TextBoxIdentidad.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombreUsuario))
            {
                return;
            }

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Confirmar cambio de nombre de usuario",
                Content = $"¿Está seguro de que desea cambiar su nombre de usuario a '{nombreUsuario}'?",
                PrimaryButtonText = "Confirmar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                bool success = await _configurationService.UpdateUserNameAsync(nombreUsuario);

                if (success)
                {
                    UpdateUserIdDisplay();
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "Nombre actualizado",
                        Content = $"Tu nombre de usuario ha sido actualizado a '{nombreUsuario}'",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await successDialog.ShowAsync();
                    
                    TextBoxIdentidad.Text = string.Empty;
                    BtnConfirmarNewIdentidad.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "No se pudo guardar el nombre de usuario. Inténtalo de nuevo.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private void LoadConfigurationUI()
        {
            var configuration = _configurationService.CurrentConfiguration;
            
            if (configuration != null)
            {
                UpdateUserIdDisplay();
            }
            else
            {
                TextGuildUsuario.Text = "No configurado";
            }

            LoadThemeOptions();
        }

        private void UpdateUserIdDisplay()
        {
            var configuration = _configurationService.CurrentConfiguration;
            if (configuration != null)
            {
                TextGuildUsuario.Text = configuration.idUsuario.ToString();
            }
        }

        private void LoadThemeOptions()
        {
            _isLoadingTheme = true;

            var themeOptions = new List<ThemeOption>();

            foreach (Configuration.PaletaColoresSeleccionada palette in Enum.GetValues(typeof(Configuration.PaletaColoresSeleccionada)))
            {
                var fieldInfo = palette.GetType().GetField(palette.ToString());
                var descriptionAttribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
                
                string displayName = descriptionAttribute?.Description ?? palette.ToString();
                
                themeOptions.Add(new ThemeOption
                {
                    Value = palette,
                    DisplayName = displayName
                });
            }

            ComboBoxTema.ItemsSource = themeOptions;
            ComboBoxTema.DisplayMemberPath = "DisplayName";

            var currentConfig = _configurationService.GetOrCreateConfiguration();
            var selectedOption = themeOptions.FirstOrDefault(t => t.Value == currentConfig.paletaColoresSeleccionada);
            if (selectedOption != null)
            {
                ComboBoxTema.SelectedItem = selectedOption;
            }

            _isLoadingTheme = false;
        }

        private async void ComboBoxTema_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingTheme)
            {
                return;
            }

            if (ComboBoxTema.SelectedItem is ThemeOption selectedTheme)
            {
                var configuration = _configurationService.GetOrCreateConfiguration();
                var previousTheme = configuration.paletaColoresSeleccionada;
                configuration.paletaColoresSeleccionada = selectedTheme.Value;

                bool success = await _configurationService.SaveConfigurationAsync(configuration);

                if (success)
                {
                    // Determinar si se necesita reiniciar la app
                    bool needsRestart = RequiresAppRestart(previousTheme, selectedTheme.Value);
                    
                    // Recargar el tema a nivel de aplicación
                    if (Application.Current is App app)
                    {
                        app.UpdateTheme();
                    }

                    ContentDialog dialog;
                    if (needsRestart)
                    {
                        dialog = new ContentDialog
                        {
                            Title = "Reinicio necesario",
                            Content = $"El tema '{selectedTheme.DisplayName}' se ha guardado. Para aplicar completamente el tema, necesitas reiniciar la aplicación.",
                            PrimaryButtonText = "Cerrar aplicación",
                            CloseButtonText = "Continuar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        
                        var result = await dialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            // Cerrar la aplicación
                            Application.Current.Exit();
                        }
                    }
                    else
                    {
                        dialog = new ContentDialog
                        {
                            Title = "Tema actualizado",
                            Content = $"El tema '{selectedTheme.DisplayName}' ha sido aplicado correctamente.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await dialog.ShowAsync();
                    }
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "No se pudo guardar el tema seleccionado. Inténtalo de nuevo.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        /// <summary>
        /// Determina si cambiar de un tema a otro requiere reiniciar la aplicación
        /// </summary>
        private bool RequiresAppRestart(Configuration.PaletaColoresSeleccionada from, Configuration.PaletaColoresSeleccionada to)
        {
            // Determinar el tema base de cada uno
            bool fromIsLight = from == Configuration.PaletaColoresSeleccionada.Claro;
            bool toIsLight = to == Configuration.PaletaColoresSeleccionada.Claro;
            
            // Si cambia entre Light y Dark (o cualquier otro), necesita reinicio
            return fromIsLight != toIsLight;
        }

        private void ApplyThemeToCurrentWindow(Configuration.PaletaColoresSeleccionada selectedTheme)
        {
            try
            {
                ElementTheme theme = selectedTheme switch
                {
                    Configuration.PaletaColoresSeleccionada.Automatico => ElementTheme.Default,
                    Configuration.PaletaColoresSeleccionada.Oscuro => ElementTheme.Dark,
                    Configuration.PaletaColoresSeleccionada.Claro => ElementTheme.Light,
                    Configuration.PaletaColoresSeleccionada.Rojo => ElementTheme.Dark,
                    Configuration.PaletaColoresSeleccionada.Verde => ElementTheme.Dark,
                    Configuration.PaletaColoresSeleccionada.Morado => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                // Aplicar el tema a la ventana actual
                if (this.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = theme;
                }

                // Aplicar el tema a la ventana principal si está disponible
                if (_ventanaPrincipal?.Content is FrameworkElement mainWindowRoot)
                {
                    mainWindowRoot.RequestedTheme = theme;
                }

                Console.WriteLine($"Applied theme to windows: {selectedTheme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme: {ex.Message}");
            }
        }

        private class ThemeOption
        {
            public Configuration.PaletaColoresSeleccionada Value { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
