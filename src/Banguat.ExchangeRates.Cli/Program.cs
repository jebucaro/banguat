using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Aliases;
using CliFx;
using Microsoft.Extensions.DependencyInjection;
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

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

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
