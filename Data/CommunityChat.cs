using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NexChat.Data
{
    public class CommunityChat : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private bool _isActive;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Name 
        { 
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string Description 
        { 
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public string CodeInvitation { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public string CreatorId { get; set; }
        
        public bool IsActive 
        { 
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }
        
        [JsonIgnore]
        public int MemberCount { get; set; }

        public CommunityChat()
        {
            _name = string.Empty;
            _description = string.Empty;
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public CommunityChat(string name, string description, string codeInvitation, string creatorId)
        {
            _name = name;
            _description = description;
            CodeInvitation = codeInvitation;
            CreatorId = creatorId;
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
