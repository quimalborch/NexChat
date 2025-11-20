using NexChat.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class ChatService
    {
        public List<Chat> chats = new List<Chat>();
        public event EventHandler<List<Chat>> ChatListUpdated;
        public ChatService() 
        {
            LoadChats();
        }

        private void LoadChats()
        {
            string conteindoJSONChats = File.ReadAllText(GetChatPath());
            if (string.IsNullOrEmpty(conteindoJSONChats))
            {
                chats = new List<Chat>();
                return;
            }
        }
        private string GetChatPath()
        {
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Chats"
            );

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(Path.Combine(folder, "chats.nxc")))
                File.Create(Path.Combine(folder, "chats.nxc")).Close();

            return Path.Combine(folder, "chats.nxc");
        }

        public void CreateChat(string Name)
        {
            Chat chat = new Chat(Name);
            chats.Add(chat);
            ChatListUpdated?.Invoke(this, chats);
        }

        public void UpdateHandlerChats()
        { 
        }
    }
}
