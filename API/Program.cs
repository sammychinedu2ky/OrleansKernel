using System.Security.Claims;
using System.Text.Json;
using API.Endpoints;
using API.Grains;
using API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["AI-ApiKey"] ?? throw new ArgumentNullException("AI-ApiKey is not set in configuration.");
var aiEndpoint = builder.Configuration["AI-Endpoint"] ?? throw new ArgumentNullException("AI-Endpoint is not set in configuration.");
var aiModelName = builder.Configuration["AI-DeploymentName"] ?? throw new ArgumentNullException("AI-DeploymentName is not set in configuration.");

builder.Services.AddAzureOpenAIChatCompletion(aiModelName, aiEndpoint, apiKey);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddKeyedRedisClient("redis");
builder.UseOrleans();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            ValidIssuer = "https://genuine-kite-18.clerk.accounts.dev"
            // Use JWKS endpoint for public key validation
            // IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            // {
            //     var jwksUrl = "https://genuine-kite-18.clerk.accounts.dev/.well-known/jwks.json";
            //     using var httpClient = new HttpClient();
            //     var jwks = httpClient.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
            //     var keys = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(jwks).Keys;
            //     return keys;
            // },
        };
        // makes IssuerSigningKeyResolver not required
         options.Authority = "https://genuine-kite-18.clerk.accounts.dev";
         options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // First, check if there's an access_token in query string
                var accessToken = context.Request.Query["access_token"];

                // If the request is for our SignalR hub...
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/api/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSignalR();
builder.Services.AddAuthorization();
// add cors support for local host 3000
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseCors("AllowFrontend");


app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/", async (
    [FromForm] IFormFileCollection files, [FromServices] IGrainFactory grainFactory, ClaimsPrincipal claim) =>
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
    }).DisableAntiforgery().RequireCors("AllowFrontend")
    .WithName("GetHelloWorld");

app.MapChatEndpoints();
app.MapHub<ChatHub>("/api/hubs/chat").RequireCors("AllowFrontend")
    .RequireAuthorization();

app.Run();
