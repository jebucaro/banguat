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
    private readonly ICurrencyResolver _currencyResolver;

    public GetRateHistoryToolTests()
    {
        _currencyResolver = new CurrencyResolver(_client, _aliasCatalog);
    }

    [Fact]
    public async Task GetRateHistoryAsync_Since_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetRateHistory.Response response = new(
            [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetRateHistoryAsync(eur, new DateOnly(2026, 8, 1), null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

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
        GetRateHistory.Response response = new(
            [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetRateHistoryAsync(eur, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", null, "2026-08-01", "2026-08-02", CancellationToken.None);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task GetRateHistoryAsync_FromAndTo_EmptyRange_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetRateHistoryAsync(eur, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetRateHistory.Response([])));
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

        GetRateHistoryTool.RateHistoryResult result = await tool.GetRateHistoryAsync(
            "24", null, "2026-08-01", "2026-08-02", CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.History);
    }

    // Which combinations are valid is the library's rule (see GetRateHistoryTests); the tool only has to
    // forward the parsed dates unchanged and surface the library's validation message.
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-08-01", "2026-08-01", "2026-08-02")]
    [InlineData(null, "2026-08-01", null)]
    public async Task GetRateHistoryAsync_InvalidHistoryRange_ThrowsMcpException(
        string? since, string? from, string? to)
    {
        _client.GetRateHistoryAsync(Arg.Any<CurrencyCode>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(),
                Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetRateHistory.Response>(BanguatErrors.InvalidHistoryRange()));
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

        McpException exception = await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", since, from, to, CancellationToken.None));

        Assert.Equal(BanguatErrors.InvalidHistoryRange().Description, exception.Message);
        await _client.Received(1).GetRateHistoryAsync(
            new CurrencyCode(2), ParseOrNull(since), ParseOrNull(from), ParseOrNull(to), Arg.Any<CancellationToken>());
    }

    private static DateOnly? ParseOrNull(string? value)
    {
        return value is null ? null : DateOnly.Parse(value);
    }

    [Fact]
    public async Task GetRateHistoryAsync_InvalidDate_ThrowsMcpException()
    {
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

        await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", "08/01/2026", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetRateHistoryAsync_TransportFailure_ThrowsMcpException()
    {
        CurrencyCode usd = new(2);
        _client.GetRateHistoryAsync(usd, new DateOnly(2026, 8, 1), null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetRateHistory.Response>(Error.Failure("transport", "boom")));
        GetRateHistoryTool tool = new(_client, _currencyResolver, _aliasCatalog);

        McpException exception = await Assert.ThrowsAsync<McpException>(() => tool.GetRateHistoryAsync(
            "usd", "2026-08-01", null, null, CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }
}