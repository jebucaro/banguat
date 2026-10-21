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
builder.Services.AddGatewayForwardedHeaders();

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

// ServiceDefaults.ConfigureOpenTelemetry only adds an ActivitySource/Meter named after the app itself,
// the exchange-rate library's domain-level spans/metrics live under BanguatExchangeRatesDiagnostics's
// names and need to be added explicitly here for them to reach the Aspire dashboard.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(BanguatExchangeRatesDiagnostics.ActivitySourceName))
    .WithMetrics(m => m.AddMeter(BanguatExchangeRatesDiagnostics.MeterName));

WebApplication app = builder.Build();

// First, so everything after it (OpenAPI, CORS, logging) sees the client's view of the request.
app.UseForwardedHeaders();

// Backstop for genuinely unexpected exceptions: a generic ProblemDetails 500 instead of an empty response.
// Upstream failures never reach it; they come back as Results and map to 502/503/504.
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