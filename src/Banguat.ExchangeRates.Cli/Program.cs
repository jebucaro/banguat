using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Aliases;
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
    public static async Task<int> Main(string[] args)
    {
        ServiceCollection services = new();

        services.AddBanguatExchangeRates();
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<ICurrencyOverrideSource, FileCurrencyOverrideSource>();

        services.Scan(scan => scan.FromAssembliesOf(typeof(Program))
            .AddClasses(classes => classes.AssignableTo<ICommand>())
            .AsSelf()
            .WithTransientLifetime());

        // Opt-in only: tracing/metrics from Banguat.ExchangeRates are exported over OTLP only when
        // OTEL_EXPORTER_OTLP_ENDPOINT is set (e.g. pointed at the Aspire dashboard), mirroring
        // ServiceDefaults' AddOpenTelemetryExporters gating. The CLI never requires Aspire to run.
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
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

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Force the OTel SDK to start now (it's normally started by a generic-host hosted service,
        // which this CLI doesn't use); no-op when the block above didn't run. Disposing
        // serviceProvider below flushes the OTLP batch exporter before the process exits.
        serviceProvider.GetService<TracerProvider>();
        serviceProvider.GetService<MeterProvider>();

        CommandLineApplication app = new CommandLineApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(type => serviceProvider.GetRequiredService(type))
            .Build();

        // Commands never throw for expected failures (see BanguatCommandBase.Fail) — they write
        // their own structured error to stdout and set Environment.ExitCode themselves, so RunAsync
        // itself returns 0 for that case. Prefer Environment.ExitCode when a command set it; fall
        // back to RunAsync's own return value, which still correctly reports a genuinely unhandled
        // exception (verified: RunAsync returns 1 for those without touching Environment.ExitCode).
        int exitCode = await app.RunAsync(args);
        return Environment.ExitCode != 0 ? Environment.ExitCode : exitCode;
    }
}
