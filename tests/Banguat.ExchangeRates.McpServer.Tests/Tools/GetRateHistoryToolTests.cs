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
        var response = new GetCurrencyRateHistorySince.Response(
            [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", since: "2026-08-01", from: null, to: null, cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.Count);
        Assert.Equal(24, result.Currency);
        Assert.Equal(new DateOnly(2026, 8, 1), result.History[0].Date);
        Assert.Equal("EUR", result.CurrencyAlias);
    }

    [Fact]
    public async Task GetRateHistoryAsync_FromAndTo_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        var response = new GetCurrencyRateHistory.Response(
            [new GetCurrencyRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", since: null, from: "2026-08-01", to: "2026-08-02", cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task GetRateHistoryAsync_FromAndTo_EmptyRange_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrencyRateHistory.Response([])));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", since: null, from: "2026-08-01", to: "2026-08-02", cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.History);
    }

    [Fact]
    public async Task GetRateHistoryAsync_NeitherSinceNorRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since: null, from: null, to: null, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_BothSinceAndRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since: "2026-08-01", from: "2026-08-01", to: "2026-08-02", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_PartialRange_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since: null, from: "2026-08-01", to: null, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_InvalidDate_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since: "08/01/2026", from: null, to: null, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_TransportFailure_ThrowsMcpException()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), usd, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrencyRateHistorySince.Response>(Error.Failure("transport", "boom")));
        GetRateHistoryTool tool = new(_client, _aliasCatalog);

        McpException exception = await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since: "2026-08-01", from: null, to: null, cancellationToken: CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }
}
