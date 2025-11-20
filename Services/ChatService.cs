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
        private Dictionary<string, WebServerService> _webServers = new Dictionary<string, WebServerService>();
        private CloudflaredService _cloudflaredService;
        
        public ChatService(CloudflaredService cloudflaredService) 
        {
            _cloudflaredService = cloudflaredService;
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

        public async Task<bool> StartWebServer(string chatId, bool enableTunnel = false)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || chat.IsRunning) 
                return false;
            
            // Crear y arrancar el servidor web con soporte de túnel
            var webServer = new WebServerService(_cloudflaredService);
            if (await webServer.Start(chatId, enableTunnel))
            {
                _webServers[chatId] = webServer;
                chat.IsRunning = true;
                chat.ServerPort = webServer.Port;
                SaveChats();
                
                Console.WriteLine($"Web server started for chat '{chat.Name}' on port {webServer.Port}");
                
                if (enableTunnel && webServer.IsTunnelActive)
                {
                    Console.WriteLine($"Cloudflare tunnel active: {webServer.TunnelUrl}");
                }
                
                // Probar la conexión en segundo plano
                Task.Run(async () =>
                {
                    await Task.Delay(500); // Esperar un poco para que el servidor esté listo
                    bool testResult = await webServer.TestConnection();
                    Console.WriteLine($"Server test result: {(testResult ? "✓ SUCCESS" : "✗ FAILED")}");
                });
                
                return true;
            }
            
            Console.WriteLine($"Failed to start web server for chat '{chat.Name}'");
            return false;
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
