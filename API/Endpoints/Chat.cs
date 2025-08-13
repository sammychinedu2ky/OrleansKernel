    using System.Security.Claims;
    using API.Hubs;
    using Microsoft.AspNetCore.Mvc;

    namespace API.Endpoints;

    public static class Chat
    {
        public static void MapChatEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/chat")
                .WithTags("Chat");

            group.MapGet("/{chatId}", async () =>
            {
                // create a list of fake messages CustomClientMessage
                var fakeMessages = new List<CustomClientMessage>
                {
                    new CustomClientMessage
                    {
                        Text = "Hello, how can I help you?",
                        Role = "assistant",
                        Files = []
                    },
                    new CustomClientMessage
                    {
                        Text = "I need help with my account.",
                        Role = "user",
                        Files = new List<FileMessage>
                        {
                            new FileMessage
                            {
                                FileId = Guid.NewGuid().ToString(),
                                FileName = "screenshot.png",
                                FileType = "image/png"
                            }
                        }
                    }
                };    
                return Results.Ok(fakeMessages);
            });
            group.MapPost("/", async (
                [FromForm] IFormFileCollection files,
                [FromServices] IGrainFactory grainFactory,
                ClaimsPrincipal claim) =>
            {
                
                var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                var blobFolder = Path.Combine(projectRoot, "BlobStorage");
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