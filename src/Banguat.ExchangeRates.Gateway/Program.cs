using System.Threading.RateLimiting;
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
                RateLimiterPolicy = RateLimitPolicies.Api,
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
                RateLimiterPolicy = RateLimitPolicies.Mcp,
                Transforms =
                [
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/mcp" }
                ]
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
    SlidingWindowRateLimiterOptions apiLimiterOptions = new()
    {
        PermitLimit = RateLimitDefaults.ApiPermitLimit,
        Window = RateLimitDefaults.Window,
        SegmentsPerWindow = RateLimitDefaults.SegmentsPerWindow
    };
    builder.Configuration.GetSection("RateLimiting:Api").Bind(apiLimiterOptions);

    SlidingWindowRateLimiterOptions mcpLimiterOptions = new()
    {
        PermitLimit = RateLimitDefaults.McpPermitLimit,
        Window = RateLimitDefaults.Window,
        SegmentsPerWindow = RateLimitDefaults.SegmentsPerWindow
    };
    builder.Configuration.GetSection("RateLimiting:Mcp").Bind(mcpLimiterOptions);

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Api, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => apiLimiterOptions));

    options.AddPolicy(RateLimitPolicies.Mcp, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => mcpLimiterOptions));

    options.OnRejected = (context, _) =>
    {
        string retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "60";
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds;
        return ValueTask.CompletedTask;
    };
});

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();

public partial class Program;
