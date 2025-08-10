using API.Util;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs;


public interface IChatHub
{
    Task ReceiveMessage(string chatRoomId, CustomClientMessage message);
    
}

public class CustomClientMessage
{
    public required string Test { get; set; }
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
    ILogger<ChatHub> logger) : Hub<IChatHub>
{
    public async Task JoinChatRoom(string chatRoomId)
    {
        var claimPrincipal = Context.User!;
        var isAuthenticated = claimPrincipal.Identity?.IsAuthenticated ?? false;
        var userId = RetrieveUserId.GetUserId(claimPrincipal);
        // need to store the userId and chatRoomId in a grain
        logger.LogInformation("User {UserId} joined chat room {ChatRoomId}", Context.UserIdentifier, chatRoomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);
    }
    
    public async Task SendToModel(string chatRoomId, CustomClientMessage message)
    {
        logger.LogInformation("User {UserId} sent message to chat room {ChatRoomId}", Context.UserIdentifier, chatRoomId);
        // Here you can process the message and send it to the model
        // For now, we just log it
        await Clients.Group(chatRoomId).ReceiveMessage(chatRoomId, message);
    }
    
    public async Task LeaveChatRoom(string chatRoomId)
    {
        logger.LogInformation("User {UserId} left chat room {ChatRoomId}", Context.UserIdentifier, chatRoomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatRoomId);
    }
}