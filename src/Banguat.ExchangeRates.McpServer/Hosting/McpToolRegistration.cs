using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Hosting;

internal static class McpToolRegistration
{
    // The tool list lives here so HTTP and stdio can't drift apart.
    internal static IMcpServerBuilder WithBanguatTools(this IMcpServerBuilder builder)
    {
        IMcpServerBuilder withTools = builder
            .WithTools<GetCurrenciesTool>()
            .WithTools<GetRateTool>()
            .WithTools<GetRateHistoryTool>();

#pragma warning disable MCPEXP005 // The Skills extension is experimental.
        return withTools.WithSkillsFromSubdirectories(Path.Combine(AppContext.BaseDirectory, "Skills"));
#pragma warning restore MCPEXP005
    }
}