namespace API.Endpoints;

public static class Chat
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat")
            .WithTags("Chat");

    }
}