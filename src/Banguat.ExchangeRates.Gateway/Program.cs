using Yarp.ReverseProxy.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// https+http lets service discovery pick the https endpoint when the target exposes one, and only fall back
// to plain http when it doesn't.
string mcpDestination = builder.Configuration["Destinations:Mcp"] ?? "https+http://mcpserver";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            new RouteConfig
            {
                RouteId = "mcp",
                ClusterId = "mcp",
                Match = new RouteMatch { Path = "/mcp/{**catch-all}" },
                Transforms =
                [
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/mcp" }
                ]
            }
        ],
        [
            new ClusterConfig
            {
                ClusterId = "mcp",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["mcp"] = new() { Address = mcpDestination }
                }
            }
        ])
    .AddServiceDiscoveryDestinationResolver();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapReverseProxy();

await app.RunAsync();
