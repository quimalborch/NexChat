using Microsoft.UI.Xaml.Controls;
using NexChat.Data;
using NexChat.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NexChat.Services
{
    public class ChatService : IDisposable
    {
        public List<Chat> chats = new List<Chat>();
        public event EventHandler<List<Chat>> ChatListUpdated;
        private Dictionary<string, WebServerService> _webServers = new Dictionary<string, WebServerService>();
        private CloudflaredService _cloudflaredService;
        private ChatConnectorService _chatConnectorService;
        private ConfigurationService _configurationService;
        
        // 🔐 SEGURIDAD: Servicio de cifrado E2EE
        private SecureMessagingService _secureMessaging;
        
        // WebSocket connections para chats remotos
        private Dictionary<string, ChatWebSocketService> _webSocketConnections = new Dictionary<string, ChatWebSocketService>();

        private bool _disposed = false;

        public ChatService(CloudflaredService cloudflaredService, ChatConnectorService chatConnectorService, ConfigurationService configurationService) 
        {
            _cloudflaredService = cloudflaredService;
            _chatConnectorService = chatConnectorService;
            _configurationService = configurationService;
            
            // 🔐 Inicializar servicio de cifrado
            _secureMessaging = new SecureMessagingService();
            Log.Information("✅ SecureMessagingService initialized - E2EE enabled");
            
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
                
                // Reconectar chats remotos vía WebSocket
                var remoteChats = chats.Where(c => c.IsInvited && !string.IsNullOrEmpty(c.CodeInvitation)).ToList();
                foreach (var chat in remoteChats)
                {
                    Console.WriteLine($"Reconectando WebSocket para chat remoto '{chat.Name}'...");
                    await ConnectWebSocketForRemoteChat(chat.Id);
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
            Log.Information("🚀 [CHAT] Joining remote chat with code: {ChatCode}", Name);
            
            Chat? chatRecuperado = await _chatConnectorService.GetChat(Name);

            if (chatRecuperado is null)
            {
                Log.Error("❌ [CHAT] Could not retrieve chat information from remote server");
                return false;
            }
            
            Log.Information("✅ [CHAT] Successfully retrieved chat info: {ChatName}", chatRecuperado.Name);

            // 🔐 INTERCAMBIO DE CLAVES: Obtener la clave pública del servidor remoto
            Log.Information("🔐 [KEY-EXCHANGE] Starting public key exchange with remote server...");
            
            string? remoteHostUserIdHash = null;
            
            try
            {
                Log.Debug("🔑 [KEY-EXCHANGE] Fetching public key from: {ChatCode}", Name);
                
                var remoteKeyExchange = await _chatConnectorService.GetPublicKey(Name);
                
                if (remoteKeyExchange == null)
                {
                    Log.Error("❌ [KEY-EXCHANGE] Failed to fetch public key - received null response");
                    Log.Warning("⚠️ [KEY-EXCHANGE] Encryption will NOT be available for this chat");
                }
                else
                {
                    Log.Information("✅ [KEY-EXCHANGE] Received public key exchange data");
                    Log.Debug("📦 [KEY-EXCHANGE] Remote user: {DisplayName}", remoteKeyExchange.DisplayName);
                    Log.Debug("📦 [KEY-EXCHANGE] Remote user ID hash: {UserIdHash}", 
                        remoteKeyExchange.UserIdHash.Substring(0, Math.Min(16, remoteKeyExchange.UserIdHash.Length)) + "...");
                    Log.Debug("📦 [KEY-EXCHANGE] Public key length: {Length} chars", remoteKeyExchange.PublicKeyPem?.Length ?? 0);
                    Log.Debug("📦 [KEY-EXCHANGE] Timestamp: {Timestamp}", remoteKeyExchange.Timestamp);
                    
                    // 🔑 GUARDAR el user ID hash del host remoto
                    remoteHostUserIdHash = remoteKeyExchange.UserIdHash;
                    Log.Information("💾 [KEY-EXCHANGE] Stored remote host user ID hash: {Hash}", 
                        remoteHostUserIdHash.Substring(0, Math.Min(16, remoteHostUserIdHash.Length)) + "...");
                    
                    // Registrar la clave pública del host remoto
                    Log.Debug("📝 [KEY-EXCHANGE] Processing public key exchange...");
                    _secureMessaging.ProcessPublicKeyExchange(remoteKeyExchange);
                    
                    Log.Information("✅ [KEY-EXCHANGE] Public key successfully exchanged with remote chat host: {DisplayName}", remoteKeyExchange.DisplayName);
                    Log.Information("🔒 [KEY-EXCHANGE] E2EE encryption is now ENABLED for this chat");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ [KEY-EXCHANGE] Exception during public key exchange");
                Log.Warning("⚠️ [KEY-EXCHANGE] Chat will continue but encryption will NOT be available");
            }

            string ChatRemotoName = $"{chatRecuperado.Name}";

#if DEBUG
            ChatRemotoName = ChatRemotoName.Insert(0, "[REMOTO] ");
#endif

            Log.Debug("📝 [CHAT] Creating local chat object: {ChatName}", ChatRemotoName);

            Chat? chatRemoto = new Chat(ChatRemotoName);

            chatRemoto.IsInvited = true;
            chatRemoto.CodeInvitation = Name;
            chatRemoto.ConnectionStatus = ConnectionStatus.Unknown; // Estado inicial
            
            // 🔑 IMPORTANTE: Guardar el user ID hash del host remoto para cifrar mensajes
            chatRemoto.RemoteHostUserIdHash = remoteHostUserIdHash;
            
            if (remoteHostUserIdHash != null)
            {
                Log.Information("✅ [CHAT] Remote host user ID hash saved for encryption: {Hash}", 
                    remoteHostUserIdHash.Substring(0, Math.Min(16, remoteHostUserIdHash.Length)) + "...");
            }
            else
            {
                Log.Warning("⚠️ [CHAT] No remote host user ID hash - encryption will fail");
            }
            
            // Copiar mensajes iniciales del chat recuperado
            Log.Debug("📋 [CHAT] Copying {Count} initial messages", chatRecuperado.Messages.Count);
            foreach (var msg in chatRecuperado.Messages)
            {
                msg.Chat = chatRemoto;
                chatRemoto.Messages.Add(msg);
            }
            
            chats.Add(chatRemoto);
            SaveChats();
            
            Log.Information("💾 [CHAT] Chat saved locally");
            
            // Conectar WebSocket para recibir actualizaciones en tiempo real
            Log.Debug("🔌 [CHAT] Connecting WebSocket for real-time updates...");
            bool connected = await ConnectWebSocketForRemoteChat(chatRemoto.Id);
            
            if (connected)
            {
                Log.Information("✅ [CHAT] WebSocket connected successfully");
            }
            else
            {
                Log.Warning("⚠️ [CHAT] WebSocket connection failed - will use polling instead");
            }

            Log.Information("✅ [CHAT] Successfully joined remote chat: {ChatName}", ChatRemotoName);
            return true;
        }

        /// <summary>
        /// Conecta un WebSocket para un chat remoto
        /// </summary>
        private async Task<bool> ConnectWebSocketForRemoteChat(string chatId)
        {
            var chat = GetChatById(chatId);
            if (chat == null || !chat.IsInvited || string.IsNullOrEmpty(chat.CodeInvitation))
                return false;

            // Si ya existe una conexión, desconectarla primero
            await DisconnectWebSocketForRemoteChat(chatId);

            try
            {
                // Crear servicio WebSocket
                var wsService = new ChatWebSocketService();
                
                // Suscribirse a eventos
                wsService.MessageReceived += (sender, message) =>
                {
                    OnWebSocketMessageReceived(chatId, message);
                };
                
                wsService.ConnectionStatusChanged += (sender, status) =>
                {
                    Console.WriteLine($"🔌 WebSocket status for chat '{chat.Name}': {status}");
                    
                    // Actualizar estado de conexión del chat
                    if (status == "Connected")
                    {
                        chat.ConnectionStatus = ConnectionStatus.Connected;
                    }
                    else if (status == "Disconnected" || status == "Error")
                    {
                        chat.ConnectionStatus = ConnectionStatus.Disconnected;
                    }
                };

                // Conectar
                string url = $"https://{chat.CodeInvitation}.trycloudflare.com";
                bool connected = await wsService.ConnectAsync(url, chatId);

                if (connected)
                {
                    _webSocketConnections[chatId] = wsService;
                    chat.ConnectionStatus = ConnectionStatus.Connected;
                    Console.WriteLine($"✓ WebSocket connected for remote chat '{chat.Name}'");
                    return true;
                }
                else
                {
                    chat.ConnectionStatus = ConnectionStatus.Disconnected;
                    Console.WriteLine($"❌ Failed to connect WebSocket for chat '{chat.Name}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                chat.ConnectionStatus = ConnectionStatus.Disconnected;
                Console.WriteLine($"❌ Error connecting WebSocket for chat {chatId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Desconecta el WebSocket de un chat remoto
        /// </summary>
        private async Task DisconnectWebSocketForRemoteChat(string chatId)
        {
            if (_webSocketConnections.TryGetValue(chatId, out var wsService))
            {
                await wsService.DisconnectAsync();
                _webSocketConnections.Remove(chatId);
                
                // Actualizar estado del chat
                var chat = GetChatById(chatId);
                if (chat != null)
                {
                    chat.ConnectionStatus = ConnectionStatus.Disconnected;
                }
                
                Console.WriteLine($"🔌 WebSocket disconnected for chat {chatId}");
            }
        }

        /// <summary>
        /// Maneja mensajes recibidos por WebSocket
        /// </summary>
        private void OnWebSocketMessageReceived(string chatId, Message message)
        {
            try
            {
                var chat = GetChatById(chatId);
                if (chat == null)
                    return;

                Console.WriteLine($"📩 New message received via WebSocket for chat '{chat.Name}'");

                // 🔐 Descifrar mensaje si está cifrado
                Message messageToStore = message;
                
                if (message.IsEncrypted)
                {
                    try
                    {
                        Log.Information("🔐 Encrypted message received via WebSocket - decrypting");
                        messageToStore = _secureMessaging.DecryptMessage(message);
                        Log.Information("✅ Message decrypted: {Content}", messageToStore.Content);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "❌ Failed to decrypt WebSocket message");
                        messageToStore.Content = "[🔒 Mensaje cifrado - no se pudo descifrar]";
                    }
                }

                // Verificar que el mensaje no exista ya (evitar duplicados)
                if (!chat.Messages.Any(m => m.Id == messageToStore.Id))
                {
                    messageToStore.Chat = chat;
                    chat.Messages.Add(messageToStore);
                    
                    // Notificar a la UI
                    ChatListUpdated?.Invoke(this, chats);
                    
                    Console.WriteLine($"✓ Message added to chat '{chat.Name}'");
                }
                else
                {
                    Console.WriteLine($"⚠️ Duplicate message ignored: {messageToStore.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing WebSocket message: {ex.Message}");
            }
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
                // Crear una copia temporal para serializar sin mensajes de chats remotos
                var chatsToSave = new List<Chat>();
                
                foreach (Chat _chat in chats)
                {
                    var chatCopy = new Chat(_chat.Name)
                    {
                        Id = _chat.Id,
                        CodeInvitation = _chat.CodeInvitation,
                        IsInvited = _chat.IsInvited,
                        IsRunning = _chat.IsRunning,
                        ServerPort = _chat.ServerPort
                    };
                    
                    // Solo guardar mensajes de chats locales
                    if (!_chat.IsInvited)
                    {
                        chatCopy.Messages = _chat.Messages;
                    }
                    
                    chatsToSave.Add(chatCopy);
                }

                string? conteindoJSONChats = System.Text.Json.JsonSerializer.Serialize(chatsToSave);

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

        public async Task DeleteChat(string chatId)
        {
            Chat? chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat is null) return;
            
            // Desconectar WebSocket si es un chat remoto
            if (chat.IsInvited)
            {
                await DisconnectWebSocketForRemoteChat(chatId);
            }
            
            // Detener servidor web si es un chat local
            if (chat.IsRunning)
            {
                await StopWebServer(chatId);
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

        public async Task AddMessage(string chatId, Message message)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null) return;
            
            // 🔐 CIFRAR EL MENSAJE antes de enviarlo
            Message messageToSend = message;
            
            try
            {
                // Obtener el hash del ID del destinatario (para chats remotos) o del propio usuario (chats locales)
                string recipientIdHash;
                
                if (chat.IsInvited)
                {
                    // ✅ CORRECCIÓN: Usar el RemoteHostUserIdHash guardado del host
                    if (string.IsNullOrEmpty(chat.RemoteHostUserIdHash))
                    {
                        Log.Error("❌ [CHAT] Cannot encrypt: RemoteHostUserIdHash is null for chat '{ChatName}'", chat.Name);
                        Log.Warning("⚠️ [CHAT] Sending UNENCRYPTED message - no remote host ID available");
                    }
                    else
                    {
                        recipientIdHash = chat.RemoteHostUserIdHash;
                        
                        Log.Information("🔐 [CHAT] Attempting to encrypt message for remote chat '{ChatName}'", chat.Name);
                        Log.Debug("🔑 [CHAT] Recipient (remote host) ID hash: {Hash}", 
                            recipientIdHash.Substring(0, Math.Min(16, recipientIdHash.Length)) + "...");
                        
                        // Verificar si tenemos la clave pública del destinatario
                        if (!_secureMessaging.CanEncryptFor(recipientIdHash))
                        {
                            Log.Error("❌ [CHAT] Cannot encrypt: recipient public key not found for hash {Hash}", 
                                recipientIdHash.Substring(0, Math.Min(16, recipientIdHash.Length)) + "...");
                            
                            // Log all registered keys for debugging
                            Log.Debug("📋 [CHAT] Checking registered public keys...");
                            
                            // Enviar mensaje sin cifrar con advertencia
                            Log.Warning("⚠️ [CHAT] Sending UNENCRYPTED message - no public key available");
                        }
                        else
                        {
                            Log.Information("✅ [CHAT] Recipient public key found - encrypting message");
                            
                            // Cifrar el mensaje
                            messageToSend = _secureMessaging.EncryptMessage(message, recipientIdHash);
                            Log.Information("✅ [CHAT] Message encrypted successfully for remote host");
                        }
                    }
                }
                else
                {
                    // Chat local: no necesitamos cifrar (solo nosotros lo vemos localmente)
                    // Pero SI ciframos para cuando se envíe a clientes conectados
                    Log.Debug("📝 [CHAT] Local chat message - storing plaintext locally");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ [CHAT] Error encrypting message");
                // Continuar con mensaje sin cifrar como fallback
            }
            
            // Si es un chat remoto, enviar mensaje por WebSocket
            if (chat.IsInvited && _webSocketConnections.TryGetValue(chatId, out var wsService))
            {
                try
                {
                    bool sent = await wsService.SendMessageAsync(messageToSend);
                    if (sent)
                    {
                        chat.ConnectionStatus = ConnectionStatus.Connected;
                        Console.WriteLine($"✓ Message sent to remote chat via WebSocket");
                        // El mensaje se agregará cuando el servidor lo confirme y lo broadcast de vuelta
                    }
                    else
                    {
                        Console.WriteLine($"❌ Failed to send message via WebSocket, falling back to HTTP");
                        
                        // Fallback a HTTP POST si WebSocket falla
                        bool httpSent = await _chatConnectorService.SendMessage(chat.CodeInvitation!, messageToSend);
                        
                        if (httpSent)
                        {
                            chat.ConnectionStatus = ConnectionStatus.Connected;
                            // Con HTTP no hay broadcast automático, así que agregamos el mensaje manualmente
                            messageToSend.Chat = chat;
                            if (!chat.Messages.Any(m => m.Id == messageToSend.Id))
                            {
                                chat.Messages.Add(messageToSend);
                                SaveChats();
                                Console.WriteLine($"✓ Message added locally after HTTP fallback");
                            }
                        }
                        else
                        {
                            // Ambos métodos fallaron
                            chat.ConnectionStatus = ConnectionStatus.Disconnected;
                            throw new Exception("No se pudo enviar el mensaje. El servidor no responde.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    chat.ConnectionStatus = ConnectionStatus.Disconnected;
                    Console.WriteLine($"❌ Error sending message: {ex.Message}");
                    throw; // Re-throw para que MainWindow lo maneje
                }
            }
            else if (chat.IsInvited && !_webSocketConnections.ContainsKey(chatId))
            {
                // No hay WebSocket, intentar HTTP directamente
                try
                {
                    bool httpSent = await _chatConnectorService.SendMessage(chat.CodeInvitation!, messageToSend);
                    
                    if (httpSent)
                    {
                        chat.ConnectionStatus = ConnectionStatus.Connected;
                        messageToSend.Chat = chat;
                        if (!chat.Messages.Any(m => m.Id == messageToSend.Id))
                        {
                            chat.Messages.Add(messageToSend);
                            SaveChats();
                        }
                    }
                    else
                    {
                        chat.ConnectionStatus = ConnectionStatus.Disconnected;
                        throw new Exception("No se pudo enviar el mensaje. El servidor no responde.");
                    }
                }
                catch (Exception ex)
                {
                    chat.ConnectionStatus = ConnectionStatus.Disconnected;
                    throw;
                }
            }
            else
            {
                // Chat local, agregar directamente
                chat.Messages.Add(message);
                SaveChats();
                
                // Si el chat tiene un servidor web activo, broadcast a los clientes conectados
                if (_webServers.TryGetValue(chatId, out var webServer))
                {
                    await webServer.BroadcastMessage(messageToSend);
                }
            }
        }

        public void ReceiveMessage(string chatId, Message message)
        {
            try
            {
                Chat? chat = GetChatById(chatId);
                if (chat is null) 
                {
                    Console.WriteLine($"❌ Chat not found for ID: {chatId}");
                    return;
                }

                // 🔐 DESCIFRAR EL MENSAJE si está cifrado
                Message messageToStore = message;
                
                if (message.IsEncrypted)
                {
                    try
                    {
                        Log.Information("🔐 Encrypted message received - attempting to decrypt");
                        messageToStore = _secureMessaging.DecryptMessage(message);
                        Log.Information("✅ Message decrypted successfully: {Content}", messageToStore.Content);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "❌ Failed to decrypt message");
                        // Guardar mensaje cifrado con placeholder
                        messageToStore.Content = "[🔒 Mensaje cifrado - no se pudo descifrar]";
                    }
                }

                // IMPORTANTE: Asignar el Chat al mensaje
                messageToStore.Chat = chat;
                
                Console.WriteLine($"📥 Receiving message for chat '{chat.Name}': {messageToStore.Content}");
                
                chat.Messages.Add(messageToStore);
                SaveChats();
                
                Console.WriteLine($"✓ Message added to chat '{chat.Name}' (Total messages: {chat.Messages.Count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ReceiveMessage: {ex.Message}");
            }
        }

        public async Task<bool> StartWebServer(string chatId, bool enableTunnel = false)
        {
            Chat? chat = GetChatById(chatId);
            if (chat is null || chat.IsRunning) 
                return false;

            // Crear y arrancar el servidor web con soporte de túnel y WebSocket
            WebServerService webServer = new WebServerService(_cloudflaredService);
            webServer.ChatListUpdated += WebServer_ChatListUpdated;
            webServer.CreateMessage += WebServer_CreateMessage;
            
            bool started = await webServer.Start(chatId, enableTunnel);
            
            if (!started)
            {
                Console.WriteLine($"Failed to start web server for chat '{chat.Name}'");
                
                // IMPORTANTE: Limpiar procesos cloudflared si falló el inicio
                if (_cloudflaredService != null)
                {
                    Console.WriteLine("Forcing cleanup of cloudflared processes after failed start");
                    _cloudflaredService.ForceCleanupAllProcesses();
                }
                
                return false;
            }
            
            if (!webServer.IsTunnelActive)
            {
                Console.WriteLine($"Tunnel not active for chat '{chat.Name}', stopping server");
                await StopWebServer(chatId);
                
                // IMPORTANTE: Limpiar procesos cloudflared si el túnel no se activó
                if (_cloudflaredService != null)
                {
                    Console.WriteLine("Forcing cleanup of cloudflared processes after tunnel failure");
                    _cloudflaredService.ForceCleanupAllProcesses();
                }
                
                SaveChats();
                return false;
            }

            _webServers[chatId] = webServer;
            chat.IsRunning = true;
            chat.ServerPort = webServer.Port;
            chat.CodeInvitation = $"{GetSubdomain(webServer.TunnelUrl)}";
            SaveChats();
            
            Console.WriteLine($"Web server started for chat '{chat.Name}' on port {webServer.Port}");
            Console.WriteLine($"WebSocket available at: ws://localhost:{webServer.Port}/ws");
            
            if (enableTunnel && webServer.IsTunnelActive)
            {
                Console.WriteLine($"Cloudflare tunnel active: {webServer.TunnelUrl}");
                Console.WriteLine($"WebSocket via tunnel: wss://{GetSubdomain(webServer.TunnelUrl)}.trycloudflare.com/ws");
            }
            
            return true;
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
        
        public bool IsWebSocketConnected(string chatId)
        {
            return _webSocketConnections.TryGetValue(chatId, out var ws) && ws.IsConnected;
        }

        public int GetConnectedClientsCount(string chatId)
        {
            if (_webServers.TryGetValue(chatId, out var webServer))
            {
                return webServer.ConnectedClientsCount;
            }
            return 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Console.WriteLine("Disposing ChatService - cleaning up resources");
                
                // Cerrar todos los servidores web
                var chatIds = _webServers.Keys.ToList();
                foreach (var chatId in chatIds)
                {
                    try
                    {
                        StopWebServer(chatId).Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping web server during dispose: {ex.Message}");
                    }
                }
                
                // Desconectar todos los WebSockets
                var wsIds = _webSocketConnections.Keys.ToList();
                foreach (var chatId in wsIds)
                {
                    try
                    {
                        DisconnectWebSocketForRemoteChat(chatId).Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disconnecting WebSocket during dispose: {ex.Message}");
                    }
                }
                
                // Dispose SecureMessagingService
                _secureMessaging?.Dispose();
                Log.Information("SecureMessagingService disposed");
                
                // Dispose CloudflaredService (esto cerrará todos los túneles)
                if (_cloudflaredService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
