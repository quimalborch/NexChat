using NexChat.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class CreatedCommunity
    {
        public bool Success { get; set; } = false;
        public string? SecretCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPrevPage { get; set; }
    }

    public class CommunityChatsResponse
    {
        public List<CommunityChat> Data { get; set; } = new List<CommunityChat>();
        public PaginationInfo Pagination { get; set; } = new PaginationInfo();
    }

    /// <summary>
    /// Service for managing NexChatCC (Community Chats)
    /// Handles public community chats that can be discovered and joined by any user
    /// </summary>
    public class CommunityChatService : ICommunityChatService
    {
        private readonly ConfigurationService _configurationService;

        private string _urlApi = "https://nexchatcc.quimalborch.com";

        private List<CommunityChat> _publicCC = new List<CommunityChat>();

        public CommunityChatService(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public async Task<CreatedCommunity> CreateCommunityChatAsync(string name, string codeInvitation)
        {
            try
            {
                Thread.Sleep(5000);

                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_urlApi}/api/chats");
                var body = new
                {
                    name = name,
                    url = codeInvitation
                };
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Content = content;

                var response = await client.SendAsync(request);

                // Capturamos el body si hay error
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Error("HTTP Request Error while creating community chat. Status: {StatusCode}, Body: {Body}", response.StatusCode, errorContent);
                    return new CreatedCommunity { Success = false, ErrorMessage = $"API Error: {errorContent}" };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var newCC = new CreatedCommunity
                {
                    Success = true,
                    SecretCode = apiResponse.GetProperty("secret_key").GetString()
                };

                return newCC;
            }
            catch (HttpRequestException httpEx)
            {
                Log.Error("HTTP Request Exception: {Message}", httpEx.Message);
                return new CreatedCommunity { Success = false, ErrorMessage = "HTTP Request Error: " + httpEx.Message };
            }
            catch (Exception ex)
            {
                return new CreatedCommunity { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> DeleteCommunityChatAsync(string secretCommunity)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_urlApi}/api/chats?secret_key={secretCommunity}");
            var content = new StringContent(string.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            await response.Content.ReadAsStringAsync();

            return true;
        }

        public async Task<CommunityChatsResponse> GetCommunityChatsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_urlApi}/api/chats?page={page}&pageSize={pageSize}");
                var content = new StringContent(string.Empty);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = content;
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var result = new CommunityChatsResponse();
                var chatsForPage = new List<CommunityChat>();

                if (apiResponse.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var name = item.GetProperty("name").GetString() ?? string.Empty;
                        var url = item.GetProperty("url").GetString() ?? string.Empty;
                        var id = item.GetProperty("id").ToString();

                        if (string.IsNullOrEmpty(id)) continue;

                        CommunityChat communityChat = new CommunityChat(name, url)
                        {
                            Id = id
                        };

                        chatsForPage.Add(communityChat);
                    }
                }

                if (apiResponse.TryGetProperty("pagination", out var paginationObj))
                {
                    result.Pagination = new PaginationInfo
                    {
                        Page = paginationObj.GetProperty("page").GetInt32(),
                        PageSize = paginationObj.GetProperty("pageSize").GetInt32(),
                        Total = paginationObj.GetProperty("total").GetInt32(),
                        TotalPages = paginationObj.GetProperty("totalPages").GetInt32(),
                        HasNextPage = paginationObj.GetProperty("hasNextPage").GetBoolean(),
                        HasPrevPage = paginationObj.GetProperty("hasPrevPage").GetBoolean()
                    };
                }

                result.Data = chatsForPage;
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting community chats");
                return new CommunityChatsResponse();
            }
        }

        public async Task<CommunityChat?> GetCommunityChatByIdAsync(string communityChatId)
        {
            // TODO: Implement API call to fetch specific community chat from server
            throw new NotImplementedException("API integration pending");
        }

        public async Task<bool> UpdateCommunityChatAsync(string secretCommunity, string? name = null)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_urlApi}/api/chats");
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
