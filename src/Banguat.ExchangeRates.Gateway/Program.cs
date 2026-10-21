using OpenTelemetry.Instrumentation.AspNetCore;
using Yarp.ReverseProxy.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The two routes match with a catch-all, so AspNetCore tracing names every span after the route template
// (/api/{**catch-all}). Rename the span to the real request path so the dashboard shows which endpoint was
// hit without expanding each trace. MCP tool calls all POST to /mcp, so their per-tool detail stays on the
// mcpserver span, not here. The rename has to happen on the response: the instrumentation applies the route
// template name when the request stops, so a name set at request start is overwritten. Configuring the options
// (rather than calling AddAspNetCoreInstrumentation again) keeps the single listener ServiceDefaults registers.
builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
    options.EnrichWithHttpResponse = (activity, response) =>
        activity.DisplayName = $"{response.HttpContext.Request.Method} {response.HttpContext.Request.Path}");

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
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/api" },
                    // YARP's own X-Forwarded-Prefix only carries the gateway's path base, which is empty, so
                    // send the prefix we strip instead. The API needs it to build links (like the OpenAPI server
                    // URL) that point back through the gateway.
                    new Dictionary<string, string> { ["X-Forwarded"] = "Set", ["Prefix"] = "Off" },
                    new Dictionary<string, string> { ["RequestHeader"] = "X-Forwarded-Prefix", ["Set"] = "/api" }
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