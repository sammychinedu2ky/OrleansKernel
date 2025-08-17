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

public class ChatGrain(Kernel kernel, ILogger<ChatGrain> logger) : Grain, IChatGrain
{
    private ChatCompletionAgent _agent;
    private ChatCompletionAgent writer;
    private ChatCompletionAgent editor;
    public  ChatHistory history = [];

    [Experimental("SKEXP0120")]
    public override Task OnActivateAsync(CancellationToken cancellationToken)
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
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };
         writer = new ChatCompletionAgent {
            Name = "CopyWriter",
            // Description = "A copy writer",
            Instructions = "You are a copywriter with ten years of experience and are known for brevity and a dry humor. The goal is to refine and decide on the single best copy as an expert in the field. Only provide a single proposal per response. You're laser focused on the goal at hand. Don't waste time with chit chat. Consider suggestions when refining an idea.",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        editor = new ChatCompletionAgent {
            Name = "Reviewer",
            // Description = "An editor.",
            Instructions = "You are an art director who has opinions about copywriting born of a love for David Ogilvy. The goal is to determine if the given copy is acceptable to print. If so, state that it is approved. If not, provide insight on how to refine suggested copy without example.",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        return Task.CompletedTask;
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

        var manager = new StandardMagenticManager(
            chatCompletionService,
            new AzureOpenAIPromptExecutionSettings()
            {
                ChatSystemPrompt =
                    "You are a helpful assistant. When the available agents plugins to answer the users question",
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ChatDeveloperPrompt =
                    "You are a helpful assistant. Use the available functions to answer the user's question when applicable."


            })
        {
            MaximumInvocationCount = 5,
        };
        logger.LogDebug("StandardMagenticManager created with max invocations: {MaxInvocations}",
            manager.MaximumInvocationCount);
       
        var orchestration = new GroupChatOrchestration<string, CustomClientMessage>(
            new RoundRobinGroupChatManager { MaximumInvocationCount = 5 }

            , _agent)
        {
            ResponseCallback = (ChatMessageContent response) =>
            {
                history.Add(response);
                logger.LogDebug("Orchestration response: {Response}", response.Content);
                return ValueTask.CompletedTask;
            },
            ResultTransform = new StructuredOutputTransform<CustomClientMessage>(
                chatCompletionService,
                new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = typeof(CustomClientMessage)
                }
            ).TransformAsync,

        };

        logger.LogInformation("Starting InProcessRuntime...");
        var runTime = new InProcessRuntime();
        await runTime.StartAsync();
        logger.LogInformation("InProcessRuntime started.");

        logger.LogInformation("Invoking orchestration...");
        var result = await orchestration.InvokeAsync(message.Text, runTime);

        logger.LogInformation("Collecting orchestration results...");
        var resulty = await result.GetValueAsync(TimeSpan.FromSeconds(240)); // Reduced timeout for faster feedback
        if (resulty == null)
        {
            // logger.LogWarning("Orchestration returned null result for chatId: {ChatId}", chatId);
           return await Task.FromResult(new CustomClientMessage { Text = "No response generated.", Role = "assistant" });
        }
        else
        {
            logger.LogInformation("Orchestration result: {Result}", JsonSerializer.Serialize(resulty));
           return await Task.FromResult(resulty); // Fixed: Send resulty

        }
    }
}
