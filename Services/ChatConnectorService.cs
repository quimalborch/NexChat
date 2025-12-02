using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NexChat.Data;
using NexChat.Security;

namespace NexChat.Services
{
    public class ChatConnectorService
    {
        private readonly HttpClient _httpClient;

        public ChatConnectorService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<Chat?> GetChat(string url)
        {
            try
            {
                // Construir la URL completa
                string fullUrl = $"https://{url}.trycloudflare.com/chat/getChat";
                
                // Hacer la petición GET
                HttpResponseMessage response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                
                // Leer el contenido de la respuesta
                string jsonContent = await response.Content.ReadAsStringAsync();
                
                // Deserializar el JSON a un objeto Chat
                Chat? chat = JsonSerializer.Deserialize<Chat>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return chat;
            }
            catch (HttpRequestException ex)
            {
                // Manejar errores de HTTP
                Console.WriteLine($"Error en la petición HTTP: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                // Manejar errores de deserialización
                Console.WriteLine($"Error al deserializar JSON: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Message>?> GetNewMessages(string url, DateTime since)
        {
            try
            {
                // Construir la URL completa con el timestamp
                long ticks = since.ToUniversalTime().Ticks;
                string fullUrl = $"https://{url}.trycloudflare.com/chat/getNewMessages?since={ticks}";
                
                Console.WriteLine($"? Polling new messages from {url} since {since}");
                
                // Hacer la petición GET
                HttpResponseMessage response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                
                // Leer el contenido de la respuesta
                string jsonContent = await response.Content.ReadAsStringAsync();
                
                // Deserializar el JSON a una lista de mensajes
                List<Message>? messages = JsonSerializer.Deserialize<List<Message>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Console.WriteLine($"? Received {messages?.Count ?? 0} new messages");
                return messages ?? new List<Message>();
            }
            catch (HttpRequestException ex)
            {
                // Manejar errores de HTTP (no loggear para evitar spam en el polling)
                return null;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error al deserializar JSON: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SendMessage(string url, Message message)
        {
            try
            {
                // Construir la URL completa
                string fullUrl = $"https://{url}.trycloudflare.com/chat/sendMessage";

                // Hacer la petición POST
                HttpContent content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(fullUrl, content);
                response.EnsureSuccessStatusCode();

                if (response.StatusCode is System.Net.HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            }
            catch (HttpRequestException ex)
            {
                // Manejar errores de HTTP
                Console.WriteLine($"Error en la petición HTTP: {ex.Message}");
                return false;
            }
            catch (JsonException ex)
            {
                // Manejar errores de serialización
                Console.WriteLine($"Error al serializar JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 🔐 Obtiene la clave pública del servidor remoto
        /// </summary>
        public async Task<PublicKeyExchange?> GetPublicKey(string url)
        {
            try
            {
                string fullUrl = $"https://{url}.trycloudflare.com/security/publickey";
                
                Console.WriteLine($"🔑 Fetching public key from {url}");
                
                HttpResponseMessage response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();
                
                string jsonContent = await response.Content.ReadAsStringAsync();
                
                PublicKeyExchange? keyExchange = JsonSerializer.Deserialize<PublicKeyExchange>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                Console.WriteLine($"✅ Public key fetched successfully for user: {keyExchange?.DisplayName}");
                return keyExchange;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ Error fetching public key: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Error deserializing public key: {ex.Message}");
                return null;
            }
        }
    }
}
