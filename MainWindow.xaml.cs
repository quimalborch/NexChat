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
        public ObservableCollection<Chat> ChatItems { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ChatItems = new ObservableCollection<Chat>();
            _chatService = CreateChatService();
            _chatService.ChatListUpdated += _chatService_ChatListUpdated;
            // Invocar manualmente la actualización después de suscribirse
            _chatService.UpdateHandlerChats();
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
            return new ChatService();
        }

        private void ChatListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var selectedChat = e.ClickedItem as Chat;
            if (selectedChat != null)
            {
                // Aquí puedes hacer algo cuando se selecciona un chat
                // Por ejemplo, navegar a una página de chat o cargar los mensajes
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
    }
}
