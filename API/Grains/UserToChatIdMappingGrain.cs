using API.Hubs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace API.Grains;

public interface IUserToChatIdMappingGrain : IGrainWithStringKey
{
    Task SaveUserToChatIdAsync(string chatId, CustomClientMessage message);
    Task<List<ChatPages>> GetChatPagesAsync();
}

[GenerateSerializer]
public class ChatPages
{
    [Id(0)] public string? Title { get; set; }

    [Id(1)] public string? ChatId { get; set; }
}

[GenerateSerializer]
public class ChatPagesState
{
    [Id(0)] public Dictionary<string, ChatPages> ChatPages { get; set; } = new();
}

public class UserToChatIdMappingGrain : Grain, IUserToChatIdMappingGrain
{
    private readonly IPersistentState<ChatPagesState> _chatPagesState;
    private readonly Kernel _kernel;
    private readonly ILogger<UserToChatIdMappingGrain> _logger;
    private ChatCompletionAgent? _agent;

    public UserToChatIdMappingGrain(
        [PersistentState("chatPages", "default")]
        IPersistentState<ChatPagesState> chatPagesState,
        ILogger<UserToChatIdMappingGrain> logger, Kernel kernel)
    {
        _chatPagesState = chatPagesState;
        _logger = logger;
        _kernel = kernel;
    }

    public async Task SaveUserToChatIdAsync(string chatId, CustomClientMessage message)
    {
        if (!_chatPagesState.State.ChatPages.ContainsKey(chatId))
        {
            var summary = await SummarizeChatAsync(message);
            _chatPagesState.State.ChatPages[chatId] = new ChatPages
            {
                Title = summary,
                ChatId = chatId
            };
        }
    }

    public Task<List<ChatPages>> GetChatPagesAsync()
    {
        return Task.FromResult(_chatPagesState.State.ChatPages.Values.ToList());
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = _kernel.Clone();

        _agent = new ChatCompletionAgent
        {
            Name = "ChatSummaryAgent",
            Description = "A chat summary agent",
            Instructions =
                "You are a helpful chat summary agent that summarizes chat conversations. I don't need more than 10 words. So try to ensure is below it. The shorter the better.",
            Kernel = agentKernel
        };
        await base.OnActivateAsync(cancellationToken);
    }

    private async Task<string> SummarizeChatAsync(CustomClientMessage message)
    {
        List<AgentResponseItem<ChatMessageContent>> res = new();
        await foreach (var msg in _agent.InvokeAsync(message.ToString())) res.Add(msg);
        var response = res.LastOrDefault()?.Message.Content;
        return response ?? "Some conversation going on";
    }
}