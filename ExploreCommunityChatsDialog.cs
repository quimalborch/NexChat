using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NexChat.Data;
using NexChat.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace NexChat
{
    public class ExploreCommunityChatsDialog
    {
        private readonly ChatService _chatService;
        private readonly XamlRoot _xamlRoot;
        private List<CommunityChat> _communityChats;
        private CommunityChat _selectedChat;
        private StackPanel _chatsPanel;
        private Button _joinButton;
        private int _currentPage = 1;
        private int _pageSize = 10;
        private PaginationInfo _paginationInfo;
        private Button _prevPageButton;
        private Button _nextPageButton;
        private TextBlock _pageInfoText;

        public ExploreCommunityChatsDialog(ChatService chatService, XamlRoot xamlRoot)
        {
            _chatService = chatService;
            _xamlRoot = xamlRoot;
            _communityChats = new List<CommunityChat>();
        }

        public async Task<CommunityChat> ShowAsync()
        {
            try
            {
                var content = BuildContent();
                
                var dialog = new ContentDialog
                {
                    Title = "Explorar Chats Públicos",
                    Content = content,
                    PrimaryButtonText = "Unirse",
                    CloseButtonText = "Cancelar",
                    XamlRoot = _xamlRoot,
                    DefaultButton = ContentDialogButton.Primary
                };

                _joinButton = new Button();
                dialog.PrimaryButtonClick += (s, args) =>
                {
                    if (_selectedChat == null)
                    {
                        args.Cancel = true;
                    }
                };

                // Cargar los chats en segundo plano
                _ = LoadCommunityChatsAsync();

                var result = await dialog.ShowAsync();
                
                if (result == ContentDialogResult.Primary && _selectedChat != null)
                {
                    return _selectedChat;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing ExploreCommunityChatsDialog");
                return null;
            }
        }

        private ScrollViewer BuildContent()
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 400,
                MinWidth = 500
            };

            var mainStack = new StackPanel
            {
                Spacing = 12
            };

            var descriptionText = new TextBlock
            {
                Text = "Selecciona un chat de la comunidad para unirte",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            _chatsPanel = new StackPanel
            {
                Spacing = 8,
                Padding = new Thickness(12)
            };

            // Mostrar indicador de carga inicial
            var loadingRing = new ProgressRing
            {
                IsActive = true,
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 24)
            };
            _chatsPanel.Children.Add(loadingRing);

            mainStack.Children.Add(descriptionText);
            mainStack.Children.Add(_chatsPanel);
            
            // Agregar controles de paginación
            var paginationPanel = BuildPaginationPanel();
            mainStack.Children.Add(paginationPanel);

            scrollViewer.Content = mainStack;

            return scrollViewer;
        }

        private StackPanel BuildPaginationPanel()
        {
            var paginationPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 12,
                Margin = new Thickness(0, 12, 0, 0)
            };

            _prevPageButton = new Button
            {
                Content = "◀ Anterior",
                IsEnabled = false
            };
            _prevPageButton.Click += async (s, e) => await NavigatePage(-1);

            _pageInfoText = new TextBlock
            {
                Text = "Página 1 de 1",
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 100,
                TextAlignment = TextAlignment.Center
            };

            _nextPageButton = new Button
            {
                Content = "Siguiente ▶",
                IsEnabled = false
            };
            _nextPageButton.Click += async (s, e) => await NavigatePage(1);

            paginationPanel.Children.Add(_prevPageButton);
            paginationPanel.Children.Add(_pageInfoText);
            paginationPanel.Children.Add(_nextPageButton);

            return paginationPanel;
        }

        private async Task LoadCommunityChatsAsync()
        {
            try
            {
                var response = await _chatService.GetCommunityChatsAsync(_currentPage, _pageSize);
                _communityChats = response.Data;
                _paginationInfo = response.Pagination;

                // Actualizar UI en el hilo principal
                if (_chatsPanel.DispatcherQueue != null)
                {
                    _chatsPanel.DispatcherQueue.TryEnqueue(() =>
                    {
                        BuildChatsList();
                        UpdatePaginationControls();
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading community chats");
                
                if (_chatsPanel.DispatcherQueue != null)
                {
                    _chatsPanel.DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowError($"Error al cargar los chats: {ex.Message}");
                    });
                }
            }
        }

        private async Task NavigatePage(int direction)
        {
            int newPage = _currentPage + direction;
            
            if (newPage < 1 || (_paginationInfo != null && newPage > _paginationInfo.TotalPages))
                return;

            _currentPage = newPage;
            
            // Mostrar indicador de carga
            _chatsPanel.Children.Clear();
            var loadingRing = new ProgressRing
            {
                IsActive = true,
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 24)
            };
            _chatsPanel.Children.Add(loadingRing);
            
            // Deshabilitar botones durante la carga
            _prevPageButton.IsEnabled = false;
            _nextPageButton.IsEnabled = false;
            
            await LoadCommunityChatsAsync();
        }

        private void UpdatePaginationControls()
        {
            if (_paginationInfo == null)
                return;

            _prevPageButton.IsEnabled = _paginationInfo.HasPrevPage;
            _nextPageButton.IsEnabled = _paginationInfo.HasNextPage;
            _pageInfoText.Text = $"Página {_paginationInfo.Page} de {_paginationInfo.TotalPages} ({_paginationInfo.Total} total)";
        }

        private void ShowError(string message)
        {
            _chatsPanel.Children.Clear();
            
            var errorTextBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(24)
            };
            
            _chatsPanel.Children.Add(errorTextBlock);
        }

        private void BuildChatsList()
        {
            _chatsPanel.Children.Clear();

            if (_communityChats == null || _communityChats.Count == 0)
            {
                var emptyTextBlock = new TextBlock
                {
                    Text = "No hay chats públicos disponibles en este momento.",
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(24)
                };
                _chatsPanel.Children.Add(emptyTextBlock);
                return;
            }

            foreach (var chat in _communityChats)
            {
                var itemBorder = new Border
                {
                    DataContext = chat,
                    BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12),
                    Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"]
                };

                var itemStack = new StackPanel
                {
                    Spacing = 4
                };

                var nameBlock = new TextBlock
                {
                    Text = chat.Name,
                    FontSize = 16,
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
                };

                var codeBlock = new TextBlock
                {
                    Text = $"Código: {chat.CodeInvitation}",
                    FontSize = 13,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };

                itemStack.Children.Add(nameBlock);
                itemStack.Children.Add(codeBlock);
                itemBorder.Child = itemStack;

                itemBorder.PointerPressed += (s, e) =>
                {
                    SelectChat(chat, itemBorder);
                };

                itemBorder.PointerEntered += (s, e) =>
                {
                    if (_selectedChat != chat)
                    {
                        itemBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                    }
                };

                itemBorder.PointerExited += (s, e) =>
                {
                    if (_selectedChat != chat)
                    {
                        itemBorder.Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"];
                    }
                };

                _chatsPanel.Children.Add(itemBorder);
            }
        }

        private void SelectChat(CommunityChat chat, Border selectedBorder)
        {
            _selectedChat = chat;

            foreach (var child in _chatsPanel.Children.OfType<Border>())
            {
                child.Background = (Brush)Application.Current.Resources["SubtleFillColorTransparentBrush"];
                child.BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
            }

            selectedBorder.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            selectedBorder.BorderBrush = (Brush)Application.Current.Resources["AccentControlElevationBorderBrush"];
        }
    }
}
