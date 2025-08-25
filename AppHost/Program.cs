using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("redis");


var orleans = builder.AddOrleans("orleans")
    .WithClustering(cache)
    .WithGrainStorage("default", cache);


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
    .WithHttpEndpoint(name: "frontend-endpoint", env: "PORT", port: 3000);

builder.Build().Run();