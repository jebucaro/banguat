IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Docker Compose deployment target. When this environment is present, `aspire publish`
// generates docker-compose.yaml and .env from the app model below. All resources are
// published automatically; no per-resource opt-in is required.
builder.AddDockerComposeEnvironment("compose");

IResourceBuilder<ValkeyResource> cache = builder.AddValkey("cache");

IResourceBuilder<ProjectResource> mcpserver = builder.AddProject<Projects.Banguat_ExchangeRates_McpServer>("mcpserver")
    .WithReference(cache);

IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.Banguat_ExchangeRates_Api>("api")
    .WithReference(cache);

builder.AddProject<Projects.Banguat_ExchangeRates_Gateway>("gateway")
    .WithReference(api)
    .WithReference(mcpserver)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();