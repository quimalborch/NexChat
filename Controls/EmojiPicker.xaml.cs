using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexChat.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexChat.Controls
{
    public sealed partial class EmojiPicker : UserControl
    {
        public event EventHandler<string> EmojiSelected;
        
        private List<EmojiCategory> _categories;
        private EmojiCategory _selectedCategory;

        public EmojiPicker()
        {
            this.InitializeComponent();
            _categories = EmojiData.Categories;
            
            LoadCategories();
            LoadRecentEmojis();
        }

        private void LoadCategories()
        {
            CategoryListView.Items.Clear();
            
            var recentButton = new Button
            {
                Content = "⌚",
                Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
                FontSize = 20,
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "Recent"
            };
            recentButton.Click += CategoryButton_Click;
            CategoryListView.Items.Add(recentButton);
            
            foreach (var category in _categories)
            {
                var button = new Button
                {
                    Content = category.Icon,
                    Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
                    FontSize = 20,
                    Width = 44,
                    Height = 44,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = category
                };
                button.Click += CategoryButton_Click;
                CategoryListView.Items.Add(button);
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is string tag && tag == "Recent")
            {
                LoadRecentEmojis();
            }
            else if (button?.Tag is EmojiCategory category)
            {
                _selectedCategory = category;
                LoadEmojis(category.Emojis);
            }
        }

        private void LoadRecentEmojis()
        {
            var recentEmojis = EmojiData.GetRecentEmojis();
            
            if (recentEmojis.Count == 0)
            {
                _selectedCategory = _categories[0];
                LoadEmojis(_categories[0].Emojis);
            }
            else
            {
                _selectedCategory = null;
                LoadEmojis(recentEmojis);
            }
        }

        private void LoadEmojis(List<string> emojis)
        {
            EmojiGridView.Items.Clear();
            
            foreach (var emoji in emojis)
            {
                var button = new Button
                {
                    Content = emoji,
                    FontSize = 28,
                    Width = 54,
                    Height = 54,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = emoji
                };
                button.Click += EmojiButton_Click;
                EmojiGridView.Items.Add(button);
            }
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is string emoji)
            {
                EmojiData.AddRecentEmoji(emoji);
                EmojiSelected?.Invoke(this, emoji);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            var searchText = SearchBox.Text;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                if (_selectedCategory != null)
                {
                    LoadEmojis(_selectedCategory.Emojis);
                }
                else
                {
                    LoadRecentEmojis();
                }
                return;
            }
            
            var allEmojis = _categories.SelectMany(c => c.Emojis).Distinct().ToList();
            var filteredEmojis = allEmojis.Where(e => e.Contains(searchText)).ToList();
            
            LoadEmojis(filteredEmojis);
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
        }
    }
}
