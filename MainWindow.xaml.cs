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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NexChat
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private ChatService _chatService;
        private CloudflaredService _cloudflaredService;
        private ChatConnectorService _chatConnectorService;
        public ObservableCollection<Chat> ChatItems { get; set; }
        private Chat _selectedChat;
        private string _currentUserId = "USER_LOCAL"; // ID del usuario actual
        private bool _cloudflareNeedsUpdate = false;

        public MainWindow()
        {
            InitializeComponent();
            ChatItems = new ObservableCollection<Chat>();
            _chatConnectorService = new ChatConnectorService();
            _cloudflaredService = new CloudflaredService();
            _chatService = CreateChatService();
            _chatService.ChatListUpdated += _chatService_ChatListUpdated;
            // Invocar manualmente la actualización después de suscribirse
            _chatService.UpdateHandlerChats();

            // Verificar estado de Cloudflare
            _ = CheckCloudflareStatus();
        }

        private async System.Threading.Tasks.Task CheckCloudflareStatus()
        {
            try
            {
                Console.WriteLine("Checking Cloudflare status...");
                
                // Verificar si necesita actualización
                _cloudflareNeedsUpdate = await _cloudflaredService.NeedsUpdate();
                
                // Actualizar UI en el hilo principal
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCloudflareButton();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking Cloudflare status: {ex.Message}");
                
                // En caso de error, mostrar botón con estado de error
                DispatcherQueue.TryEnqueue(() =>
                {
                    var content = this.Content as FrameworkElement;
                    if (content == null) return;

                    var button = content.FindName("CloudflareUpdateButton") as Button;
                    var textBlock = content.FindName("CloudflareButtonText") as TextBlock;
                    
                    if (button != null) button.IsEnabled = false;
                    if (textBlock != null) textBlock.Text = "Error al verificar";
                });
            }
        }

        private void UpdateCloudflareButton()
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var button = content.FindName("CloudflareUpdateButton") as Button;
            var textBlock = content.FindName("CloudflareButtonText") as TextBlock;
            
            if (button == null || textBlock == null) return;

            if (_cloudflareNeedsUpdate)
            {
                // Hay actualización disponible o no está descargado
                button.IsEnabled = true;
                textBlock.Text = _cloudflaredService.IsExecutablePresent() 
                    ? "Actualizar Cloudflare" 
                    : "Descargar Cloudflare";
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
            else
            {
                // Ya está actualizado
                button.IsEnabled = false;
                textBlock.Text = "Cloudflare actualizado";
            }
        }

        private async void CloudflareUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var button = content.FindName("CloudflareUpdateButton") as Button;
            var textBlock = content.FindName("CloudflareButtonText") as TextBlock;
            
            if (button == null || textBlock == null) return;

            // Deshabilitar el botón mientras se descarga
            button.IsEnabled = false;
            textBlock.Text = "Descargando...";

            try
            {
                bool success = await _cloudflaredService.DownloadExecutable();
                
                if (success)
                {
                    _cloudflareNeedsUpdate = false;
                    textBlock.Text = "Cloudflare actualizado";
                    
                    // Mostrar notificación de éxito
                    var dialog = new ContentDialog
                    {
                        Title = "Descarga completada",
                        Content = "Cloudflare se ha descargado/actualizado correctamente.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    textBlock.Text = "Error en descarga";
                    button.IsEnabled = true;
                    
                    // Mostrar error
                    var dialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "No se pudo descargar Cloudflare. Verifica tu conexión a internet.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading Cloudflare: {ex.Message}");
                textBlock.Text = "Error en descarga";
                button.IsEnabled = true;
                
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Error al descargar Cloudflare: {ex.Message}",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void _chatService_ChatListUpdated(object? sender, List<Data.Chat> e)
        {
            ChatItems.Clear();
            foreach (var chat in e)
            {
                ChatItems.Add(chat);
            }
        }

        private ChatService CreateChatService(){
            return new ChatService(_cloudflaredService, _chatConnectorService);
        }

        private void ChatListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            var selectedChat = listView?.SelectedItem as Chat;
            if (selectedChat != null)
            {
                _selectedChat = selectedChat;
                LoadChatView(selectedChat);
            }
        }

        private void LoadChatView(Chat chat)
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            // Obtener referencias a los controles usando FindName
            var chatViewPanel = content.FindName("ChatViewPanel") as Grid;
            var emptyChatPanel = content.FindName("EmptyChatPanel") as Grid;
            var chatHeaderName = content.FindName("ChatHeaderName") as TextBlock;
            var chatHeaderInfo = content.FindName("ChatHeaderInfo") as TextBlock;

            if (chatViewPanel == null || emptyChatPanel == null || chatHeaderName == null || chatHeaderInfo == null)
                return;

            // Mostrar panel de chat y ocultar empty state
            chatViewPanel.Visibility = Visibility.Visible;
            emptyChatPanel.Visibility = Visibility.Collapsed;

            // Actualizar header del chat
            chatHeaderName.Text = chat.Name;
            chatHeaderInfo.Text = $"Code: {chat.CodeInvitation ?? "Sin código"}";

            // Cargar mensajes
            LoadMessages(chat.Messages);

            // Scroll al final
            ScrollToBottom();
        }

        private void LoadMessages(List<Message> messages)
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var messagesPanel = content.FindName("MessagesPanel") as StackPanel;
            if (messagesPanel == null) return;

            messagesPanel.Children.Clear();
            
            foreach (var message in messages)
            {
                AddMessageToUI(message);
            }
        }

        private void AddMessageToUI(Message message)
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var messagesPanel = content.FindName("MessagesPanel") as StackPanel;
            if (messagesPanel == null) return;

            bool isMyMessage = message.Sender.Id == _currentUserId;

            // Grid contenedor para alineación
            var messageGrid = new Grid
            {
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = isMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            // Border para la burbuja del mensaje
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 600,
                Background = isMyMessage 
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
            };

            // StackPanel para contenido del mensaje
            var contentStack = new StackPanel
            {
                Spacing = 4
            };

            // Nombre del remitente (solo para mensajes recibidos)
            if (!isMyMessage && !string.IsNullOrEmpty(message.Sender.Name))
            {
                var senderName = new TextBlock
                {
                    Text = message.Sender.Name,
                    FontSize = 12,
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                };
                contentStack.Children.Add(senderName);
            }

            // Contenido del mensaje
            var contentText = new TextBlock
            {
                Text = message.Content,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            };
            contentStack.Children.Add(contentText);

            // Timestamp
            var timestamp = new TextBlock
            {
                Text = message.Timestamp.ToLocalTime().ToString("HH:mm"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Right
            };
            contentStack.Children.Add(timestamp);

            bubble.Child = contentStack;
            messageGrid.Children.Add(bubble);
            messagesPanel.Children.Add(messageGrid);
        }

        private void MessageInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SendMessage();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var messageInputBox = content.FindName("MessageInputBox") as TextBox;
            if (messageInputBox == null) return;

            if (_selectedChat == null || string.IsNullOrWhiteSpace(messageInputBox.Text))
                return;

            string messageContent = messageInputBox.Text;
            
            //TODO: Implementar lógica de envío de mensaje a través del ChatService a la red
            var sender = new Sender(_currentUserId) { Name = "Yo" };
            var message = new Message(_selectedChat, sender, messageContent);
            
            // Agregar mensaje al chat usando el servicio
            _chatService.AddMessage(_selectedChat.Id, message);
            
            // Mostrar en la UI
            AddMessageToUI(message);

            // Limpiar input
            messageInputBox.Text = string.Empty;

            // Scroll al final
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            //TODO: Implementar scroll automático al final cuando se carguen o agreguen mensajes
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var messagesScrollViewer = content.FindName("MessagesScrollViewer") as ScrollViewer;
            if (messagesScrollViewer == null) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                messagesScrollViewer.ChangeView(null, messagesScrollViewer.ScrollableHeight, null, false);
            });
        }

        private void ChatListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Ya no se usa, ahora usamos SelectionChanged
        }

        private async void BtnUnirseNuevoChat_Click(object sender, RoutedEventArgs e)
        {
            var textBox = new TextBox();
            var dialog = new ContentDialog
            {
                Title = "Introduce codigo sala",
                Content = textBox,
                PrimaryButtonText = "Aceptar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string userInput = textBox.Text;
                // aquí puedes usar la variable userInput
                if (!await _chatService.JoinChat(userInput))
                {
                    // Mostrar notificación de éxito
                    var dialogErrorConnectChat = new ContentDialog
                    {
                        Title = "No se pudo unir al chat",
                        Content = "Verifica que el código sea correcto y que el chat esté disponible.",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await dialogErrorConnectChat.ShowAsync();
                }
            }
        }

        private async void BtnCrearNuevoChat_Click(object sender, RoutedEventArgs e)
        {
            //pedir a usuario input de string para nombre
            var textBox = new TextBox();
            var dialog = new ContentDialog
            {
                Title = "Introduce tu nombre",
                Content = textBox,
                PrimaryButtonText = "Aceptar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string userInput = textBox.Text;
                // aquí puedes usar la variable userInput
                _chatService.CreateChat(userInput);
            }
        }

        private void ChatItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var grid = sender as Grid;
            if (grid != null)
            {
                var flyout = FlyoutBase.GetAttachedFlyout(grid) as MenuFlyout;
                if (flyout != null)
                {
                    flyout.ShowAt(grid, e.GetPosition(grid));
                }
            }
        }

        private async void EditChat_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is string chatId)
            {
                var textBox = new TextBox();
                var dialog = new ContentDialog
                {
                    Title = "Editar nombre del chat",
                    Content = textBox,
                    PrimaryButtonText = "Aceptar",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    string newName = textBox.Text;
                    _chatService.EditChat(chatId, newName);
                }
            }
        }

        private async void DeleteChat_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is string chatId)
            {
                var dialog = new ContentDialog
                {
                    Title = "Eliminar chat",
                    Content = "¿Estás seguro de que deseas eliminar este chat?",
                    PrimaryButtonText = "Eliminar",
                    CloseButtonText = "Cancelar",
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _chatService.DeleteChat(chatId);
                }
            }
        }

        private async void PlayChat_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is string chatId)
            {
                // Por defecto, no habilitar túnel. Puedes cambiarlo a true si deseas
                // habilitar túnel automáticamente cuando se inicia el servidor
                bool enableTunnel = true;
                await _chatService.StartWebServer(chatId, enableTunnel);
            }
        }

        private async void StopChat_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is string chatId)
            {
                await _chatService.StopWebServer(chatId);
            }
        }

        private void ChatContextMenu_Opening(object sender, object e)
        {
            var menuFlyout = sender as MenuFlyout;
            if (menuFlyout == null) return;

            // Obtener el chatId desde el contexto del menú
            var grid = menuFlyout.Target as Grid;
            if (grid == null) return;

            var chatItem = grid.DataContext as Chat;
            if (chatItem == null) return;

            // Buscar los MenuFlyoutItems por nombre
            MenuFlyoutItem playMenuItem = null;
            MenuFlyoutItem stopMenuItem = null;
            MenuFlyoutItem copyURLMenuItem = null;

            foreach (var item in menuFlyout.Items)
            {
                if (item is MenuFlyoutItem menuItem)
                {
                    if (menuItem.Text == "Play")
                        playMenuItem = menuItem;
                    else if (menuItem.Text == "Stop")
                        stopMenuItem = menuItem;
                    else if (menuItem.Text == "Copy Direction Code")
                        copyURLMenuItem = menuItem;
                }
            }

            // Mostrar/ocultar según el estado
            if (playMenuItem != null)
                playMenuItem.Visibility = chatItem.IsRunning ? Visibility.Collapsed : Visibility.Visible;
            
            if (stopMenuItem != null)
                stopMenuItem.Visibility = chatItem.IsRunning ? Visibility.Visible : Visibility.Collapsed;
            
            // Mostrar CopyURLMenuItem solo si CodeInvitation no es null y IsInvited es false
            if (copyURLMenuItem != null)
                copyURLMenuItem.Visibility = (!string.IsNullOrEmpty(chatItem.CodeInvitation) && !chatItem.IsInvited) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
        }

        private void CopyURLMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is string chatId)
            {
                // Copiar al portapapeles la URL del chat
                var chat = ChatItems.FirstOrDefault(c => c.Id == chatId);
                if (chat == null) return;
                if (chat.ServerPort.HasValue)
                {
                    string url = $"{chat.CodeInvitation}";
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(url);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                }
            }
        }
    }
}
