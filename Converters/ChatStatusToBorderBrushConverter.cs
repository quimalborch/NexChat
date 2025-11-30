using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using NexChat.Data;
using System;

namespace NexChat.Converters
{
    /// <summary>
    /// Convierte el estado de un chat invitado a un color de borde
    /// </summary>
    public class ChatStatusToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not Chat chat)
                return new SolidColorBrush(Colors.Transparent);

            // Si es host, sin borde especial
            if (!chat.IsInvited)
                return new SolidColorBrush(Colors.Transparent);

            // Para chats invitados, aplicar color de borde según estado
            switch (chat.ConnectionStatus)
            {
                case ConnectionStatus.Connected:
                    // Verde brillante
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94)); // #22C55E
                
                case ConnectionStatus.Disconnected:
                    // Rojo brillante
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)); // #EF4444
                
                case ConnectionStatus.Unknown:
                default:
                    // Amarillo para estado desconocido
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 234, 179, 8)); // #EAB308
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
