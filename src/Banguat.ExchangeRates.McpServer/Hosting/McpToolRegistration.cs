using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Hosting;

internal static class McpToolRegistration
{
    // One place for the tool list, shared by the HTTP and stdio hosts.
    internal static IMcpServerBuilder WithBanguatTools(this IMcpServerBuilder builder)
    {
        return builder
            .WithTools<GetCurrenciesTool>()
            .WithTools<GetRateTool>()
            .WithTools<GetRateHistoryTool>();
    }
}
