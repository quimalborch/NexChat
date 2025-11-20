using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [JsonIgnore]
        public Chat Chat { get; set; }
        public Sender Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public Message()
        {
        }

        public Message(Chat _Chat, Sender _Sender, string _Content)
        {
            Chat = _Chat;
            Sender = _Sender;
            Content = _Content;
        }
    }
}
