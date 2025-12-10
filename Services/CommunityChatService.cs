using NexChat.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class CreatedCommunity
    {
        public bool Success { get; set; } = false;
        public string? SecretCode { get; set; }
    }

    /// <summary>
    /// Service for managing NexChatCC (Community Chats)
    /// Handles public community chats that can be discovered and joined by any user
    /// </summary>
    public class CommunityChatService : ICommunityChatService
    {
        private readonly ConfigurationService _configurationService;

        private string _urlApi = "http://localhost:3000/api/chats";

        private List<CommunityChat> _publicCC = new List<CommunityChat>();

        public CommunityChatService(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public async Task<CreatedCommunity> CreateCommunityChatAsync(string name, string codeInvitation)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, _urlApi);
            var body = new
            {
                name = name,
                url = codeInvitation
            };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var newCC = new CreatedCommunity
            {
                Success = true,
                SecretCode = apiResponse.GetProperty("secret_key").GetString()
            };

            return newCC;
        }

        public async Task<bool> DeleteCommunityChatAsync(string secretCommunity)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_urlApi}?secret_key={secretCommunity}");
            var content = new StringContent(string.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await response.Content.ReadAsStringAsync();

            return true;
        }

        public async Task<List<CommunityChat>> GetCommunityChatsAsync()
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_urlApi}?page=1&pageSize=5");
            var content = new StringContent(string.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            Console.WriteLine(_publicCC);

            if (apiResponse.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    //var description = item.GetProperty("description").GetString() ?? string.Empty;
                    var url = item.GetProperty("url").GetString() ?? string.Empty;
                    var id = item.GetProperty("id").ToString();

                    if (string.IsNullOrEmpty(id)) continue;

                    if (_publicCC.Any(cc => cc.Id == id)) continue;

                    CommunityChat communityChat = new CommunityChat(name, url)
                    {
                        Id = id
                    };

                    _publicCC.Add(communityChat);
                }
            }

            return _publicCC;
        }

        public async Task<CommunityChat?> GetCommunityChatByIdAsync(string communityChatId)
        {
            // TODO: Implement API call to fetch specific community chat from server
            throw new NotImplementedException("API integration pending");
        }

        public async Task<bool> UpdateCommunityChatAsync(string secretCommunity, string? name = null)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Put, _urlApi);
            var body = new
            {
                name = name,
                secret_key = secretCommunity
            };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await response.Content.ReadAsStringAsync();
            
            return true;

        }

        public async Task<List<CommunityChat>> SearchCommunityChatsAsync(string name)
        {
            // TODO: Implement API call to search community chats on NexChat community server
            throw new NotImplementedException("API integration pending");
        }
    }
}
