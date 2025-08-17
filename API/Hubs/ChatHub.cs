using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using API.Grains;
using API.Plugins;
using API.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.JSInterop;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Magentic;
using Microsoft.SemanticKernel.Agents.Orchestration.Concurrent;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Orchestration.Sequential;
using Microsoft.SemanticKernel.Agents.Orchestration.Transforms;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace API.Hubs;


public interface IChatHub
{
    Task ReceiveMessage(string chatRoomId, CustomClientMessage message);

}

[GenerateSerializer]
public class CustomClientMessage
{
    [Id(0)]
    public required string Text { get; set; }
    [Id(1)]
    public required string Role { get; set; } = "user"; // default role is user
    [Id(2)]
    public List<FileMessage> Files { get; set; } = [];
}

[GenerateSerializer]
public class FileMessage
{
    [Id(0)]
    public required string FileId { get; set; }
    [Id(1)]
    public required string FileName { get; set; }
    [Id(2)]
    public required string FileType { get; set; }
    [Id(3)]
    public string? Text { get; set; }
}
public class ChatHub(
    IGrainFactory grainFactory,
    Kernel kernel,
    ILogger<ChatHub> logger) : Hub<IChatHub>
{
   [Experimental("SKEXP0110")]
public async Task SendMessageToModel(string chatId, CustomClientMessage message)
{
    var claimPrincipal = Context.User!;
    var userId = RetrieveUserId.GetUserId(claimPrincipal);

    logger.LogInformation("User {UserId} sent message to chat room {ChatId}: {MessageText}", userId, chatId, message.Text);

    if (string.IsNullOrEmpty(message.Text))
    {
        logger.LogWarning("Empty message text received for chatId: {ChatId}", chatId);
        await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "Please provide a message.", Role = "assistant" });
        return;
    }

    try
    {
        var agentKernel = kernel.Clone();
        logger.LogDebug("Cloned Kernel for chatId: {ChatId}", chatId);

        agentKernel.Plugins.AddFromType<ChatPlugin>("chat_plugin");
        foreach (var plugin in agentKernel.Plugins)
        {
            foreach (var function in plugin)
            {
                logger.LogDebug("Registered plugin: {PluginName}, Function: {FunctionName}, Description: {Description}",
                    plugin.Name, function.Name, function.Description);
            }
        }

        var agent = new ChatCompletionAgent()
        {
            Name = "ChatAgent",
            Instructions = "You are a helpful assistant. When the user asks about their name or identity, use the 'get_user_name' function. For questions about age, use the 'get_age' function. For other questions, provide a natural language response or check available tools.",
            Kernel = agentKernel,
            Arguments = new(new AzureOpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };
        logger.LogDebug("ChatCompletionAgent created with name: {AgentName}", agent.Name);

        var chatCompletionService = agent.Kernel.GetRequiredService<IChatCompletionService>();
        logger.LogDebug("Retrieved IChatCompletionService for chatId: {ChatId}", chatId);
        var grain = grainFactory.GetGrain<IChatGrain>(chatId);
        logger.LogDebug("Retrieved ChatGrain for chatId: {ChatId}", chatId);
        var res = await grain.SendMessageAsync(userId, message);
        Clients.Caller.ReceiveMessage(chatId, res);
        return;
        // var manager = new StandardMagenticManager(
        //     chatCompletionService,
        //     new AzureOpenAIPromptExecutionSettings()
        //     {
        //         ChatSystemPrompt = "You are a helpful assistant. When the available agents plugins to answer the users question",
        //             FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        //             ChatDeveloperPrompt = "You are a helpful assistant. Use the available functions to answer the user's question when applicable."
        //             
        //
        //     })
        // {
        //     MaximumInvocationCount = 5,
        // };
        // logger.LogDebug("StandardMagenticManager created with max invocations: {MaxInvocations}", manager.MaximumInvocationCount);
        // ChatHistory history = [];
        // var orchestration = new MagenticOrchestration<string,CustomClientMessage>(
        //     manager
        //     , agent,agent)
        // {
        //     ResponseCallback = (ChatMessageContent response) =>
        //     {
        //         history.Add(response);
        //         logger.LogDebug("Orchestration response: {Response}", response.Content);
        //         return ValueTask.CompletedTask;
        //     }, 
        //     ResultTransform = new StructuredOutputTransform<CustomClientMessage>(
        //         chatCompletionService,
        //         new OpenAIPromptExecutionSettings
        //         {
        //             ResponseFormat = typeof(CustomClientMessage)
        //         }
        //     ).TransformAsync,
        //
        // };
        //
        // logger.LogInformation("Starting InProcessRuntime...");
        // var runTime = new InProcessRuntime();
        // await runTime.StartAsync();
        // logger.LogInformation("InProcessRuntime started.");
        //
        // logger.LogInformation("Invoking orchestration...");
        // var result = await orchestration.InvokeAsync(message.Text, runTime);
        //
        // logger.LogInformation("Collecting orchestration results...");
        // var resulty = await result.GetValueAsync(TimeSpan.FromSeconds(240)); // Reduced timeout for faster feedback
        // if (resulty == null)
        // {
        //     logger.LogWarning("Orchestration returned null result for chatId: {ChatId}", chatId);
        //     await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "No response generated.", Role = "assistant" });
        // }
        // else
        // {
        //     logger.LogInformation("Orchestration result: {Result}", JsonSerializer.Serialize(resulty));
        //     await Clients.Caller.ReceiveMessage(chatId, resulty); // Fixed: Send resulty
        // }

        logger.LogInformation("Running InProcessRuntime until idle...");
       // await runTime.RunUntilIdleAsync();
        logger.LogInformation("InProcessRuntime completed.");
    }
    catch (TimeoutException ex)
    {
        logger.LogError(ex, "Timeout during orchestration for chatId: {ChatId}", chatId);
        await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "Request timed out.", Role = "assistant" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during orchestration for chatId: {ChatId}", chatId);
        await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "An error occurred: " + ex.Message, Role = "assistant" });
    }
}
}
