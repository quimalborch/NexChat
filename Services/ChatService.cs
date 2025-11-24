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
        
        // Polling para chats remotos
        private Dictionary<string, DateTime> _lastPollTimestamps = new Dictionary<string, DateTime>();
        private Dictionary<string, CancellationTokenSource> _pollingCancellationTokens = new Dictionary<string, CancellationTokenSource>();
        private const int POLLING_INTERVAL_MS = 2000; // Polling cada 2 segundos

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
                //chat.CodeInvitation = null;
                foreach (var message in chat.Messages)
                {
                    message.Chat = chat;
                }
            }

            ChatListUpdated?.Invoke(this, chats);
            
            // Restaurar servidores web para chats que estaban corriendo
            _ = RestoreRunningChatsAsync();
        }
        
        private async Task RestoreRunningChatsAsync()
        {
            try
            {
                var runningChats = chats.Where(c => c.IsRunning && !c.IsInvited).ToList();
                
                foreach (var chat in runningChats)
                {
                    Console.WriteLine($"Restaurando servidor web para chat '{chat.Name}'...");
                    
                    // Reiniciar el servidor web con túnel habilitado
                    // Primero reseteamos el estado para que StartWebServer funcione
                    chat.IsRunning = false;
                    
                    // Iniciar el servidor con túnel
                    bool success = await StartWebServer(chat.Id, enableTunnel: true);
                    
                    if (success)
                    {
                        Console.WriteLine($"Servidor web restaurado exitosamente para chat '{chat.Name}'");
                    }
                    else
                    {
                        Console.WriteLine($"Error al restaurar servidor web para chat '{chat.Name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al restaurar servidores web: {ex.Message}");
            }
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
            
            // Iniciar polling para este chat remoto
            StartPollingForRemoteChat(chatRemoto.Id);

            return true;
        }

        /// <summary>
        /// Inicia el polling para un chat remoto
        /// </summary>
        private void StartPollingForRemoteChat(string chatId)
        {
            var chat = GetChatById(chatId);
            if (chat == null || !chat.IsInvited || string.IsNullOrEmpty(chat.CodeInvitation))
                return;

            // Si ya existe un polling activo, detenerlo primero
            StopPollingForRemoteChat(chatId);

            // Inicializar el timestamp con el último mensaje o DateTime.UtcNow
            var lastMessage = chat.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
            _lastPollTimestamps[chatId] = lastMessage?.Timestamp ?? DateTime.UtcNow;

            // Crear token de cancelación
            var cancellationTokenSource = new CancellationTokenSource();
            _pollingCancellationTokens[chatId] = cancellationTokenSource;

            Console.WriteLine($"? Starting polling for remote chat '{chat.Name}' (ID: {chatId})");

            // Iniciar tarea de polling
            _ = Task.Run(async () => await PollRemoteChatAsync(chatId, cancellationTokenSource.Token));
        }

        /// <summary>
        /// Detiene el polling para un chat remoto
        /// </summary>
        private void StopPollingForRemoteChat(string chatId)
        {
            if (_pollingCancellationTokens.TryGetValue(chatId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _pollingCancellationTokens.Remove(chatId);
                Console.WriteLine($"? Stopped polling for chat ID: {chatId}");
            }
        }

        /// <summary>
        /// Tarea de polling que consulta periódicamente mensajes nuevos
        /// </summary>
        private async Task PollRemoteChatAsync(string chatId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var chat = GetChatById(chatId);
                    if (chat == null || !chat.IsInvited || string.IsNullOrEmpty(chat.CodeInvitation))
                    {
                        Console.WriteLine($"? Chat {chatId} no longer valid for polling, stopping...");
                        break;
                    }

                    // Obtener el último timestamp conocido
                    DateTime lastTimestamp = _lastPollTimestamps.ContainsKey(chatId)
                        ? _lastPollTimestamps[chatId]
                        : DateTime.UtcNow;

                    // Consultar mensajes nuevos
                    var newMessages = await _chatConnectorService.GetNewMessages(chat.CodeInvitation, lastTimestamp);

                    if (newMessages != null && newMessages.Count > 0)
                    {
                        Console.WriteLine($"? Received {newMessages.Count} new messages for chat '{chat.Name}'");

                        // Agregar los mensajes nuevos al chat
                        foreach (var message in newMessages)
                        {
                            message.Chat = chat;
                            chat.Messages.Add(message);
                            
                            // Actualizar el último timestamp
                            if (message.Timestamp > lastTimestamp)
                            {
                                lastTimestamp = message.Timestamp;
                            }
                        }

                        // Guardar el nuevo timestamp
                        _lastPollTimestamps[chatId] = lastTimestamp;

                        // Guardar cambios y notificar
                        SaveChats();
                        
                        Console.WriteLine($"? Chat '{chat.Name}' updated with new messages");
                    }

                    // Esperar antes del siguiente poll
                    await Task.Delay(POLLING_INTERVAL_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Polling cancelado, salir del loop
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Error in polling for chat {chatId}: {ex.Message}");
                    
                    // Esperar un poco más en caso de error antes de reintentar
                    try
                    {
                        await Task.Delay(POLLING_INTERVAL_MS * 2, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            Console.WriteLine($"? Polling task ended for chat {chatId}");
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
            
            // Detener polling si es un chat remoto
            if (chat.IsInvited)
            {
                StopPollingForRemoteChat(chatId);
            }
            
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
