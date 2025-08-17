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
   // [Experimental("SKEXP0110")]
public async Task SendMessageToModel(string chatId, CustomClientMessage message)
{
    var claimPrincipal = Context.User!;
    var userId = RetrieveUserId.GetUserId(claimPrincipal);
    logger.LogInformation("User {UserId} sent message to chat room {ChatId}: {MessageText}", userId, chatId, message.Text);
    try
    {
        var grain = grainFactory.GetGrain<IChatGrain>(chatId);
        logger.LogDebug("Retrieved ChatGrain for chatId: {ChatId}", chatId);
        var res = await grain.SendMessageAsync(userId, message);
        await Clients.Caller.ReceiveMessage(chatId, res);
        return;
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
