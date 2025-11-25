using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NexChat.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private Usuario? _usuario;
        public ConfigWindow(MainWindow ventanaPrincipal)
        {
            InitializeComponent();
            _ventanaPrincipal = ventanaPrincipal;
            LoadUsuario();
            this.AppWindow.Closing += (_, __) => _ventanaPrincipal.AppWindow.Show();
        }

        private void BtnIdentidad_Click(object sender, RoutedEventArgs e)
        {
            IdentidadContent.Visibility = Visibility.Visible;
        }

        private void TextBoxIdentidad_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxIdentidad.Text.Length > 0)
            {
                CheckConfirmarNewIdentidad.Visibility = Visibility.Visible;
            }
            else
            {
                CheckConfirmarNewIdentidad.Visibility = Visibility.Collapsed;
            }
        }

        private async void CheckConfirmarNewIdentidad_Click(object sender, RoutedEventArgs e)
        {
            if (CheckConfirmarNewIdentidad.IsChecked == true)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Confirmar cambio de nombre de usuario",
                    Content = $"¿Está seguro de que desea cambiar su nombre de usuario a '{TextBoxIdentidad.Text}'?",
                    PrimaryButtonText = "Confirmar",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.Content.XamlRoot
                };

                ContentDialogResult result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    string nombreUsuario = TextBoxIdentidad.Text;

                    if (_usuario is null)
                    {
                        Usuario nuevoUsuario = new Usuario(nombreUsuario);
                        SaveUsuario(nuevoUsuario);
                    }
                    else 
                    { 
                        _usuario.nombreUsuario = nombreUsuario;
                    }

                    TextGuildUsuario.Text = _usuario.idUsuario.ToString();
                }
                else
                {
                    TextBoxIdentidad.Text = string.Empty;
                    CheckConfirmarNewIdentidad.IsChecked = false;
                    CheckConfirmarNewIdentidad.Visibility = Visibility.Collapsed;
                    IdentidadContent.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void SaveUsuario(Usuario usuario) 
        {
            string path = LoadUsuarioPath();
            string directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(usuario);
            File.WriteAllText(path, json);
        }

        public string LoadUsuarioPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "User"
            );
            
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return Path.Combine(folder, "user.ncf");
        }

        public void LoadUsuario() 
        {
            LoadUsuarioInterno();
            RecuperarUsuarioUI();
        }

        public void LoadUsuarioInterno() 
        {
            string path = LoadUsuarioPath();
            string? directory = Path.GetDirectoryName(path);

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            if (!File.Exists(path))
            {
                using (File.Create(path)) { }
            }

            string conteindoJSON = File.ReadAllText(path);
            if (string.IsNullOrEmpty(conteindoJSON))
            {
                return;
            }

            _usuario = JsonSerializer.Deserialize<Usuario>(conteindoJSON);
        }

        public void RecuperarUsuarioUI()
        {
            if (_usuario is null) return;

            TextGuildUsuario.Text = _usuario.idUsuario.ToString();
        }
    }
}
