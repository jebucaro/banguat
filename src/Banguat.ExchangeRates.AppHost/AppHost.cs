IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Banguat_ExchangeRates_McpServer>("mcpserver");

builder.Build().Run();