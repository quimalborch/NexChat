using NexChat.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class ChatService
    {
        public List<Chat> chats = new List<Chat>();
        public event EventHandler<List<Chat>> ChatListUpdated;
        private Dictionary<string, WebServerService> _webServers = new Dictionary<string, WebServerService>();
        private CloudflaredService _cloudflaredService;
        private ChatConnectorService _chatConnectorService;

        public ChatService(CloudflaredService cloudflaredService, ChatConnectorService chatConnectorService) 
        {
            _cloudflaredService = cloudflaredService;
            _chatConnectorService = chatConnectorService;
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

        public async Task<bool> JoinChat(string Name)
        {
            Chat? chatRecuperado = await _chatConnectorService.GetChat(Name);

            if (chatRecuperado is null) return false;

            string ChatRemotoName = $"{chatRecuperado.Name}";

#if DEBUG
            ChatRemotoName = ChatRemotoName.Insert(0, "[REMOTO] ");
#endif

            Chat? chatRemoto = new Chat(ChatRemotoName);

            chatRemoto.IsInvited = true;
            chatRemoto.CodeInvitation = Name;
            chats.Add(chatRemoto);
            SaveChats();

            return true;
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
            try
            {
                foreach (Chat _chat in chats)
                {
                    if (_chat.IsInvited)
                    {
                        _chat.Messages = new List<Message>();
                    }
                }

                string? conteindoJSONChats = System.Text.Json.JsonSerializer.Serialize(chats);

                if (string.IsNullOrEmpty(conteindoJSONChats))
                    return;

                File.WriteAllText(GetChatPath(), conteindoJSONChats);
                ChatListUpdated?.Invoke(this, chats);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
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
            try
            {
                //TODO: Implementar lógica para recibir mensajes desde red/servidor
                // Este método se llamaría cuando llegue un mensaje nuevo de otro usuario
                Chat? chat = GetChatById(chatId);
                if (chat is null) return;

                chat.Messages.Add(message);
                SaveChats();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task<bool> StartWebServer(string chatId, bool enableTunnel = false)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || chat.IsRunning) 
                return false;

            // Crear y arrancar el servidor web con soporte de túnel
            WebServerService webServer = new WebServerService(_cloudflaredService);
            webServer.ChatListUpdated += WebServer_ChatListUpdated;
            webServer.CreateMessage += WebServer_CreateMessage;
            if (await webServer.Start(chatId, enableTunnel))
            {
                _webServers[chatId] = webServer;
                chat.IsRunning = true;
                chat.ServerPort = webServer.Port;
                chat.CodeInvitation = $"{GetSubdomain(webServer.TunnelUrl)}";
                SaveChats();
                
                Console.WriteLine($"Web server started for chat '{chat.Name}' on port {webServer.Port}");
                
                if (enableTunnel && webServer.IsTunnelActive)
                {
                    Console.WriteLine($"Cloudflare tunnel active: {webServer.TunnelUrl}");
                }
                
                return true;
            }
            
            Console.WriteLine($"Failed to start web server for chat '{chat.Name}'");
            return false;
        }

        private bool WebServer_CreateMessage(string chatId, Message message)
        {
            try
            {
                ReceiveMessage(chatId, message);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public string GetSubdomain(string url)
        {
            try
            {
                if (url is null) return string.Empty;
                var match = Regex.Match(url, @"https://([^.]+)");
                if (match.Success)
                    return match.Groups[1].Value;

                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private Chat? WebServer_ChatListUpdated(string chatId)
        {
            return WebGetChat(chatId);
        }

        public Chat? WebGetChat(string chatId)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || !chat.IsRunning)
                return null;

            return chat;
        }

        public async Task<bool> StopWebServer(string chatId)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || !chat.IsRunning) 
                return false;
            
            // Detener el servidor web (y el túnel si está activo)
            if (_webServers.TryGetValue(chatId, out var webServer))
            {
                webServer.Stop();
                _webServers.Remove(chatId);
            }
            
            chat.IsRunning = false;
            chat.CodeInvitation = null;
            chat.ServerPort = null;
            SaveChats();
            
            Console.WriteLine($"Web server stopped for chat '{chat.Name}'");
            return true;
        }

        public async Task<bool> OpenTunnel(string chatId)
        {
            if (!_webServers.TryGetValue(chatId, out var webServer))
            {
                Console.WriteLine($"No web server running for chat {chatId}");
                return false;
            }

            return await webServer.OpenTunnel();
        }

        public async Task<bool> CloseTunnel(string chatId)
        {
            if (!_webServers.TryGetValue(chatId, out var webServer))
            {
                Console.WriteLine($"No web server running for chat {chatId}");
                return false;
            }

            return await webServer.CloseTunnel();
        }

        public string? GetTunnelUrl(string chatId)
        {
            if (_webServers.TryGetValue(chatId, out var webServer))
            {
                return webServer.TunnelUrl;
            }
            return null;
        }

        public bool IsTunnelActive(string chatId)
        {
            if (_webServers.TryGetValue(chatId, out var webServer))
            {
                return webServer.IsTunnelActive;
            }
            return false;
        }
    }
}
