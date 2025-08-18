using API.Hubs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace API.Grains;

public interface ISaveUserToChatIdGrain : IGrainWithGuidKey
{
    Task SaveUserToChatIdAsync(string userId, string chatId, CustomClientMessage message);
    Task<List<ChatPages>> GetChatPagesAsync();
}

public class ChatPages
{
    public string? Title { get; set; }
    public string? ChatId { get; set; }
}

public class SaveUserToChatIdGrain : Grain, ISaveUserToChatIdGrain
{
    private readonly IPersistentState<Dictionary<string, ChatPages>> _chatPagesState;
    private readonly ILogger<SaveUserToChatIdGrain> _logger;
    private readonly Kernel _kernel;
    private ChatCompletionAgent? _agent;

    public SaveUserToChatIdGrain(
        [PersistentState("chatPages", "default")] IPersistentState<Dictionary<string, ChatPages>> chatPagesState,
        ILogger<SaveUserToChatIdGrain> logger, Kernel kernel)
    {
        _chatPagesState = chatPagesState;
        _logger = logger;
        _kernel = kernel;
    }
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = _kernel.Clone();
        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent()
        {
            Name = "ChatSummaryAgent",
            Description = "A chat summary agent",
            Instructions =
                "You are a helpful chat summary agent that summarizes chat conversations. I don't need more than 10 words. So try to ensure is below it. The shorter the better.",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = typeof(CustomClientMessage),
            })
        };
        await base.OnActivateAsync(cancellationToken);
    }
    public async Task SaveUserToChatIdAsync(string userId, string chatId, CustomClientMessage message)
    {
        if (!_chatPagesState.State.ContainsKey(chatId))
        {
            var summary = await SummarizeChatAsync(message);
            _chatPagesState.State[chatId] = new ChatPages
            {
                Title = summary,
                ChatId = chatId
            };
        }
    }

    private async Task<string> SummarizeChatAsync(CustomClientMessage message)
    {
        List<AgentResponseItem<ChatMessageContent>> res = new();
        await foreach (var msg in _agent.InvokeAsync(message.ToString()))
        {
            res.Add(msg);
        }
        var response = res.LastOrDefault()?.Message.Content;
        return response ?? "Some conversation going on";
    }

    public Task<List<ChatPages>> GetChatPagesAsync()
    {
        return Task.FromResult(_chatPagesState.State.Values.ToList());
    }
}