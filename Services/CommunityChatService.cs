using NexChat.Data;
using Serilog;
using System;
using System.Collections.Generic;
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

        public CommunityChatService(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public async Task<CreatedCommunity> CreateCommunityChatAsync(string name, string description, string codeInvitation)
        {
            // TODO: Implement API call to NexChat community server to register the chat
            throw new NotImplementedException("API integration pending");
        }

        public async Task<bool> DeleteCommunityChatAsync(string secretCommunity)
        {
            // TODO: Implement API call to NexChat community server to remove the chat from public list
            throw new NotImplementedException("API integration pending");
        }

        public async Task<List<CommunityChat>> GetCommunityChatsAsync()
        {
            // TODO: Implement API call to fetch community chats from NexChat community server
            throw new NotImplementedException("API integration pending");
        }

        public async Task<CommunityChat?> GetCommunityChatByIdAsync(string communityChatId)
        {
            // TODO: Implement API call to fetch specific community chat from server
            throw new NotImplementedException("API integration pending");
        }

        public async Task<bool> UpdateCommunityChatAsync(string communityChatId, string? name = null, string? description = null)
        {
            // TODO: Implement API call to update community chat on NexChat community server
            throw new NotImplementedException("API integration pending");
        }

        public async Task<List<CommunityChat>> SearchCommunityChatsAsync(string name)
        {
            // TODO: Implement API call to search community chats on NexChat community server
            throw new NotImplementedException("API integration pending");
        }
    }
}
