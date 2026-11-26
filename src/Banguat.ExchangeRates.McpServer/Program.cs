using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.McpServer.Hosting;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

if (StdioHost.IsRequested(args))
{
    await StdioHost.RunAsync(args);
    return;
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");
builder.AddBanguatExchangeRates();

string[] allowedOrigins = builder.Configuration.GetSection("Mcp:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("McpClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "DELETE")
            .WithHeaders("Content-Type", "MCP-Protocol-Version", "Mcp-Session-Id", "Mcp-Method", "Mcp-Name")
            .WithExposedHeaders("Mcp-Session-Id");
    });
});

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
    .WithBanguatTools();

// ServiceDefaults only listens to a source named after the app. The MCP SDK and the library publish under their
// own names, so add both for their telemetry to reach the dashboard.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("Experimental.ModelContextProtocol")
        .AddSource(BanguatExchangeRatesDiagnostics.ActivitySourceName))
    .WithMetrics(m => m
        .AddMeter("Experimental.ModelContextProtocol")
        .AddMeter(BanguatExchangeRatesDiagnostics.MeterName));

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.MapMcp().RequireCors("McpClient");

await app.RunAsync();