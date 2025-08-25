using API.Endpoints;
using API.Hubs;
using API.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Orleans.Configuration;
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["AI-ApiKey"] ??
             throw new ArgumentNullException("AI-ApiKey is not set in configuration.");
var aiEndpoint = builder.Configuration["AI-Endpoint"] ??
                 throw new ArgumentNullException("AI-Endpoint is not set in configuration.");
var aiModelName = builder.Configuration["AI-DeploymentName"] ??
                  throw new ArgumentNullException("AI-DeploymentName is not set in configuration.");
var aiEmbeddingModel = builder.Configuration["Embedding-Model"] ??
                       throw new ArgumentNullException("Embedding-Model is not set in configuration.");
builder.AddServiceDefaults();

builder.Services.AddTransient<HttpMessageHandlerBuilder>(sp => 
    new CustomHttpMessageHandlerBuilder(
        sp.GetRequiredService<ILogger<HttpLoggingHandler>>(),
        sp
    ));
builder.Services.AddAzureOpenAIChatCompletion(aiModelName, aiEndpoint, apiKey);

 builder.Services.AddAzureOpenAIEmbeddingGenerator(aiEmbeddingModel, aiEndpoint, apiKey);

builder.Services.AddTransient<Kernel>();

builder.AddKeyedRedisClient("redis");


builder.UseOrleans(siloBuilder =>
{
    siloBuilder.Configure<GrainCollectionOptions>(options =>
    {
        options.CollectionAge = TimeSpan.FromMinutes(30); // unrelated, but example
    });

    siloBuilder.Configure<MessagingOptions>(options =>
    {
        options.ResponseTimeout = TimeSpan.FromMinutes(2); // extend timeout
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
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
                var accessToken = context.Request.Query["access_token"];
                // If the request is for our SignalR hub...
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/api/hubs/chat"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });
var connectionString = builder.Configuration.GetConnectionString("redis") ??
                       throw new ArgumentNullException("Redis connection string is not set in configuration.");
builder.Services.AddSignalR()
    .AddStackExchangeRedis(connectionString, options => { options.Configuration.ChannelPrefix = "MyApp"; });
builder.Services.AddAuthorization();
// add cors support for local host 4000
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder.WithOrigins("http://localhost:4000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseCors("AllowFrontend");


app.UseAuthentication();
app.UseAuthorization();



app.MapChatEndpoints();
app.MapDownloadEndpoints();
app.MapHub<ChatHub>("/api/hubs/chat");

app.Run();