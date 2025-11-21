using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NexChat.Data;

namespace NexChat.Services
{
    public class ChatConnectorService
    {
        private readonly HttpClient _httpClient;

        public ChatConnectorService()
        {
            _httpClient = new HttpClient();
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
    }
}
