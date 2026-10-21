using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Diagnostics;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scrutor;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli;

public static class Program
{
    // Fail at startup, not mid-command, if a registration has a lifetime mistake such as a scoped service
    // resolved from the root provider.
    internal static readonly ServiceProviderOptions ProviderOptions = new()
    {
        ValidateScopes = true, ValidateOnBuild = true
    };

    public static async Task<int> Main(string[] args)
    {
        await using ServiceProvider serviceProvider = CreateServices().BuildServiceProvider(ProviderOptions);

        // Force the OTel SDK to start now (it's normally started by a generic-host hosted service,
        // which this CLI doesn't use); no-op when OTLP export isn't configured. Disposing
        // serviceProvider below flushes the OTLP batch exporter before the process exits.
        serviceProvider.GetService<TracerProvider>();
        serviceProvider.GetService<MeterProvider>();

        // One CLI invocation is one unit of work, like one HTTP request in the web hosts: the command and the
        // scoped client and resolver it depends on all live in this scope.
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CommandLineApplication app = new CommandLineApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(type => scope.ServiceProvider.GetRequiredService(type))
            .Build();

        // Commands never throw for expected failures (see BanguatCommandBase.Fail) — they write
        // their own structured error to stdout and set Environment.ExitCode themselves, so RunAsync
        // itself returns 0 for that case. Prefer Environment.ExitCode when a command set it; fall
        // back to RunAsync's own return value, which still correctly reports a genuinely unhandled
        // exception (verified: RunAsync returns 1 for those without touching Environment.ExitCode).
        int exitCode = await app.RunAsync(args);
        return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
    }

    internal static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();

        services.AddBanguatExchangeRates();
        services.AddSingleton(AnsiConsole.Console);

        services.Scan(scan => scan.FromAssembliesOf(typeof(Program))
            .AddClasses(classes => classes.AssignableTo<ICommand>())
            .AsSelf()
            .WithTransientLifetime());

        // Opt-in only: tracing/metrics from Banguat.ExchangeRates are exported over OTLP only when
        // OTEL_EXPORTER_OTLP_ENDPOINT is set (e.g. pointed at the Aspire dashboard), mirroring
        // ServiceDefaults' AddOpenTelemetryExporters gating. The CLI never requires Aspire to run.
        string? otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService("banguat-exchangerates-cli"))
                .WithTracing(tracing => tracing
                    .AddSource(BanguatExchangeRatesDiagnostics.ActivitySourceName)
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    .AddMeter(BanguatExchangeRatesDiagnostics.MeterName)
                    .AddOtlpExporter());
        }

        return services;
    }
}