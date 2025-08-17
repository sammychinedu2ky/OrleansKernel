using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using API.Hubs;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
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


    [Experimental("SKEXP0120")]
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var agentKernel = kernel.Clone();



        agentKernel.Plugins.AddFromObject(this);
        _agent = new ChatCompletionAgent()
        {
            Name = "ChatAgent",
            Instructions =
                "You are a helpful assistant. When the user asks about their name or identity, use the 'get_user_name' function. For questions about age, use the 'get_age' function. For other questions, provide a natural language response or check available tools.",
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
        ChatHistory history = [];
        var orchestration = new MagenticOrchestration<string, CustomClientMessage>(
            manager
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
