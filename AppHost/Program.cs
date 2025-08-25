using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("redis");


var orleans = builder.AddOrleans("orleans")
    .WithClustering(cache)
    .WithGrainStorage("default", cache);

// Add a custom resource for HttpLoggingHandler
var httpLoggingResource = builder.AddResource(new CustomHttpLoggingResource("http-logging-handler"))
    .WithAnnotation(new ManifestPublishingCallbackAnnotation(context =>
    {
        context.Writer.WriteString("type", "custom.http.logging");
        context.Writer.WriteString("description", "HTTP Logging Handler for capturing request and response payloads");
    }));

var aiKey = builder.AddParameter("AI-ApiKey");
var aiEndpoint = builder.AddParameter("AI-Endpoint");
var aiModelName = builder.AddParameter("AI-DeploymentName");
var aiEmbeddingModel = builder.AddParameter("Embedding-Model");
var apiService = builder.AddProject<API>("apiservice")
    .WithHttpEndpoint(5000, name: "api")
    .WithEnvironment("AI-ApiKey", aiKey)
    .WithEnvironment("AI-Endpoint", aiEndpoint)
    .WithEnvironment("AI-DeploymentName", aiModelName)
    .WithEnvironment("Embedding-Model", aiEmbeddingModel)
    .WithReference(cache)
    .WithReference(orleans);

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithReference(apiService)
    // add a port of 3000 for the frontend
    .WithHttpEndpoint(name: "frontend-endpoint", env: "PORT", port: 4000);

builder.Build().Run();

// Custom resource class for HttpLoggingHandler
public class CustomHttpLoggingResource : Resource
{
    public CustomHttpLoggingResource(string name) : base(name)
    {
    }
}

