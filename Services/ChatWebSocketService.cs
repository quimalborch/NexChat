using NexChat.Data;
using System;
using System.Net.WebSockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class ChatWebSocketService
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _receiveTask;
        private string? _chatId;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public event EventHandler<Message>? MessageReceived;
        public event EventHandler<string>? ConnectionStatusChanged;

        public async Task<bool> ConnectAsync(string url, string chatId)
        {
            if (IsConnected)
            {
                Console.WriteLine("?? WebSocket already connected");
                return true;
            }

            _chatId = chatId;

            try
            {
                // Convertir URL HTTP a WebSocket URL
                string wsUrl = url.Replace("https://", "wss://").Replace("http://", "ws://");
                if (!wsUrl.EndsWith("/"))
                    wsUrl += "/";
                wsUrl += "ws";

                Console.WriteLine($"?? Connecting to WebSocket: {wsUrl}");

                _webSocket = new ClientWebSocket();
                
                // SEGURIDAD: Configurar validación de certificados TLS
                ConfigureTlsValidation(_webSocket);
                
                _cancellationTokenSource = new CancellationTokenSource();

                // Conectar al WebSocket
                await _webSocket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);

                Console.WriteLine($"? WebSocket connected securely to {wsUrl}");
                ConnectionStatusChanged?.Invoke(this, "Connected");

                // Iniciar tarea para recibir mensajes
                _receiveTask = Task.Run(() => ReceiveMessagesAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error connecting WebSocket: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
                Cleanup();
                return false;
            }
        }

        /// <summary>
        /// Configura la validación de certificados TLS para conexiones seguras
        /// </summary>
        private void ConfigureTlsValidation(ClientWebSocket webSocket)
        {
            try
            {
                // Configurar opciones de TLS/SSL
                webSocket.Options.RemoteCertificateValidationCallback = ValidateServerCertificate;
                
                Console.WriteLine("? TLS certificate validation configured");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Could not configure TLS validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Valida el certificado del servidor para prevenir ataques MITM
        /// </summary>
        private bool ValidateServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            // Si no hay errores, el certificado es válido
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                Console.WriteLine("? Server certificate is valid");
                return true;
            }

            // Log de errores de certificado
            Console.WriteLine($"?? SSL Certificate Error: {sslPolicyErrors}");

            if (certificate != null)
            {
                Console.WriteLine($"   Certificate Subject: {certificate.Subject}");
                Console.WriteLine($"   Certificate Issuer: {certificate.Issuer}");
            }

            // Para Cloudflare Tunnel (.trycloudflare.com), validar el certificado
            if (certificate != null && certificate.Subject.Contains("cloudflare", StringComparison.OrdinalIgnoreCase))
            {
                // Cloudflare tiene certificados válidos, pero el nombre puede no coincidir exactamente
                // debido al uso de subdominios dinámicos
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    Console.WriteLine("?? Certificate name mismatch for Cloudflare Tunnel - this may be expected");
                    
                    // Verificar que al menos la cadena de certificados sea válida
                    if (chain != null && chain.ChainStatus.Length == 0)
                    {
                        Console.WriteLine("? Certificate chain is valid, accepting connection");
                        return true;
                    }
                }
            }

            // Rechazar certificados con errores graves
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.RemoteCertificateChainErrors)
            {
                Console.WriteLine("? Certificate chain has errors - rejecting connection");
                return false;
            }

            // Para desarrollo local (localhost), permitir certificados auto-firmados
            if (certificate != null && certificate.Subject.Contains("localhost", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("?? Accepting self-signed certificate for localhost (development only)");
                return true;
            }

            // Por defecto, rechazar el certificado si hay errores
            Console.WriteLine("? Rejecting invalid certificate");
            return false;
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (_webSocket != null && _webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("?? WebSocket close received from server");
                        await DisconnectAsync();
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"?? WebSocket message received: {messageText}");

                        // Procesar el mensaje
                        ProcessReceivedMessage(messageText);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("?? WebSocket receive cancelled");
            }
            catch (WebSocketException wsex)
            {
                Console.WriteLine($"? WebSocket error: {wsex.Message}");
                ConnectionStatusChanged?.Invoke(this, $"Disconnected: {wsex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error receiving WebSocket messages: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private void ProcessReceivedMessage(string messageText)
        {
            try
            {
                // Deserializar el mensaje JSON
                var jsonDoc = System.Text.Json.JsonDocument.Parse(messageText);
                var root = jsonDoc.RootElement;

                // Verificar el tipo de mensaje
                if (root.TryGetProperty("type", out var typeProperty))
                {
                    string messageType = typeProperty.GetString() ?? "";

                    switch (messageType)
                    {
                        case "new_message":
                            // Extraer el mensaje
                            if (root.TryGetProperty("message", out var messageProperty))
                            {
                                var message = System.Text.Json.JsonSerializer.Deserialize<Message>(messageProperty.GetRawText());
                                if (message != null)
                                {
                                    Console.WriteLine($"? New message received: {message.Content}");
                                    MessageReceived?.Invoke(this, message);
                                }
                            }
                            break;

                        case "message_created":
                            Console.WriteLine("? Message creation confirmed by server");
                            break;

                        case "error":
                            if (root.TryGetProperty("message", out var errorMsg))
                            {
                                Console.WriteLine($"? Server error: {errorMsg.GetString()}");
                            }
                            break;

                        default:
                            Console.WriteLine($"?? Unknown message type: {messageType}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error processing received message: {ex.Message}");
            }
        }

        public async Task<bool> SendMessageAsync(Message message)
        {
            if (!IsConnected)
            {
                Console.WriteLine("? Cannot send message: WebSocket not connected");
                return false;
            }

            // SEGURIDAD: Usar hash con salt en lugar de SHA256 simple
            message.Sender.Id = NexChat.Security.CryptographyService.HashUserId(message.Sender.Id);

            try
            {
                var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
                var buffer = Encoding.UTF8.GetBytes(messageJson);

                await _webSocket!.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

                Console.WriteLine($"? Message sent via WebSocket: {message.Content}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error sending message via WebSocket: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket == null)
                return;

            Console.WriteLine("?? Disconnecting WebSocket...");

            try
            {
                _cancellationTokenSource?.Cancel();

                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                }

                Console.WriteLine("? WebSocket disconnected");
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error disconnecting WebSocket: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private void Cleanup()
        {
            _webSocket?.Dispose();
            _webSocket = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _receiveTask = null;
        }
    }
}
