using System;
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
        public int Port { get; private set; }
        public bool IsRunning => _listener?.IsListening ?? false;

        public bool Start()
        {
            if (IsRunning)
            {
                Console.WriteLine("Server is already running");
                return false;
            }

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
                    
                    // Solo usar localhost con trailing slash
                    string prefix = $"http://localhost:{Port}/";
                    _listener.Prefixes.Add(prefix);
                    
                    Console.WriteLine($"Prefix added: {prefix}");
                    
                    _cancellationTokenSource = new CancellationTokenSource();
                    
                    // Iniciar el listener
                    _listener.Start();
                    
                    Console.WriteLine($"? HttpListener started successfully on port {Port}");
                    Console.WriteLine($"? IsListening: {_listener.IsListening}");
                    Console.WriteLine($"? Server URL: http://localhost:{Port}/");
                    
                    // Iniciar tarea para escuchar peticiones
                    _listenerTask = Task.Run(() => HandleIncomingConnections(_cancellationTokenSource.Token));
                    
                    Console.WriteLine("? Background task started for handling connections");
                    
                    return true;
                }
                catch (HttpListenerException hlex)
                {
                    Console.WriteLine($"? HttpListenerException on port {Port}: {hlex.Message} (Error Code: {hlex.ErrorCode})");
                    
                    // Limpiar antes de intentar de nuevo
                    if (_listener != null)
                    {
                        try { _listener.Close(); } catch { }
                        _listener = null;
                    }
                    
                    if (attempt == maxAttempts - 1)
                    {
                        Console.WriteLine($"Failed to start server after {maxAttempts} attempts");
                        Stop();
                        return false;
                    }
                    
                    // Esperar un poco antes de reintentar
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Error starting web server: {ex.Message}");
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

        public void Stop()
        {
            if (_listener == null)
                return;

            Console.WriteLine($"Stopping web server on port {Port}");

            try
            {
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
                Console.WriteLine($"Processing request: {request.HttpMethod} {request.Url?.AbsolutePath}");

                string responseString;
                
                // Manejar diferentes rutas
                switch (request.Url?.AbsolutePath.ToLower())
                {
                    case "/":
                        responseString = "NexChat WebServer is running!";
                        response.StatusCode = 200;
                        Console.WriteLine("? Responding to root path");
                        break;
                        
                    case "/ping":
                        responseString = "pong";
                        response.StatusCode = 200;
                        Console.WriteLine("? Responding to /ping with pong");
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
