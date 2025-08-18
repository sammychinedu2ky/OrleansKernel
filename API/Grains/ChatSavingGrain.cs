using API.Endpoints;
using API.Hubs;

namespace API.Grains;

public interface IChatSavingGrain : IGrainWithStringKey
{
    Task SaveChat(string userId, string chatId, CustomClientMessage message);
    ValueTask<List<CustomClientMessage>> GetChatMessages(string userId, string chatId);
}
[GenerateSerializer]
public class ChatHistoryState{
    [Id(0)]
    public List<CustomClientMessage> ChatHistory { get; set; } = new();
}
public class ChatSavingGrain : Grain, IChatSavingGrain
{

    private readonly IPersistentState<ChatHistoryState> _chatHistory;
    private readonly IPersistentState<string?> _userId;
    private readonly ILogger<ChatSavingGrain> _logger;
    public ChatSavingGrain(
        [PersistentState("chatHistory", "default")] IPersistentState<ChatHistoryState> chatHistory,
        [PersistentState("userId", "default")] IPersistentState<string?> userId,
        ILogger<ChatSavingGrain> logger)
    {
        this._chatHistory = chatHistory;
        this._userId = userId;
        _logger = logger;
    }

    public ChatSavingGrain(ILogger<ChatSavingGrain> logger)
    {
        _logger = logger;
    }

    public async ValueTask<List<CustomClientMessage>> GetChatMessages(string userId, string chatId)
    {
        // Retrieve chat messages from the persistent state
        if(userId != _userId.State)
        {
            _logger.LogWarning("User ID mismatch: expected {ExpectedUserId}, got {ActualUserId}", _userId.State, userId);
            return new List<CustomClientMessage>();
        }
        return _chatHistory.State.ChatHistory;
    }

    public async Task SaveChat(string userId,string chatId, CustomClientMessage message)
    {
        // Ensure the chat history is initialized
        if (_chatHistory.State == null)
        {
            _userId.State = userId;
            await _userId.WriteStateAsync();
            _chatHistory.State = new ChatHistoryState();
        }

        // Add the message to the chat history
        _chatHistory.State.ChatHistory.Add(message);

        // Log the message saving action
        _logger.LogInformation("Saving message to chat {ChatId}: {Message}", chatId, message.ToString());

        // Write the updated state back to persistent storage
        await _chatHistory.WriteStateAsync();
    }
}
