using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol;
using NSubstitute;

namespace Banguat.ExchangeRates.McpServer.Tests.Tools;

public class CurrencyArgumentTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyResolver _currencyResolver;

    public CurrencyArgumentTests()
    {
        _currencyResolver = new CurrencyResolver(_client, new BundledCurrencyAliasCatalog());
    }

    [Fact]
    public async Task ResolveAsync_NumericString_ReturnsCurrencyCode()
    {
        CurrencyCode code = await CurrencyArgument.ResolveAsync("24", _currencyResolver, CancellationToken.None);

        Assert.Equal(24, code.Value);
    }

    [Fact]
    public async Task ResolveAsync_KnownAlias_ReturnsCurrencyCode()
    {
        CurrencyCode code = await CurrencyArgument.ResolveAsync("usd", _currencyResolver, CancellationToken.None);

        Assert.Equal(2, code.Value);
    }

    [Fact]
    public async Task ResolveAsync_UnknownValue_ThrowsMcpExceptionPointingAtGetCurrencies()
    {
        McpException exception = await Assert.ThrowsAsync<McpException>(() =>
            CurrencyArgument.ResolveAsync("not-a-currency", _currencyResolver, CancellationToken.None));

        Assert.Contains("not-a-currency", exception.Message);
        Assert.Contains("get_currencies", exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_IsoNumericCode_ThrowsMcpExceptionExplainingBanguatCodes()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(
            new GetAvailableCurrencies.Response(
                [new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(2), "Dólares de EE.UU.")])));

        McpException exception = await Assert.ThrowsAsync<McpException>(() =>
            CurrencyArgument.ResolveAsync("840", _currencyResolver, CancellationToken.None));

        Assert.Contains("ISO 4217", exception.Message);
    }

    [Fact]
    public void Description_WarnsThatCodesAreNotIso4217()
    {
        Assert.Contains("NOT the ISO 4217", CurrencyArgument.Description);
    }

    [Fact]
    public async Task ResolveAsync_CatalogTimesOut_ThrowsMcpExceptionWithoutCurrenciesHint()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(
            Result.Failure<GetAvailableCurrencies.Response>(BanguatErrors.Timeout()));

        McpException exception = await Assert.ThrowsAsync<McpException>(() =>
            CurrencyArgument.ResolveAsync("28", _currencyResolver, CancellationToken.None));

        Assert.Equal(BanguatErrors.Timeout().Description, exception.Message);
    }
}