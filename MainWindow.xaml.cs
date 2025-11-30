using Microsoft.UI;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Serilog;

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
        private ConfigurationService _configurationService;
        public ObservableCollection<Chat> ChatItems { get; set; }
        private Chat _selectedChat;
        private string _currentUserId;
        private bool _cloudflareNeedsUpdate = false;
        private NexChat.Services.UpdateManager _updateManager;
        private returnMessageUpdateInfo _updateManagerResponse;

        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            ChatItems = new ObservableCollection<Chat>();

            _configurationService = new ConfigurationService();
            _chatConnectorService = new ChatConnectorService();
            _cloudflaredService = new CloudflaredService();
            _chatService = CreateChatService();
            _updateManager = CreateUpdateManager();
            _updateManagerResponse = new returnMessageUpdateInfo(false);

            _currentUserId = _configurationService.GetUserId();

            _chatService.ChatListUpdated += _chatService_ChatListUpdated;
            // Invocar manualmente la actualización después de suscribirse
            _chatService.UpdateHandlerChats();

            // Verificar estado de Cloudflare
            _ = CheckCloudflareStatus();

            this.Activated += MainWindow_Activated;
            
            // Suscribirse al evento de cierre de ventana
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                Log.Information("MainWindow closing - cleaning up resources");
                
                // Limpiar ChatService (esto a su vez limpiará CloudflaredService)
                if (_chatService is IDisposable disposableChat)
                {
                    disposableChat.Dispose();
                }
                
                // Cleanup extra de procesos cloudflared por si acaso
                CleanupCloudflaredProcesses();
                
                Log.Information("MainWindow cleanup completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during MainWindow cleanup");
            }
        }

        private void CleanupCloudflaredProcesses()
        {
            try
            {
                var cloudflaredProcesses = System.Diagnostics.Process.GetProcessesByName("cloudflared");
                
                if (cloudflaredProcesses.Length > 0)
                {
                    Log.Warning("Found {Count} cloudflared process(es) during window close. Cleaning up...", 
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
                            Log.Warning(ex, "Could not kill cloudflared process during cleanup");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cleaning up cloudflared processes");
            }
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
                button.Visibility = Visibility.Visible;
                textBlock.Text = _cloudflaredService.IsExecutablePresent()
                    ? "Actualizar Cloudflare"
                    : "Descargar Cloudflare";
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
            else
            {
                // Ya está actualizado
                button.IsEnabled = false;
                button.Visibility = Visibility.Collapsed;
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
            // Dispatch to UI thread since this can be called from background threads (WebSocket)
            DispatcherQueue.TryEnqueue(() =>
            {
                Console.WriteLine($"🔄 ChatListUpdated event triggered");

                // Primero, verificar si hay mensajes nuevos para el chat seleccionado
                if (_selectedChat != null)
                {
                    var updatedChat = e.FirstOrDefault(c => c.Id == _selectedChat.Id);
                    if (updatedChat != null)
                    {
                        Console.WriteLine($"📱 Checking for new messages in selected chat '{updatedChat.Name}'");
                        Console.WriteLine($"   Current chat has {_selectedChat.Messages.Count} messages");
                        Console.WriteLine($"   Updated chat has {updatedChat.Messages.Count} messages");

                        // Obtener el MessagesPanel para verificar qué mensajes ya están en la UI
                        var content = this.Content as FrameworkElement;
                        var messagesPanel = content?.FindName("MessagesPanel") as StackPanel;
                        var messagesScrollViewer = content?.FindName("MessagesScrollViewer") as ScrollViewer;

                        if (messagesPanel != null && messagesScrollViewer != null)
                        {
                            // Verificar si el usuario está al final del scroll ANTES de agregar mensajes
                            bool wasAtBottom = IsScrolledToBottom(messagesScrollViewer);

                            Console.WriteLine($"   User is at bottom: {wasAtBottom}");

                            // Obtener IDs de mensajes que ya están en la UI
                            var existingMessageIds = new HashSet<string>();
                            foreach (var child in messagesPanel.Children)
                            {
                                if (child is Grid grid && grid.Tag is string messageId)
                                {
                                    existingMessageIds.Add(messageId);
                                }
                            }

                            Console.WriteLine($"   UI currently has {existingMessageIds.Count} messages displayed");

                            // Agregar solo mensajes que NO están en la UI
                            bool newMessagesAdded = false;
                            foreach (var message in updatedChat.Messages)
                            {
                                if (!existingMessageIds.Contains(message.Id))
                                {
                                    Console.WriteLine($"   ✅ Adding new message to UI: {message.Content.Substring(0, Math.Min(30, message.Content.Length))}...");
                                    AddMessageToUI(message);
                                    newMessagesAdded = true;
                                }
                            }

                            // Actualizar la referencia del _selectedChat con los nuevos mensajes
                            _selectedChat = updatedChat;

                            // Auto-scroll al final SOLO si el usuario estaba al final Y se agregaron mensajes nuevos
                            if (newMessagesAdded && wasAtBottom)
                            {
                                Console.WriteLine($"   📜 Auto-scrolling to bottom (user was at bottom)");
                                // Usar un pequeño delay para que el layout se actualice
                                _ = ScrollToBottomWithDelay();
                            }
                            else if (newMessagesAdded && !wasAtBottom)
                            {
                                Console.WriteLine($"   📜 NOT auto-scrolling (user is reading old messages)");
                            }
                        }
                    }
                }

                // Actualizar la lista de chats en el sidebar
                ChatItems.Clear();
                foreach (var chat in e)
                {
                    ChatItems.Add(chat);
                }

                Console.WriteLine($"✓ ChatList updated with {e.Count} chats");
            });
        }

        /// <summary>
        /// Verifica si el ScrollViewer está scrolleado hasta el final (con un margen de tolerancia)
        /// </summary>
        private bool IsScrolledToBottom(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                return false;

            // Obtener posición actual del scroll
            double verticalOffset = scrollViewer.VerticalOffset;
            double scrollableHeight = scrollViewer.ScrollableHeight;

            // Considerar que está al final si está dentro de 10 pixels del fondo
            // Esto da un margen de tolerancia para evitar problemas de redondeo
            const double tolerance = 10.0;

            bool isAtBottom = (scrollableHeight - verticalOffset) <= tolerance;

            Console.WriteLine($"   📏 Scroll position - Offset: {verticalOffset:F2}, Scrollable: {scrollableHeight:F2}, IsAtBottom: {isAtBottom}");

            return isAtBottom;
        }

        /// <summary>
        /// Hace scroll al final con un pequeño delay para que el layout se actualice
        /// </summary>
        private async Task ScrollToBottomWithDelay()
        {
            // Esperar a que el layout se actualice
            await Task.Delay(50);

            DispatcherQueue.TryEnqueue(() =>
            {
                var content = this.Content as FrameworkElement;
                if (content == null) return;

                var messagesScrollViewer = content.FindName("MessagesScrollViewer") as ScrollViewer;
                if (messagesScrollViewer == null) return;

                // Forzar actualización del layout
                messagesScrollViewer.UpdateLayout();

                // Hacer scroll al final
                messagesScrollViewer.ChangeView(null, messagesScrollViewer.ScrollableHeight, null, false);

                Console.WriteLine($"   ✓ Scrolled to bottom - New height: {messagesScrollViewer.ScrollableHeight:F2}");
            });
        }

        private ChatService CreateChatService()
        {
            return new ChatService(_cloudflaredService, _chatConnectorService);
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (!_isInitialized && e.WindowActivationState != WindowActivationState.Deactivated)
            {
                Log.Information("MainWindow activated - starting initialization");
                _isInitialized = true;

                // Desuscribirse para que solo se ejecute una vez
                this.Activated -= MainWindow_Activated;

                await InitializeAsync();
            }
        }

        private NexChat.Services.UpdateManager CreateUpdateManager()
        {
            return new NexChat.Services.UpdateManager();
        }

        private async Task<returnMessageUpdateInfo> LoadUpdateManagerResponse()
        {
            return await _updateManager.CheckActualizacionDisponible();
        }

        private async Task InitializeAsync()
        {
            try
            {

                Log.Information("=== Initializing MainWindow ===");

                Log.Debug("Loading UpdateManager response...");
                _updateManagerResponse = await LoadUpdateManagerResponse();
                Log.Debug("UpdateManager response loaded: UpdateAvailable={UpdateAvailable}, Version={Version}, Error={Error}", 
                    _updateManagerResponse.updateAvaliable, 
                    _updateManagerResponse.version ?? "null", 
                    _updateManagerResponse.messageError ?? "null");

                await CheckVersionAsync();

                Log.Information("MainWindow initialization completed successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fatal error during MainWindow initialization");
                throw;
            }
        }

        private async Task CheckVersionAsync()
        {
            try
            {
                Log.Information("Checking for application updates...");
                
                _updateManagerResponse = await _updateManager.CheckActualizacionDisponible();
                
                Log.Information("Update check completed: UpdateAvailable={UpdateAvailable}, Version={Version}", 
                    _updateManagerResponse.updateAvaliable, 
                    _updateManagerResponse.version ?? "null");

                if (_updateManagerResponse.messageError != null)
                {
                    Log.Warning("Update check returned error: {Error}", _updateManagerResponse.messageError);
                }

                if (!_updateManagerResponse.updateAvaliable) 
                {
                    Log.Information("No updates available, hiding update button");
                    return;
                }

                Log.Information("Update available! Showing update button in UI");
                
                DispatcherQueue.TryEnqueue(() =>
                {
                    ButtonUpdateVersion.Visibility = Visibility.Visible;
                    TextButtonUpdateVersion.Text = $"Descargar actualización ({_updateManagerResponse.version})";
                    ToolTipUpdateVersionBtn.Content = $"Descargar nueva actualización ({_updateManagerResponse.version})";
                    
                    Log.Debug("Update button UI updated successfully");
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during version check");
            }
        }

        private async void ButtonUpdateVersion_Click(object sender, RoutedEventArgs e)
        {
            Log.Information("=== Update button clicked by user ===");
            Log.Information("Target update version: {Version}", _updateManagerResponse.version);
            
            ContentDialog confirmUpdateVersionDialog = new ContentDialog
            {
                Title = $"¿Estás completamente seguro de que deseas actualizar a la versión ({_updateManagerResponse.version})?",
                Content = "Esta acción reemplazará la versión que estás usando ahora mismo y aplicará todos los cambios incluidos en la nueva actualización.",
                PrimaryButtonText = $"Actualizar a la nueva versión",
                CloseButtonText = "Cancelar",
                XamlRoot = this.Content.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            ContentDialogResult result = await confirmUpdateVersionDialog.ShowAsync();

            if (result != ContentDialogResult.Primary)
            {
                Log.Information("User cancelled the update");
                return;
            }

            Log.Information("User confirmed update - starting download and installation");

            try
            {
                ButtonUpdateVersion.IsEnabled = false;
                TextButtonUpdateVersion.Text = "Descargando actualización...";
                
                Log.Information("Calling ForceUpdate...");
                await _updateManager.ForceUpdate();
                
                // Este código probablemente nunca se ejecutará porque ForceUpdate reinicia la app
                Log.Warning("ForceUpdate completed without restarting app - this is unexpected");
                
                ButtonUpdateVersion.IsEnabled = true;
                TextButtonUpdateVersion.Text = $"Descargar actualización ({_updateManagerResponse.version})";
                ToolTipUpdateVersionBtn.Content = $"Descargar actualización ({_updateManagerResponse.version})";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during update process in UI");
                
                ButtonUpdateVersion.IsEnabled = true;
                TextButtonUpdateVersion.Text = $"Error - Reintentar ({_updateManagerResponse.version})";
                
                // Mostrar diálogo de error
                var errorDialog = new ContentDialog
                {
                    Title = "Error al actualizar",
                    Content = $"No se pudo completar la actualización: {ex.Message}",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
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

            if (!chat.IsInvited)
            {
                LoadMessages(chat.Messages);
            }
            else
            {
                if (chat.CodeInvitation is null) return;
                LoadMessagesRemote(chat.CodeInvitation);
            }

            // Scroll al final
            ScrollToBottom();
        }

        private void LoadMessagesRemote(string ConnectionCode)
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            var messagesPanel = content.FindName("MessagesPanel") as StackPanel;
            if (messagesPanel == null) return;

            messagesPanel.Children.Clear();

            _chatConnectorService.GetChat(ConnectionCode).ContinueWith(chatTask =>
            {
                var chat = chatTask.Result;
                if (chat == null) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadMessages(chat.Messages);
                    ScrollToBottom();
                });
            });

            //foreach (var message in messages)
            //{
            //    AddMessageToUI(message);
            //}
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

            string currentUserHashed = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(_configurationService.GetOrCreateConfiguration().idUsuario)))
;
            bool isMyMessage = message.Sender.Id == currentUserHashed;

            bool isCertified = _configurationService.IsCertifiedUser(message.Sender.Id);

            // Grid contenedor para alineación
            var messageGrid = new Grid
            {
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = isMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Tag = message.Id
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

            // Nombre del remitente con icono de verificación (solo para mensajes recibidos)
            if (!isMyMessage && !string.IsNullOrEmpty(message.Sender.Name))
            {
                // StackPanel horizontal para nombre + icono de verificación
                var senderHeaderStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6
                };

                string SenderNameLocal = message.Sender.Name;

                if (isCertified && _configurationService is not null)
                {
                    var certifiedUser = _configurationService.GetCertifiedNameByKey(message.Sender.Id);
                    if (certifiedUser is not null)
                    {
                        SenderNameLocal = certifiedUser.Nombre;
                    }
                }

                var senderName = new TextBlock
                {
                    Text = SenderNameLocal,
                    FontSize = 12,
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                senderHeaderStack.Children.Add(senderName);

                // Agregar icono de verificación si el usuario está certificado
                if (isCertified)
                {
                    // Border circular azul para el fondo del icono
                    var verifiedBorder = new Border
                    {
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 29, 155, 240)), // Azul similar a Twitter/X
                        CornerRadius = new CornerRadius(10),
                        Width = 16,
                        Height = 16,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // FontIcon de checkmark blanco
                    var verifiedIcon = new FontIcon
                    {
                        Glyph = "\uE73E", // Checkmark con círculo
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Agregar el icono dentro del border
                    verifiedBorder.Child = verifiedIcon;

                    // Agregar tooltip
                    ToolTipService.SetToolTip(verifiedBorder, "Verificado");

                    senderHeaderStack.Children.Add(verifiedBorder);
                }

                contentStack.Children.Add(senderHeaderStack);
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
                SendMessageStarter();
                e.Handled = true;
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessageStarter();
        }

        private void SendMessageStarter()
        {
            var content = this.Content as FrameworkElement;
            if (content == null) return;

            TextBox? messageInputBox = content.FindName("MessageInputBox") as TextBox;
            if (messageInputBox is null) return;

            if (_selectedChat is null || string.IsNullOrWhiteSpace(messageInputBox.Text))
                return;

            string messageContent = messageInputBox.Text;

            string senderId = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(_configurationService.GetOrCreateConfiguration().idUsuario)));

            //TODO: Implementar lógica de envío de mensaje a través del ChatService a la red
            var _sender = new Sender(senderId) { Name = RecuperarName() };
            var message = new Message(_selectedChat, _sender, messageContent);

            if (!_selectedChat.IsInvited)
            {
                SendMessage(messageInputBox, message);
            }
            else
            {
                SendMessageExternal(messageInputBox, message);
            }
        }

        private string RecuperarName()
        {
            if (!_selectedChat.IsInvited)
            {
                return _configurationService.GetUserName();
            }
            else
            {
                return _configurationService.GetUserName();
            }
        }

        private async void SendMessageExternal(TextBox messageInputBox, Message message)
        {
            if (_selectedChat.CodeInvitation is null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error al enviar mensaje",
                    Content = "No se ha encontrado el codigo de invitación.",
                    CloseButtonText = "Aceptar",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
                return;
            }

            // Usar AddMessage del ChatService que maneja WebSocket automáticamente
            await _chatService.AddMessage(_selectedChat.Id, message);

            // Limpiar input después de enviar
            messageInputBox.Text = string.Empty;

            Console.WriteLine($"✓ Message sent to ChatService, waiting for server confirmation");
        }

        private void SendMessage(TextBox messageInputBox, Message message)
        {
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
                Log.Information("Starting chat server for ChatId: {ChatId}", chatId);
                
                // Buscar el chat en la lista
                var chat = ChatItems.FirstOrDefault(c => c.Id == chatId);
                if (chat == null)
                {
                    Log.Warning("Chat not found in ChatItems: {ChatId}", chatId);
                    return;
                }
                
                // Establecer el estado de loading (se actualiza automáticamente en la UI por INotifyPropertyChanged)
                chat.IsStarting = true;
                Log.Debug("Loading state set for chat: {ChatName}", chat.Name);
                
                try
                {
                    // Por defecto, habilitar túnel automáticamente cuando se inicia el servidor
                    bool enableTunnel = true;
                    bool success = await _chatService.StartWebServer(chatId, enableTunnel);
                    
                    if (!success)
                    {
                        Log.Error("Failed to start web server for ChatId: {ChatId}", chatId);
                        
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "No se pudo iniciar la conexión privada con Cloudflare.",
                            CloseButtonText = "Aceptar",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                    else
                    {
                        Log.Information("Web server started successfully for ChatId: {ChatId}", chatId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception while starting chat server for ChatId: {ChatId}", chatId);
                    
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Error al iniciar el servidor: {ex.Message}",
                        CloseButtonText = "Aceptar",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
                finally
                {
                    // Quitar el estado de loading (se actualiza automáticamente en la UI)
                    chat.IsStarting = false;
                    Log.Debug("Loading state cleared for chat: {ChatName}", chat.Name);
                }
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
                playMenuItem.Visibility = (chatItem.IsRunning || chatItem.IsInvited) ? Visibility.Collapsed : Visibility.Visible;

            if (stopMenuItem != null)
                stopMenuItem.Visibility = (!chatItem.IsRunning || chatItem.IsInvited) ? Visibility.Collapsed : Visibility.Visible;

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

        private void BtnConfig_Click(object sender, RoutedEventArgs e)
        {
            ConfigWindow ventanaConfig = new ConfigWindow(this, _configurationService);
            ventanaConfig.Title = "Configuración";
            ventanaConfig.Activate();
            this.AppWindow.Hide();
        }
    }
}