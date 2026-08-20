using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol;
using NSubstitute;

namespace Banguat.ExchangeRates.McpServer.Tests.Tools;

public class GetRateToolTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public async Task GetRateAsync_DefaultsToUsd_ReturnsRate()
    {
        CurrencyCode usd = new(2);
        var response = new GetCurrentRate.Response(
            [new GetCurrentRate.RatePoint(new DateOnly(2026, 8, 18), 7.62157m, 7.62157m)]);
        _client.GetCurrentRateAsync(usd, Arg.Any<CancellationToken>()).Returns(Result.Success(response));
        GetRateTool tool = new(_client, _aliasCatalog);

        GetRateTool.RateResult result = await tool.GetRateAsync("usd", CancellationToken.None);

        Assert.Equal(2, result.Currency);
        Assert.Equal(1, result.Count);
        Assert.Equal(7.62157m, result.Buy);
        Assert.Equal(new DateOnly(2026, 8, 18), result.Date);
        Assert.Equal("USD", result.CurrencyAlias);
    }

    [Fact]
    public async Task GetRateAsync_NoDataForToday_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrentRateAsync(eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrentRate.Response([])));
        GetRateTool tool = new(_client, _aliasCatalog);

        GetRateTool.RateResult result = await tool.GetRateAsync("24", CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Null(result.Date);
        Assert.Null(result.Buy);
    }

    [Fact]
    public async Task GetRateAsync_UnknownCurrency_ThrowsMcpException()
    {
        GetRateTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateAsync("not-a-currency", CancellationToken.None));
    }

    [Fact]
    public async Task GetRateAsync_TransportFailure_ThrowsMcpException()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrentRate.Response>(Error.Failure("transport", "boom")));
        GetRateTool tool = new(_client, _aliasCatalog);

        McpException exception = await Assert.ThrowsAsync<McpException>(
            () => tool.GetRateAsync("usd", CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }
}
