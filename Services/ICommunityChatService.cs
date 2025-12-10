using NexChat.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NexChat.Services
{
    /// <summary>
    /// Interface for NexChatCC (Community Chats) service
    /// Manages public community chats that can be discovered and joined by any user
    /// </summary>
    public interface ICommunityChatService
    {
        /// <summary>
        /// Creates a new community chat and publishes it to the public list
        /// </summary>
        /// <param name="name">Name of the community chat</param>
        /// <param name="codeInvitation">Invitation code for the chat</param>
        /// <returns>The created CommunityChat or null if creation failed</returns>
        Task<CreatedCommunity> CreateCommunityChatAsync(string name, string codeInvitation);

        /// <summary>
        /// Deletes a community chat from the public list
        /// </summary>
        /// <param name="secretCommunity">Secret of the community chat to delete</param>
        /// <returns>True if deletion was successful, false otherwise</returns>
        Task<bool> DeleteCommunityChatAsync(string secretCommunity);

        /// <summary>
        /// Gets a list of all available community chats
        /// </summary>
        /// <returns>List of available community chats</returns>
        Task<List<CommunityChat>> GetCommunityChatsAsync();

        /// <summary>
        /// Gets a specific community chat by ID
        /// </summary>
        /// <param name="communityChatId">ID of the community chat</param>
        /// <returns>The community chat or null if not found</returns>
        Task<CommunityChat?> GetCommunityChatByIdAsync(string communityChatId);

        /// <summary>
        /// Updates community chat information
        /// </summary>
        /// <param name="communityChatId">ID of the community chat to update</param>
        /// <param name="name">New name (optional)</param>
        /// <param name="description">New description (optional)</param>
        /// <returns>True if update was successful, false otherwise</returns>
        Task<bool> UpdateCommunityChatAsync(string secretCommunity, string? name = null);

        /// <summary>
        /// Searches for community chats by name or description
        /// </summary>
        /// <param name="query">Search query</param>
        /// <returns>List of matching community chats</returns>
        Task<List<CommunityChat>> SearchCommunityChatsAsync(string name);
    }
}
