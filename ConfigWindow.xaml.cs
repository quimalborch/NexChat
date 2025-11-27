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
using System.Collections.ObjectModel;
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
        private bool _isPasswordVisible = false;

        public ObservableCollection<CertifiedUser> CertifiedUsers { get; set; } = new ObservableCollection<CertifiedUser>();

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
            UsuariosCertificadosContent.Visibility = Visibility.Collapsed;
            
            BtnIdentidad.Style = (Style)Application.Current.Resources["NavigationButtonSelectedStyle"];
            BtnApariencia.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
            BtnUsuariosCertificados.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
        }

        private void BtnApariencia_Click(object sender, RoutedEventArgs e)
        {
            IdentidadContent.Visibility = Visibility.Collapsed;
            AparienciaContent.Visibility = Visibility.Visible;
            UsuariosCertificadosContent.Visibility = Visibility.Collapsed;
            
            BtnApariencia.Style = (Style)Application.Current.Resources["NavigationButtonSelectedStyle"];
            BtnIdentidad.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
            BtnUsuariosCertificados.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
        }

        private void BtnUsuariosCertificados_Click(object sender, RoutedEventArgs e)
        {
            IdentidadContent.Visibility = Visibility.Collapsed;
            AparienciaContent.Visibility = Visibility.Collapsed;
            UsuariosCertificadosContent.Visibility = Visibility.Visible;
            
            BtnUsuariosCertificados.Style = (Style)Application.Current.Resources["NavigationButtonSelectedStyle"];
            BtnIdentidad.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];
            BtnApariencia.Style = (Style)Application.Current.Resources["NavigationButtonStyle"];

            LoadCertifiedUsers();
        }

        private void TextBoxIdentidad_TextChanged(object sender, TextChangedEventArgs e)
        {
            var configuration = _configurationService.CurrentConfiguration;
            if ((configuration?.nombreUsuario != TextBoxIdentidad.Text) && !string.IsNullOrWhiteSpace(TextBoxIdentidad.Text))
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
                PasswordBoxGuildUsuario.Password = "No configurado";
            }

            LoadThemeOptions();
        }

        private void UpdateUserIdDisplay()
        {
            var configuration = _configurationService.CurrentConfiguration;
            if (configuration != null)
            {
                PasswordBoxGuildUsuario.Password = configuration.idUsuario.ToString();
                TextBoxIdentidad.Text = configuration.nombreUsuario;
            }
        }

        private void BtnTogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;
            
            if (_isPasswordVisible)
            {
                PasswordBoxGuildUsuario.IsEnabled = true;
                PasswordBoxGuildUsuario.PasswordRevealMode = PasswordRevealMode.Visible;
                IconEye.Glyph = "\uF78D";
            }
            else
            {
                PasswordBoxGuildUsuario.IsEnabled = false;
                PasswordBoxGuildUsuario.PasswordRevealMode = PasswordRevealMode.Hidden;
                IconEye.Glyph = "\uED1A";
            }
        }

        private async void BtnCopyUserId_Click(object sender, RoutedEventArgs e)
        {
            var configuration = _configurationService.CurrentConfiguration;
            if (configuration != null)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(configuration.idUsuario.ToString());
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                var dialog = new ContentDialog
                {
                    Title = "ID copiado",
                    Content = "El ID de usuario ha sido copiado al portapapeles.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
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

        private void LoadCertifiedUsers()
        {
            CertifiedUsers.Clear();
            var users = _configurationService.GetCertifiedUsers();
            
            foreach (var user in users)
            {
                CertifiedUsers.Add(user);
            }

            // Mostrar/ocultar empty state y ListView
            if (CertifiedUsers.Count > 0)
            {
                EmptyUsersState.Visibility = Visibility.Collapsed;
                CertifiedUsersListView.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyUsersState.Visibility = Visibility.Visible;
                CertifiedUsersListView.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnAgregarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TextBoxNuevoNombre.Text.Trim();
            string llave = TextBoxNuevaLlave.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                var dialog = new ContentDialog
                {
                    Title = "Campo requerido",
                    Content = "El nombre del usuario no puede estar vacío.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            if (string.IsNullOrWhiteSpace(llave))
            {
                var dialog = new ContentDialog
                {
                    Title = "Campo requerido",
                    Content = "La llave de acceso no puede estar vacía.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            bool success = await _configurationService.AddCertifiedUserAsync(nombre, llave);

            if (success)
            {
                // Limpiar campos
                TextBoxNuevoNombre.Text = string.Empty;
                TextBoxNuevaLlave.Text = string.Empty;

                // Recargar lista
                LoadCertifiedUsers();

                var successDialog = new ContentDialog
                {
                    Title = "Usuario agregado",
                    Content = $"El usuario '{nombre}' ha sido agregado correctamente.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            else
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "No se pudo agregar el usuario. Verifica que la llave no esté duplicada.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void BtnEditarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string userId)
            {
                var user = _configurationService.GetCertifiedUserById(userId);
                if (user == null)
                {
                    var notFoundDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Usuario no encontrado.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await notFoundDialog.ShowAsync();
                    return;
                }

                // Crear un panel con campos de entrada
                var stackPanel = new StackPanel { Spacing = 12 };
                
                var nombreTextBox = new TextBox
                {
                    PlaceholderText = "Nombre del usuario",
                    Text = user.Nombre,
                    MaxLength = 50
                };
                
                var llaveTextBox = new TextBox
                {
                    PlaceholderText = "Llave de acceso",
                    Text = user.Llave,
                    MaxLength = 100
                };

                stackPanel.Children.Add(new TextBlock 
                { 
                    Text = "Nombre:", 
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    Margin = new Thickness(0, 0, 0, 4)
                });
                stackPanel.Children.Add(nombreTextBox);
                
                stackPanel.Children.Add(new TextBlock 
                { 
                    Text = "Llave:", 
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    Margin = new Thickness(0, 8, 0, 4)
                });
                stackPanel.Children.Add(llaveTextBox);

                var editDialog = new ContentDialog
                {
                    Title = "Editar usuario certificado",
                    Content = stackPanel,
                    PrimaryButtonText = "Guardar",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await editDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    string nuevoNombre = nombreTextBox.Text.Trim();
                    string nuevaLlave = llaveTextBox.Text.Trim();

                    if (string.IsNullOrWhiteSpace(nuevoNombre) || string.IsNullOrWhiteSpace(nuevaLlave))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Campos requeridos",
                            Content = "El nombre y la llave no pueden estar vacíos.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }

                    bool success = await _configurationService.UpdateCertifiedUserAsync(userId, nuevoNombre, nuevaLlave);

                    if (success)
                    {
                        LoadCertifiedUsers();

                        var successDialog = new ContentDialog
                        {
                            Title = "Usuario actualizado",
                            Content = $"Los datos de '{nuevoNombre}' han sido actualizados correctamente.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "No se pudo actualizar el usuario. Verifica que la llave no esté duplicada.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        private async void BtnEliminarUsuario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string userId)
            {
                var user = _configurationService.GetCertifiedUserById(userId);
                if (user == null)
                {
                    var notFoundDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Usuario no encontrado.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await notFoundDialog.ShowAsync();
                    return;
                }

                var confirmDialog = new ContentDialog
                {
                    Title = "Confirmar eliminación",
                    Content = $"¿Estás seguro de que deseas eliminar al usuario '{user.Nombre}'? Esta acción no se puede deshacer.",
                    PrimaryButtonText = "Eliminar",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.Content.XamlRoot,
                    DefaultButton = ContentDialogButton.Close
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    bool success = await _configurationService.DeleteCertifiedUserAsync(userId);

                    if (success)
                    {
                        LoadCertifiedUsers();

                        var successDialog = new ContentDialog
                        {
                            Title = "Usuario eliminado",
                            Content = $"El usuario '{user.Nombre}' ha sido eliminado correctamente.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "No se pudo eliminar el usuario. Inténtalo de nuevo.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
            }
        }

        private class ThemeOption
        {
            public Configuration.PaletaColoresSeleccionada Value { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
