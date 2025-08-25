using System.Security.Claims;
using API.Grains;
using API.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;

namespace API.Hubs;

public interface IChatHub
{
    Task ReceiveMessage(string chatRoomId, CustomClientMessage message);
}

[GenerateSerializer]
public class CustomClientMessage
{
    [Id(0)] public required string Text { get; set; }

    [Id(1)] public required string Role { get; set; } = "user"; // default role is user

    [Id(2)] public List<FileMessage> Files { get; set; } = [];

    // I need the to string representation
    public override string ToString()
    {
        var fileStrings = Files.Select(f => f.ToString());
        return $"Text: {Text}, Role: {Role}, Files: [{string.Join(", ", fileStrings)}]";
    }
}

[GenerateSerializer]
public class FileMessage
{
    [Id(0)] public required string FileId { get; set; }

    [Id(1)] public required string FileName { get; set; }

    [Id(2)] public required string FileType { get; set; }

    [Id(3)] public string? Text { get; set; }

    // the to string representation
    public override string ToString()
    {
        return $"FileId: {FileId}, FileName: {FileName}, FileType: {FileType}, Text: {Text}";
    }
}

public class ChatHub(
    IGrainFactory grainFactory,
    Kernel kernel,
    ILogger<ChatHub> logger) : Hub<IChatHub>
{
    private bool IsAuthenticated(ClaimsPrincipal? user)
    {
        return user?.Identity?.IsAuthenticated ?? false;
    }

    public async Task SendMessageToModel(string chatId, CustomClientMessage message)
    {
        var claimPrincipal = Context.User!;
        var userId = RetrieveUserId.GetUserId(claimPrincipal);
        var chatSavingGrain = grainFactory.GetGrain<IChatSavingGrain>(userId + ":" + chatId);
        if (IsAuthenticated(Context.User))
        {
            var userToChatIdMappingGrain = grainFactory.GetGrain<IUserToChatIdMappingGrain>(userId);
            await userToChatIdMappingGrain.SaveUserToChatIdAsync(chatId, message);
            await chatSavingGrain.SaveChat(userId, chatId, message);
        }

        logger.LogInformation("User {UserId} sent message to chat room {ChatId}: {MessageText}", userId, chatId,
            message.Text);
        try
        {
            var fileUtilGrain = grainFactory.GetGrain<IFileUtilGrain>(chatId);
            logger.LogDebug("Retrieved ChatGrain for chatId: {ChatId}", chatId);
            var res = await fileUtilGrain.SendMessageAsync(message);
            if (IsAuthenticated(Context.User)) await chatSavingGrain.SaveChat(userId, chatId, res);
            await Clients.Caller.ReceiveMessage(chatId, res);
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Timeout during orchestration for chatId: {ChatId}", chatId);
            var res = new CustomClientMessage { Text = "Request timed out.", Role = "assistant" };
            if (IsAuthenticated(Context.User)) await chatSavingGrain.SaveChat(userId, chatId, res);
            await Clients.Caller.ReceiveMessage(chatId, res);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during orchestration for chatId: {ChatId}", chatId);
            var res = new CustomClientMessage { Text = "An error occurred: " + ex.Message, Role = "assistant" };
            if (IsAuthenticated(Context.User)) await chatSavingGrain.SaveChat(userId, chatId, res);
            await Clients.Caller.ReceiveMessage(chatId, res);
        }
    }
}