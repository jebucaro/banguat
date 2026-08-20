using Banguat.ExchangeRates;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddBanguatExchangeRates();

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
    .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless);

// ServiceDefaults.ConfigureOpenTelemetry only listens on a source named after this app; the MCP SDK's
// own instrumentation lives under "Experimental.ModelContextProtocol" and needs to be added explicitly
// for its spans/metrics to reach the Aspire dashboard.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Experimental.ModelContextProtocol"))
    .WithMetrics(m => m.AddMeter("Experimental.ModelContextProtocol"));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.MapMcp().RequireCors("McpClient");

app.Run();
