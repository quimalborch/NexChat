using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Chat : INotifyPropertyChanged
    {
        private bool _isStarting;
        private bool _isRunning;
        private string? _codeInvitation;
        private ConnectionStatus _connectionStatus = ConnectionStatus.Unknown;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public bool? CommunityChat { get; set; }
        public string? CommunityChatSecret { get; set; }


        public string? CodeInvitation 
        { 
            get => _codeInvitation;
            set
            {
                if (_codeInvitation != value)
                {
                    _codeInvitation = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public List<Message> Messages { get; set; }
        public bool IsInvited { get; set; } = true;
        
        public bool IsRunning 
        { 
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [JsonIgnore]
        public int? ServerPort { get; set; }
        
        [JsonIgnore]
        public bool IsStarting 
        { 
            get => _isStarting;
            set
            {
                if (_isStarting != value)
                {
                    _isStarting = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Estado de conexión del chat (solo relevante para chats invitados)
        /// </summary>
        [JsonIgnore]
        public ConnectionStatus ConnectionStatus 
        { 
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public Chat() 
        {
            Messages = new List<Message>();
        }

        public Chat(string Name) 
        {
            this.Name = Name;
            Messages = new List<Message>();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Estado de conexión de un chat invitado
    /// </summary>
    public enum ConnectionStatus
    {
        Unknown,      // Estado inicial o no verificado
        Connected,    // Conectado correctamente
        Disconnected  // No se puede conectar
    }
}
