using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Hosting;

internal static class McpToolRegistration
{
    // The tool list lives here so HTTP and stdio can't drift apart.
    internal static IMcpServerBuilder WithBanguatTools(this IMcpServerBuilder builder)
    {
        return builder
            .WithTools<GetCurrenciesTool>()
            .WithTools<GetRateTool>()
            .WithTools<GetRateHistoryTool>();
    }
}