using API.Hubs;

namespace API.Grains;

public interface IChatSavingGrain : IGrainWithStringKey
{
    Task SaveChat(string userId, string chatId, CustomClientMessage message);
    ValueTask<List<CustomClientMessage>> GetChatMessages(string userId);
}

[GenerateSerializer]
public class ChatHistoryState
{
    [Id(0)] public List<CustomClientMessage> ChatHistory { get; set; } = new();
}

[GenerateSerializer]
public class UserIdState
{
    [Id(0)] public string? UserId { get; set; }
}

public class ChatSavingGrain : Grain, IChatSavingGrain
{
    private readonly IPersistentState<ChatHistoryState> _chatHistory;
    private readonly ILogger<ChatSavingGrain> _logger;
    private readonly IPersistentState<UserIdState> _userId;

    public ChatSavingGrain(
        [PersistentState("chatHistory", "default")]
        IPersistentState<ChatHistoryState> chatHistory,
        [PersistentState("userId", "default")] IPersistentState<UserIdState> userId,
        ILogger<ChatSavingGrain> logger)
    {
        _chatHistory = chatHistory;
        _userId = userId;
        _logger = logger;
    }

    public ChatSavingGrain(ILogger<ChatSavingGrain> logger)
    {
        _logger = logger;
    }

    public async ValueTask<List<CustomClientMessage>> GetChatMessages(string userId)
    {
        // Retrieve chat messages from the persistent state
        if (userId != _userId.State.UserId)
        {
            _logger.LogWarning("User ID mismatch: expected {ExpectedUserId}, got {ActualUserId}", _userId.State.UserId,
                userId);
            return new List<CustomClientMessage>();
        }

        return _chatHistory.State.ChatHistory;
    }

    public async Task SaveChat(string userId, string chatId, CustomClientMessage message)
    {
        // Ensure the chat history is initialized
        if (_userId.State.UserId == null)
        {
            _userId.State.UserId = userId;
            await _userId.WriteStateAsync();
        }

        // Add the message to the chat history
        _chatHistory.State.ChatHistory.Add(message);

        // Log the message saving action
        _logger.LogInformation("Saving message to chat {ChatId}: {Message}", chatId, message.ToString());

        // Write the updated state back to persistent storage
        await _chatHistory.WriteStateAsync();
    }
}