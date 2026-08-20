using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.McpServer.Tools;
using ModelContextProtocol;
using NSubstitute;

namespace Banguat.ExchangeRates.McpServer.Tests.Tools;

public class GetRateHistoryToolTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public async Task GetRateHistoryAsync_Since_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetCurrencyRateHistorySince.Response response = new(
            [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", "2026-08-01", null, null, CancellationToken.None);

        Assert.Equal(1, result.Count);
        Assert.Equal(24, result.Currency);
        Assert.Equal(new DateOnly(2026, 8, 1), result.History[0].Date);
        Assert.Equal("EUR", result.CurrencyAlias);
    }

    [Fact]
    public async Task GetRateHistoryAsync_FromAndTo_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetCurrencyRateHistory.Response response = new(
            [new GetCurrencyRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", null, "2026-08-01", "2026-08-02", CancellationToken.None);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task GetRateHistoryAsync_FromAndTo_EmptyRange_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrencyRateHistory.Response([])));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", null, "2026-08-01", "2026-08-02", CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.History);
    }

    [Fact]
    public async Task GetRateHistoryAsync_NeitherSinceNorRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_BothSinceAndRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", "2026-08-01", "2026-08-01", "2026-08-02", CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_PartialRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", null, "2026-08-01", null, CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_InvalidDate_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", "08/01/2026", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_TransportFailure_ThrowsMcpException()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), usd, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrencyRateHistorySince.Response>(Error.Failure("transport", "boom")));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        McpException exception = await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", "2026-08-01", null, null, CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }
}