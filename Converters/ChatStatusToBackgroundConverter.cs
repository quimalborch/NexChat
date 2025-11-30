using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using NexChat.Data;
using System;

namespace NexChat.Converters
{
    /// <summary>
    /// Convierte el estado de un chat invitado a un color de fondo con gradiente
    /// </summary>
    public class ChatStatusToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not Chat chat)
                return new SolidColorBrush(Colors.Transparent);

            // Si es host, sin fondo especial
            if (!chat.IsInvited)
                return new SolidColorBrush(Colors.Transparent);

            // Para chats invitados, aplicar gradiente según estado
            switch (chat.ConnectionStatus)
            {
                case ConnectionStatus.Connected:
                    // Verde suave con transparencia
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(30, 34, 197, 94)); // #22C55E con alpha
                
                case ConnectionStatus.Disconnected:
                    // Rojo suave con transparencia
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(30, 239, 68, 68)); // #EF4444 con alpha
                
                case ConnectionStatus.Unknown:
                default:
                    // Amarillo suave para estado desconocido
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(20, 234, 179, 8)); // #EAB308 con alpha
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
