using Microsoft.AspNetCore.RateLimiting;
using Banguat.ExchangeRates.Gateway;
using Yarp.ReverseProxy.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

string apiDestination = builder.Configuration["Destinations:Api"] ?? "http://api";
string mcpDestination = builder.Configuration["Destinations:Mcp"] ?? "http://mcpserver";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes:
        [
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = "api",
                Match = new RouteMatch { Path = "/api/{**catch-all}" },
                RateLimiterPolicy = RateLimitPolicies.Api
            },
            new RouteConfig
            {
                RouteId = "mcp",
                ClusterId = "mcp",
                Match = new RouteMatch { Path = "/mcp/{**catch-all}" },
                RateLimiterPolicy = RateLimitPolicies.Mcp
            }
        ],
        clusters:
        [
            new ClusterConfig
            {
                ClusterId = "api",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["api"] = new DestinationConfig { Address = apiDestination }
                }
            },
            new ClusterConfig
            {
                ClusterId = "mcp",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["mcp"] = new DestinationConfig { Address = mcpDestination }
                }
            }
        ])
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddSlidingWindowLimiter(RateLimitPolicies.Api, o =>
    {
        o.PermitLimit = RateLimitDefaults.ApiPermitLimit;
        o.Window = RateLimitDefaults.Window;
        o.SegmentsPerWindow = RateLimitDefaults.SegmentsPerWindow;
        builder.Configuration.GetSection("RateLimiting:Api").Bind(o);
    });
    options.AddSlidingWindowLimiter(RateLimitPolicies.Mcp, o =>
    {
        o.PermitLimit = RateLimitDefaults.McpPermitLimit;
        o.Window = RateLimitDefaults.Window;
        o.SegmentsPerWindow = RateLimitDefaults.SegmentsPerWindow;
        builder.Configuration.GetSection("RateLimiting:Mcp").Bind(o);
    });
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();

public partial class Program;
