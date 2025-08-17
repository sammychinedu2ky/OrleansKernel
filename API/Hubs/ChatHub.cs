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
        agentKernel.Plugins.AddFromType<ChatPlugin>("chat_plugin");
        logger.LogInformation("Available plugins: {Plugins}", string.Join(", ", agentKernel.Plugins.Select(p => p.Name)));
        
        var agent = new ChatCompletionAgent()
        {
            Name = "ChatAgent",
            Instructions = "You are a helpful assistant. When the user asks a question, check the available tools and provide a response.",
            Kernel = agentKernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            })
            
            
        };
            // await foreach (var response in agent.InvokeAsync(
            //     new ChatMessageContent(AuthorRole.User, message.Text),
            //     null,
            //    null,
            //     CancellationToken.None))
            // {
            //     // Process each response, e.g.:
            //     logger.LogInformation("Agent response: {Response}", response.Message);
            //     await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = response.Message.ToString(), Role = "assistant" });
            //     return;
            // }
            var chatCompletionService = agent.Kernel.GetRequiredService<IChatCompletionService>();
            var manager = new StandardMagenticManager(
                chatCompletionService,
                new OpenAIPromptExecutionSettings())
            {
                MaximumInvocationCount = 5,
            };

            ChatHistory history = [];
            var orchestration = new SequentialOrchestration(agent)
            {
                ResponseCallback = (ChatMessageContent response) =>
                {
                    history.Add(response);
                    logger.LogInformation("Orchestration response: {Response}", response.Content);
                    return ValueTask.CompletedTask;
                }
            };

            logger.LogInformation("Starting InProcessRuntime...");
            var runTime = new InProcessRuntime();
            await runTime.StartAsync();
            logger.LogInformation("InProcessRuntime started.");

            logger.LogInformation("Invoking orchestration...");
            var result = await orchestration.InvokeAsync(message.Text, runTime);

            logger.LogInformation("Collecting orchestration results...");
            var resulty = await result.GetValueAsync(TimeSpan.FromSeconds(300));
            if (resulty == null)
            {
                logger.LogWarning("Orchestration returned null result for chatId: {ChatId}", chatId);
                await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "No response generated.", Role = "assistant" });
            }
            else
            {
                logger.LogInformation("Orchestration result: {Result}", JsonSerializer.Serialize(resulty));
                await Clients.Caller.ReceiveMessage(chatId, default);
                
                
                
                
                
                
                
                
            }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during orchestration for chatId: {ChatId}", chatId);
        await Clients.Caller.ReceiveMessage(chatId, new CustomClientMessage { Text = "An error occurred: " + ex.Message, Role = "assistant" });
    }
}
}
