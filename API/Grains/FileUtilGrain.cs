using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using API.Hubs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Orchestration.Transforms;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace API.Grains;

public interface IChatGrain : IGrainWithStringKey
{
    Task<int> MyAgeAsync();
    Task<string> WhoAmIAsync();

    // Instead of returning the agent, let Orleans do the work
    Task<CustomClientMessage> SendMessageAsync(string userId, CustomClientMessage message);
}

public class AgentThreadState
{
   public ChatHistoryAgentThread Thread { get; set; } 
}
public class ChatGrain : Grain, IChatGrain
{
    private ChatCompletionAgent _agent;
    private readonly Kernel kernel;
    private readonly ILogger<ChatGrain> logger;

    public IPersistentState<AgentThreadState> history;
    public  ChatGrain(
        [PersistentState("history", "default")] IPersistentState<AgentThreadState> history, Kernel _kernel, ILogger<ChatGrain> _logger)
    {
        this.history = history;
        this.kernel = _kernel;
        this.logger = _logger;
    }
    
    [Experimental("SKEXP0120")]
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();



        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent()
        {
            Name = "ChatAgent",
            Description = "A chat agent",
            Instructions =
                "You are a helpful assistant. When the user asks about their name or identity, use the 'get_user_name' function. For questions about age, use the 'get_age' function. For other questions, provide a natural language response or check available tools.",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ResponseFormat = typeof(CustomClientMessage),
            })
        };

        // if (history.State.Thread == null)
        // {
        //     history.State.Thread = new ChatHistoryAgentThread();
        // }
        // try
        // {
        //     await history.stReadStateAsync();
        //     logger.LogDebug("Successfully read state with {Count} messages", history.State.Thread.ChatHistory.Count);
        // }
        // catch (Exception ex)
        // {
        //     logger.LogError(ex, "Failed to read state");
        // }

        await base.OnActivateAsync(cancellationToken);

       
    }

    [KernelFunction("get_age")]
    [Description("Get the age of the user.")]
    public Task<int> MyAgeAsync() => Task.FromResult(25);

    [KernelFunction("who_am_i")]
    [Description("Get the identity of the user.")]
    public Task<string> WhoAmIAsync() => Task.FromResult("I am a Swacblooms.");

    [Experimental("SKEXP0010")]
    public async Task<CustomClientMessage> SendMessageAsync(string userId, CustomClientMessage message)
    {
        // Process with the internal agent
        var chatCompletionService = _agent.Kernel.GetRequiredService<IChatCompletionService>();
        var PromptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(CustomClientMessage),
             
        };
        List< AgentResponseItem<ChatMessageContent>> res = new();
        //var result = await  _agent.InvokeAsync(message.Text).FirstAsync();
       
        await foreach (var msg in _agent.InvokeAsync(message.Text, history.State.Thread))
        {
            res.Add(msg);
            
        }

        var response = res.LastOrDefault().Message.Content;
        history.State.Thread = (ChatHistoryAgentThread)res.LastOrDefault().Thread;
        Console.WriteLine(response);
        logger.LogInformation(JsonSerializer.Serialize(history.State.Thread.ChatHistory));
        await history.WriteStateAsync();
        return JsonSerializer.Deserialize<CustomClientMessage>(response);
    }
}
