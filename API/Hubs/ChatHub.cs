using API.Util;
using Microsoft.AspNetCore.SignalR;

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
    ILogger<ChatHub> logger) : Hub<IChatHub>
{
    public async Task SendMessageToModel(string chatId, CustomClientMessage message)
    {
        var claimPrincipal = Context.User!;
        var isAuthenticated = claimPrincipal.Identity?.IsAuthenticated ?? false;
        var userId = RetrieveUserId.GetUserId(claimPrincipal);
        // need to store the userId and chatRoomId in a grain
        logger.LogInformation("User {UserId} sent message to chat room {ChatId}", Context.UserIdentifier, chatId);
        // Here you can process the message and send it to the model
        // For now, we just log it
        var fakeMessage = new CustomClientMessage
        {
            Role = "assistant", // assuming the model responds as an assistant
            Text = "This is a test message",
            Files = message.Files
        };
        // ought to perform some grain calls to process the message
        logger.LogInformation("Received message: {Message}", fakeMessage.Text);
        // send to the client that send this message
        await Clients.Caller.ReceiveMessage(chatId, fakeMessage);
    }
    
    
}