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

            chats = System.Text.Json.JsonSerializer.Deserialize<List<Chat>>(conteindoJSONChats) ?? new List<Chat>();

            // Restaurar referencias de Chat en cada mensaje
            foreach (var chat in chats)
            {
                foreach (var message in chat.Messages)
                {
                    message.Chat = chat;
                }
            }

            ChatListUpdated?.Invoke(this, chats);
        }
        private string GetChatPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Chats"
            );

            // Crear carpeta si no existe
            Directory.CreateDirectory(folder);

            string chatFile = Path.Combine(folder, "chats.nxc");

            // Crear archivo si no existe
            if (!File.Exists(chatFile))
                File.Create(chatFile).Close();

            return chatFile;
        }

        public void CreateChat(string Name)
        {
            Chat chat = new Chat(Name);
            chat.IsInvited = false;
            chats.Add(chat);
            SaveChats();
        }

        private void SaveChats()
        {
            string? conteindoJSONChats = System.Text.Json.JsonSerializer.Serialize(chats);

            if (string.IsNullOrEmpty(conteindoJSONChats))
                return;

            File.WriteAllText(GetChatPath(), conteindoJSONChats);
            ChatListUpdated?.Invoke(this, chats);
        }

        public void UpdateHandlerChats()
        { 
            ChatListUpdated?.Invoke(this, chats);
        }

        public void EditChat(string chatId, string newName)
        {
            Chat? chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat is null) return;
            chat.Name = newName;
            SaveChats();
        }

        public void DeleteChat(string chatId)
        {
            Chat? chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat is null) return;
            chats.Remove(chat);
            SaveChats();
        }

        public void SaveChatsData()
        {
            SaveChats();
        }

        public Chat? GetChatById(string chatId)
        {
            return chats.FirstOrDefault(c => c.Id == chatId);
        }

        public void AddMessage(string chatId, Message message)
        {
            //TODO: Implementar lógica de envío de mensaje a través de red/servidor
            // Por ahora solo agregamos el mensaje localmente
            Chat? chat = GetChatById(chatId);
            if (chat is null) return;
            
            chat.Messages.Add(message);
            SaveChats();
        }

        public void ReceiveMessage(string chatId, Message message)
        {
            //TODO: Implementar lógica para recibir mensajes desde red/servidor
            // Este método se llamaría cuando llegue un mensaje nuevo de otro usuario
            Chat? chat = GetChatById(chatId);
            if (chat is null) return;
            
            chat.Messages.Add(message);
            SaveChats();
        }

        public bool StartWebServer(string chatId)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || chat.IsRunning) 
                return false;
            
            chat.IsRunning = true;
            SaveChats();
            
            //TODO: Implementar lógica para levantar el web server
            // Por ejemplo: iniciar un servidor HTTP, abrir puerto, etc.
            
            return true;
        }

        public bool StopWebServer(string chatId)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || !chat.IsRunning) 
                return false;
            
            chat.IsRunning = false;
            SaveChats();
            
            //TODO: Implementar lógica para detener el web server
            // Por ejemplo: cerrar el servidor HTTP, liberar puerto, etc.
            
            return true;
        }
    }
}
