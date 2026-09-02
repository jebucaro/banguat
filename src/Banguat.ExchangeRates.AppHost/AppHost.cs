IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ValkeyResource> cache = builder.AddValkey("cache");

var mcpserver = builder.AddProject<Projects.Banguat_ExchangeRates_McpServer>("mcpserver")
    .WithReference(cache);

var api = builder.AddProject<Projects.Banguat_ExchangeRates_Api>("api")
    .WithReference(cache);

builder.AddProject<Projects.Banguat_ExchangeRates_Gateway>("gateway")
    .WithReference(api)
    .WithReference(mcpserver)
    .WithExternalHttpEndpoints();

builder.Build().Run();