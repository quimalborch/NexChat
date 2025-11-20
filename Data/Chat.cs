using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Chat
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string? CodeInvitation { get; set; }
        public List<Message> Messages { get; set; }
        public bool IsInvited { get; set; } = true;
        public bool IsRunning { get; set; } = false;

        public Chat() 
        {
            Messages = new List<Message>();
        }

        public Chat(string Name) 
        {
            this.Name = Name;
            Messages = new List<Message>();
        }
    }
}
