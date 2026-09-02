IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ValkeyResource> cache = builder.AddValkey("cache");

builder.AddProject<Projects.Banguat_ExchangeRates_McpServer>("mcpserver")
    .WithReference(cache);

builder.AddProject<Projects.Banguat_ExchangeRates_Api>("api")
    .WithReference(cache);

builder.Build().Run();