using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol;

namespace Banguat.ExchangeRates.McpServer.Tests.Tools;

public class CurrencyArgumentTests
{
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public void Resolve_NumericString_ReturnsCurrencyCode()
    {
        CurrencyCode code = CurrencyArgument.Resolve("24", _aliasCatalog);

        Assert.Equal(24, code.Value);
    }

    [Fact]
    public void Resolve_KnownAlias_ReturnsCurrencyCode()
    {
        CurrencyCode code = CurrencyArgument.Resolve("usd", _aliasCatalog);

        Assert.Equal(2, code.Value);
    }

    [Fact]
    public void Resolve_UnknownValue_ThrowsMcpException()
    {
        McpException exception = Assert.Throws<McpException>(
            () => CurrencyArgument.Resolve("not-a-currency", _aliasCatalog));

        Assert.Contains("not-a-currency", exception.Message);
    }
}
