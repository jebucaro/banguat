using Yarp.ReverseProxy.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// https+http lets service discovery pick the https endpoint when the target exposes one, and only fall back
// to plain http when it doesn't.
string apiDestination = builder.Configuration["Destinations:Api"] ?? "https+http://api";
string mcpDestination = builder.Configuration["Destinations:Mcp"] ?? "https+http://mcpserver";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = "api",
                Match = new RouteMatch { Path = "/api/{**catch-all}" },
                Transforms =
                [
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/api" }
                ]
            },
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
                ClusterId = "api",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["api"] = new() { Address = apiDestination }
                }
            },
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