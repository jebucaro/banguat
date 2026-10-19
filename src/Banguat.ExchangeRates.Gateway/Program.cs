using OpenTelemetry;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// The two routes match with a catch-all, so AspNetCore tracing names every span after the route template
// (/api/{**catch-all}). Rename the span to the real request path so the dashboard shows which endpoint was
// hit without expanding each trace. MCP tool calls all POST to /mcp, so their per-tool detail stays on the
// mcpserver span, not here.
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
    tracing.AddAspNetCoreInstrumentation(options =>
        options.EnrichWithHttpRequest = (activity, request) =>
            activity.DisplayName = $"{request.Method} {request.Path}"));

string apiDestination = builder.Configuration["Destinations:Api"] ?? "http://api";
string mcpDestination = builder.Configuration["Destinations:Mcp"] ?? "http://mcpserver";

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

app.Run();

public partial class Program;
