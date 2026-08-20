using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol;
using NSubstitute;

namespace Banguat.ExchangeRates.McpServer.Tests.Tools;

public class GetCurrenciesToolTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public async Task GetCurrenciesAsync_ReturnsCurrenciesWithAliasesAndCount()
    {
        GetAvailableCurrencies.Response response = new(
        [
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(2), "Dólares de EE.UU."),
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(1), "Quetzales")
        ]);
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(response));
        GetCurrenciesTool tool = new(_client, _aliasCatalog);

        GetCurrenciesTool.CurrenciesResult result = await tool.GetCurrenciesAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        GetCurrenciesTool.CurrencyEntry usd = Assert.Single(result.Currencies, c => c.Code == 2);
        Assert.Contains("USD", usd.Aliases, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrenciesAsync_TransportFailure_ThrowsMcpException()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetAvailableCurrencies.Response>(Error.Failure("transport", "boom")));
        GetCurrenciesTool tool = new(_client, _aliasCatalog);

        McpException exception =
            await Assert.ThrowsAsync<McpException>(() => tool.GetCurrenciesAsync(CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }
}