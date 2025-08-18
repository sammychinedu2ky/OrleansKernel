using System.Security.Claims;
using API.Grains;
using API.Hubs;
using API.Util;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class Chat
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat")
            .WithTags("Chat");

        group.MapGet("/{chatId}", async ([FromRoute] string chatId, IGrainFactory grainFactory, ClaimsPrincipal user) =>
        {
            var userId = RetrieveUserId.GetUserId(user);
            // if user is authenticated
            if (user.Identity.IsAuthenticated)
            {
                var chatSavingGrain = grainFactory.GetGrain<IChatSavingGrain>(userId, chatId);
                var messages = await chatSavingGrain.GetChatMessages(userId,chatId);
                return Results.Ok(messages);
            }
            return Results.Unauthorized();
        });

        group.MapGet("/pages", async (IGrainFactory grainFactory, ClaimsPrincipal user) =>
        {
            var userId = RetrieveUserId.GetUserId(user);
            // if user is authenticated
            if (user.Identity.IsAuthenticated)
            {
            var userToChatIdMappingGrain = grainFactory.GetGrain<IUserToChatIdMappingGrain>(userId);
            var messages = await userToChatIdMappingGrain.GetChatPagesAsync();
            return Results.Ok(messages);
            }
            return Results.Unauthorized();
        });

        group.MapPost("/", async (
                [FromForm] IFormFileCollection files,
                [FromServices] IGrainFactory grainFactory,
                ClaimsPrincipal claim) =>
            {
                string blobFolder = RetrieveBlobFolder.Get();
                // creates folder if it doens't exist
                Directory.CreateDirectory(blobFolder);

                // Simulate saving the file and generating a file ID
                var fileMessages = new List<FileMessage>();
                foreach (var formFile in files)
                {
                    if (formFile.Length > 0)
                    {
                        var fileId = Guid.NewGuid().ToString();
                        var fileExtension = Path.GetExtension(formFile.FileName);
                        var filePath = Path.Combine(blobFolder, fileId);
                        using var stream = formFile.OpenReadStream();
                        using var fileStream = new FileStream(filePath, FileMode.Create);
                        await stream.CopyToAsync(fileStream);

                        var fileMessage = new FileMessage
                        {
                            FileId = fileId,
                            FileName = formFile.FileName,
                            FileType = formFile.ContentType
                        };

                        fileMessages.Add(fileMessage);
                    }

                }

                return Results.Ok(fileMessages);

                //    // Console.WriteLine(context.User.Identity.Name);
                //     Console.WriteLine(AppContext.BaseDirectory);
                //     var userId = claim.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous User";
                //     return new { UserId = userId };
            }).DisableAntiforgery();
    }


}