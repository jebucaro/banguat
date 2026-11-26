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
    // Catches lifetime mistakes (like a scoped service resolved from the root) at startup instead of mid-command.
    internal static readonly ServiceProviderOptions ProviderOptions = new()
    {
        ValidateScopes = true, ValidateOnBuild = true
    };

    public static async Task<int> Main(string[] args)
    {
        await using ServiceProvider serviceProvider = CreateServices().BuildServiceProvider(ProviderOptions);

        // The OTel SDK normally starts from a generic-host hosted service, which this CLI doesn't use, so poke it
        // here. Disposing the provider below flushes the exporter.
        serviceProvider.GetService<TracerProvider>();
        serviceProvider.GetService<MeterProvider>();

        // One invocation is one unit of work, like a web request: the command and its scoped client and resolver
        // live in this scope.
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        CommandLineApplication app = new CommandLineApplicationBuilder()
            .AddCommandsFromThisAssembly()
            .UseTypeInstantiator(type => scope.ServiceProvider.GetRequiredService(type))
            .Build();

        // Commands handle expected failures themselves (see BanguatCommandBase.Fail): they print the error and set
        // Environment.ExitCode, so RunAsync returns 0 for those. Prefer ExitCode when it's set; RunAsync's own
        // value still covers unhandled exceptions (it returns 1).
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

        // Opt-in: telemetry only goes out over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set (the Aspire dashboard,
        // say). The CLI never needs Aspire to run.
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