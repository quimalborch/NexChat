using NexChat.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class WebServerService
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenerTask;
        private CloudflaredService? _cloudflaredService;
        private string? _chatId;
        
        public int Port { get; private set; }
        public bool IsRunning => _listener?.IsListening ?? false;
        public string? TunnelUrl { get; private set; }
        public bool IsTunnelActive { get; private set; }
        public EventHandler<EventArgs> TunnelActive;
        public delegate Chat? ChatUpdatedHandler(string chatId);
        public event ChatUpdatedHandler ChatListUpdated;

        public delegate bool CreateMessageHandler(string chatId, Message message);
        public event CreateMessageHandler CreateMessage;


        public WebServerService(CloudflaredService? cloudflaredService = null)
        {
            _cloudflaredService = cloudflaredService;
        }

        public async Task<bool> Start(string? chatId = null, bool enableTunnel = false)
        {
            if (IsRunning)
            {
                Console.WriteLine("Server is already running");
                return false;
            }

            _chatId = chatId;

            // Intentar varios puertos si es necesario
            int maxAttempts = 5;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Encontrar un puerto aleatorio disponible
                    Port = GetRandomAvailablePort();
                    
                    Console.WriteLine($"Attempt {attempt + 1}/{maxAttempts}: Trying to start server on port {Port}");
                    
                    _listener = new HttpListener();
                    
                    // Usar el wildcard '+' para aceptar conexiones desde cualquier hostname
                    // Esto permite que Cloudflare Tunnel y otros proxies funcionen correctamente
                    string prefix = $"http://127.0.0.1:{Port}/";
                    _listener.Prefixes.Add(prefix);

                    Console.WriteLine($"Prefix added: {prefix} (accepts any hostname)");
                    
                    _cancellationTokenSource = new CancellationTokenSource();
                    
                    // Iniciar el listener
                    _listener.Start();
                    
                    Console.WriteLine($"✓ HttpListener started successfully on port {Port}");
                    Console.WriteLine($"✓ IsListening: {_listener.IsListening}");
                    Console.WriteLine($"✓ Server URL: http://localhost:{Port}/");
                    
                    // Iniciar tarea para escuchar peticiones
                    _listenerTask = Task.Run(() => HandleIncomingConnections(_cancellationTokenSource.Token));
                    
                    Console.WriteLine("✓ Background task started for handling connections");
                    
                    // Si se solicita túnel y tenemos el servicio disponible, abrirlo
                    if (enableTunnel && _cloudflaredService != null && !string.IsNullOrEmpty(_chatId))
                    {
                        await OpenTunnel();
                    }
                    
                    return true;
                }
                catch (HttpListenerException hlex)
                {
                    Console.WriteLine($"✗ HttpListenerException on port {Port}: {hlex.Message} (Error Code: {hlex.ErrorCode})");
                    
                    // Si el error es de permisos (Error Code 5), intentar con localhost
                    if (hlex.ErrorCode == 5)
                    {
                        Console.WriteLine($"✗ Access Denied - Trying with localhost only...");
                        try
                        {
                            _listener = new HttpListener();
                            _listener.Prefixes.Add($"http://localhost:{Port}/");
                            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                            _cancellationTokenSource = new CancellationTokenSource();
                            _listener.Start();
                            
                            Console.WriteLine($"✓ Server started with localhost only (run as Admin for remote access)");
                            
                            _listenerTask = Task.Run(() => HandleIncomingConnections(_cancellationTokenSource.Token));
                            
                            // Intentar abrir túnel si fue solicitado
                            if (enableTunnel && _cloudflaredService != null && !string.IsNullOrEmpty(_chatId))
                            {
                                await OpenTunnel();
                            }
                            
                            return true;
                        }
                        catch
                        {
                            // Si también falla, continuar con el siguiente intento
                        }
                    }
                    
                    // Limpiar antes de intentar de nuevo
                    if (_listener != null)
                    {
                        try { _listener.Close(); } catch { }
                        _listener = null;
                    }
                    
                    if (attempt == maxAttempts - 1)
                    {
                        Console.WriteLine($"Failed to start server after {maxAttempts} attempts");
                        Console.WriteLine($"⚠️ TIP: Run Visual Studio as Administrator to accept remote connections");
                        Stop();
                        return false;
                    }
                    
                    // Esperar un poco antes de reintentar
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error starting web server: {ex.Message}");
                    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                    
                    if (_listener != null)
                    {
                        try { _listener.Close(); } catch { }
                        _listener = null;
                    }
                    
                    if (attempt == maxAttempts - 1)
                    {
                        Stop();
                        return false;
                    }
                    
                    Thread.Sleep(100);
                }
            }
            
            return false;
        }

        public async Task<bool> OpenTunnel()
        {
            if (_cloudflaredService == null)
            {
                Console.WriteLine("✗ CloudflaredService not available");
                return false;
            }

            if (string.IsNullOrEmpty(_chatId))
            {
                Console.WriteLine("✗ ChatId not set, cannot open tunnel");
                return false;
            }

            if (!IsRunning)
            {
                Console.WriteLine("✗ Server must be running before opening tunnel");
                return false;
            }

            if (IsTunnelActive)
            {
                Console.WriteLine("⚠️ Tunnel is already active");
                return true;
            }

            try
            {
                Console.WriteLine($"Opening Cloudflare tunnel for chat {_chatId} on port {Port}...");
                var result = await _cloudflaredService.TryOpenTunnel(_chatId, Port);
                
                if (result.Success)
                {
                    TunnelUrl = result.TunnelUrl;
                    IsTunnelActive = true;
                    Console.WriteLine($"✓ Tunnel opened successfully: {TunnelUrl}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ Failed to open tunnel: {result.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error opening tunnel: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CloseTunnel()
        {
            if (_cloudflaredService == null)
            {
                Console.WriteLine("✗ CloudflaredService not available");
                return false;
            }

            if (string.IsNullOrEmpty(_chatId))
            {
                Console.WriteLine("✗ ChatId not set");
                return false;
            }

            if (!IsTunnelActive)
            {
                Console.WriteLine("⚠️ No active tunnel to close");
                return true;
            }

            try
            {
                Console.WriteLine($"Closing Cloudflare tunnel for chat {_chatId}...");
                var result = await _cloudflaredService.TryCloseTunnel(_chatId);
                
                if (result.Success)
                {
                    TunnelUrl = null;
                    IsTunnelActive = false;
                    Console.WriteLine("✓ Tunnel closed successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"✗ Failed to close tunnel: {result.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error closing tunnel: {ex.Message}");
                return false;
            }
        }

        public async void Stop()
        {
            if (_listener == null)
                return;

            Console.WriteLine($"Stopping web server on port {Port}");

            try
            {
                // Cerrar túnel si está activo
                if (IsTunnelActive)
                {
                    await CloseTunnel();
                }

                _cancellationTokenSource?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _listenerTask?.Wait(TimeSpan.FromSeconds(5));
                
                Console.WriteLine("Web server stopped successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping web server: {ex.Message}");
            }
            finally
            {
                _listener = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _listenerTask = null;
                TunnelUrl = null;
                IsTunnelActive = false;
                _chatId = null;
            }
        }

        private async Task HandleIncomingConnections(CancellationToken cancellationToken)
        {
            Console.WriteLine("HandleIncomingConnections task started");
            
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    // Esperar por conexiones entrantes sin timeout artificial
                    var context = await _listener.GetContextAsync();
                    
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"? Request received: {context.Request.HttpMethod} {context.Request.Url}");
                        
                        // Procesar la petición sin esperar (fire and forget para no bloquear)
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                await ProcessRequest(context);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing request: {ex.Message}");
                            }
                        }, cancellationToken);
                    }
                }
                catch (HttpListenerException hlex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"HttpListenerException in HandleIncomingConnections: {hlex.Message}");
                    }
                    // Listener fue detenido
                    break;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Operation cancelled in HandleIncomingConnections");
                    // Cancelación solicitada
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in HandleIncomingConnections: {ex.Message}");
                    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                }
            }
            
            Console.WriteLine("HandleIncomingConnections task ended");
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            try
            {
                // Loggear información detallada de la petición
                Console.WriteLine($"Processing request: {request.HttpMethod} {request.Url?.AbsolutePath}");
                Console.WriteLine($"  Host: {request.Headers["Host"]}");
                Console.WriteLine($"  Remote IP: {request.RemoteEndPoint}");
                Console.WriteLine($"  User-Agent: {request.UserAgent}");

                string responseString;
                
                // Manejar diferentes rutas
                switch (request.Url?.AbsolutePath.ToLower())
                {
                    case "/":
                        responseString = $"NexChat WebServer is running! Call Id: {Guid.NewGuid().ToString()}";
                        response.StatusCode = 200;
                        Console.WriteLine("? Responding to root path");
                        break;
                        
                    case "/ping":
                        responseString = "pong";
                        response.StatusCode = 200;
                        Console.WriteLine("? Responding to /ping with pong");
                        break;

                    case "/chat/getchat":
                        if (_chatId is null)
                        {
                            responseString = "ChatId not set";
                            response.StatusCode = 400;
                            Console.WriteLine("? ChatId not set for /chat/getChat");
                            break;
                        }

                        Chat? respuesta = ChatListUpdated?.Invoke(_chatId);
                        if (respuesta != null)
                        {
                            responseString = System.Text.Json.JsonSerializer.Serialize(respuesta);
                            response.StatusCode = 200;
                            Console.WriteLine("? Responding to /chat/getChat with chat data");
                        }
                        else
                        {
                            responseString = "Chat not found";
                            response.StatusCode = 404;
                            Console.WriteLine("? Chat not found for /chat/getChat");
                        }

                        break;
                    case "/chat/sendmessage":
                        if (_chatId is null) 
                        {
                            responseString = "ChatId not set";
                            response.StatusCode = 400;
                            Console.WriteLine("? ChatId not set for /chat/getChat");
                            break;
                        }

                        // Leer el cuerpo de la petición
                        string requestBody;
                        using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            requestBody = await reader.ReadToEndAsync();
                        }

                        Console.WriteLine($"? Received message to send: {requestBody}");

                        // passar a json a clase Message
                        Message? newMessage = null;
                        try
                        {
                            newMessage = System.Text.Json.JsonSerializer.Deserialize<Message>(requestBody);
                            if (newMessage == null)
                            {
                                responseString = "Invalid message data";
                                response.StatusCode = 400;
                                break;
                            }

                            // Aquí podrías agregar lógica para almacenar el mensaje o procesarlo según sea necesario
                            Console.WriteLine($"? Message deserialized successfully: {newMessage.Content}");
                            bool? _CreateMessage = CreateMessage?.Invoke(_chatId, newMessage);
                            if (_CreateMessage == true)
                            {
                                responseString = "Message created successfully";
                                response.StatusCode = 200;
                            }
                            else
                            {
                                responseString = "Failed to create message";
                                response.StatusCode = 500;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"? Error deserializing message: {ex.Message}");
                            responseString = "Invalid message format";
                            response.StatusCode = 400;
                            break;
                        }

                        responseString = "Message received";
                        response.StatusCode = 200;
                        Console.WriteLine("? Responding to /chat/sendMessage");
                        break;

                    default:
                        responseString = $"NexChat WebServer - Unknown endpoint: {request.Url?.AbsolutePath}";
                        response.StatusCode = 404;
                        Console.WriteLine($"? Unknown endpoint: {request.Url?.AbsolutePath}");
                        break;
                }

                // Configurar headers de respuesta
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentEncoding = Encoding.UTF8;
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Server", "NexChat/1.0");
                
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                
                // Escribir respuesta
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
                
                Console.WriteLine($"? Response sent: {response.StatusCode} ({buffer.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error in ProcessRequest: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing response: {ex.Message}");
                }
            }
        }

        private int GetRandomAvailablePort()
        {
            // Usar un socket temporal para encontrar un puerto disponible
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            
            Console.WriteLine($"Random available port found: {port}");
            
            return port;
        }

        /// <summary>
        /// Prueba si el servidor responde correctamente haciendo una petición HTTP
        /// </summary>
        public async Task<bool> TestConnection()
        {
            if (!IsRunning)
            {
                Console.WriteLine("? Cannot test: Server is not running");
                return false;
            }

            try
            {
                Console.WriteLine($"Testing server connection on port {Port}...");
                
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                string url = $"http://localhost:{Port}/ping";
                Console.WriteLine($"? Making GET request to: {url}");
                
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"? Response received:");
                Console.WriteLine($"   Status: {(int)response.StatusCode} {response.StatusCode}");
                Console.WriteLine($"   Content: '{content}'");
                Console.WriteLine($"   Content-Type: {response.Content.Headers.ContentType}");
                
                bool success = response.IsSuccessStatusCode && content == "pong";
                
                if (success)
                {
                    Console.WriteLine($"? Test PASSED - Server is responding correctly!");
                }
                else
                {
                    Console.WriteLine($"? Test FAILED - Expected 'pong', got '{content}'");
                }
                
                return success;
            }
            catch (HttpRequestException hex)
            {
                Console.WriteLine($"? HttpRequestException: {hex.Message}");
                Console.WriteLine($"   Inner Exception: {hex.InnerException?.Message}");
                return false;
            }
            catch (TaskCanceledException tex)
            {
                Console.WriteLine($"? Request timeout: {tex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Test connection failed: {ex.Message}");
                Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}
