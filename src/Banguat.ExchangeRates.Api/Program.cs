using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");
builder.AddBanguatExchangeRates();
builder.Services.AddApiEndpoints();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

string[] allowedOrigins = builder.Configuration.GetSection("Api:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET")
            .WithHeaders("Content-Type", "Accept");
    });
});

// ServiceDefaults only listens to a source named after the app, so the library's spans and metrics need adding
// here to reach the dashboard.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(BanguatExchangeRatesDiagnostics.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(BanguatExchangeRatesDiagnostics.MeterName));

WebApplication app = builder.Build();

// Last resort for truly unexpected exceptions: a ProblemDetails 500 instead of an empty body. Upstream failures
// never get here, they come back as Results mapped to 502/503/504.
app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.UseCors("ApiClient");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

RouteGroupBuilder v1 = app.MapGroup("/v1");
foreach (IEndpoint endpoint in app.Services.GetServices<IEndpoint>())
{
    endpoint.MapEndpoint(v1);
}

await app.RunAsync();