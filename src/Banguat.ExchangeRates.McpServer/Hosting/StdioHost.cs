using Microsoft.Extensions.Logging;

namespace Banguat.ExchangeRates.McpServer.Hosting;

internal static class StdioHost
{
    private const string Flag = "--stdio";

    internal static bool IsRequested(string[] args)
    {
        return args.Contains(Flag);
    }

    internal static async Task RunAsync(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args.Where(arg => arg != Flag).ToArray());

        // stdout carries the MCP protocol, so every log line has to go to stderr.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.AddServiceDefaults();
        builder.AddBanguatExchangeRates();

        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithBanguatTools();

        await builder.Build().RunAsync();
    }
}
