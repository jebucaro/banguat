using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates.Tests;

public class BanguatExchangeRateClientTests
{
    [Fact]
    public async Task GetCurrentUsdRateAsync_Should_DelegateToHandler()
    {
        var expected = Result.Success(new GetCurrentUsdRate.Response(new DateOnly(2026, 8, 17), 7.61992m));
        var handler = new RecordingQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(handler);

        var result = await client.GetCurrentUsdRateAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetCurrentUsdRateTextAsync_Should_DelegateToHandler()
    {
        var expected = Result.Success(new GetCurrentUsdRateText.Response("text"));
        var handler = new RecordingQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrentUsdRateText: handler);

        var result = await client.GetCurrentUsdRateTextAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetAvailableCurrenciesAsync_Should_DelegateToHandler()
    {
        var expected = Result.Success(new GetAvailableCurrencies.Response([]));
        var handler =
            new RecordingQueryHandler<GetAvailableCurrencies.Query, GetAvailableCurrencies.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getAvailableCurrencies: handler);

        var result = await client.GetAvailableCurrenciesAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetCurrentRateAsync_Should_DelegateToHandler_WithCurrency()
    {
        var expected = Result.Success(new GetCurrentRate.Response([]));
        var handler = new RecordingQueryHandler<GetCurrentRate.Query, GetCurrentRate.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrentRate: handler);
        var currency = new CurrencyCode(18);

        var result = await client.GetCurrentRateAsync(currency);

        Assert.Same(expected, result);
        Assert.Equal(currency, handler.LastQuery!.Currency);
    }

    [Fact]
    public async Task GetUsdRateHistorySinceAsync_Should_DelegateToHandler_WithDate()
    {
        var expected = Result.Success(new GetUsdRateHistorySince.Response([]));
        var handler =
            new RecordingQueryHandler<GetUsdRateHistorySince.Query, GetUsdRateHistorySince.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getUsdRateHistorySince: handler);
        var since = new DateOnly(2026, 8, 1);

        var result = await client.GetUsdRateHistorySinceAsync(since);

        Assert.Same(expected, result);
        Assert.Equal(since, handler.LastQuery!.Since);
    }

    [Fact]
    public async Task GetUsdRateHistoryAsync_Should_DelegateToHandler_WithDateRange()
    {
        var expected = Result.Success(new GetUsdRateHistory.Response([]));
        var handler = new RecordingQueryHandler<GetUsdRateHistory.Query, GetUsdRateHistory.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getUsdRateHistory: handler);
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);

        var result = await client.GetUsdRateHistoryAsync(from, to);

        Assert.Same(expected, result);
        Assert.Equal(from, handler.LastQuery!.From);
        Assert.Equal(to, handler.LastQuery!.To);
    }

    [Fact]
    public async Task GetCurrencyRateHistorySinceAsync_Should_DelegateToHandler_WithDateAndCurrency()
    {
        var expected = Result.Success(new GetCurrencyRateHistorySince.Response([]));
        var handler =
            new RecordingQueryHandler<GetCurrencyRateHistorySince.Query, GetCurrencyRateHistorySince.Response>(
                expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrencyRateHistorySince: handler);
        var since = new DateOnly(2026, 8, 1);
        var currency = new CurrencyCode(18);

        var result = await client.GetCurrencyRateHistorySinceAsync(since, currency);

        Assert.Same(expected, result);
        Assert.Equal(since, handler.LastQuery!.Since);
        Assert.Equal(currency, handler.LastQuery!.Currency);
    }

    [Fact]
    public async Task GetCurrencyRateHistoryAsync_Should_DelegateToHandler_WithDateRangeAndCurrency()
    {
        var expected = Result.Success(new GetCurrencyRateHistory.Response([]));
        var handler =
            new RecordingQueryHandler<GetCurrencyRateHistory.Query, GetCurrencyRateHistory.Response>(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrencyRateHistory: handler);
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 5);
        var currency = new CurrencyCode(18);

        var result = await client.GetCurrencyRateHistoryAsync(from, to, currency);

        Assert.Same(expected, result);
        Assert.Equal(from, handler.LastQuery!.From);
        Assert.Equal(to, handler.LastQuery!.To);
        Assert.Equal(currency, handler.LastQuery!.Currency);
    }

    private static BanguatExchangeRateClient CreateClient(
        RecordingQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>? getCurrentUsdRate = null,
        RecordingQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response>? getCurrentUsdRateText =
            null,
        RecordingQueryHandler<GetAvailableCurrencies.Query, GetAvailableCurrencies.Response>? getAvailableCurrencies =
            null,
        RecordingQueryHandler<GetCurrentRate.Query, GetCurrentRate.Response>? getCurrentRate = null,
        RecordingQueryHandler<GetUsdRateHistorySince.Query, GetUsdRateHistorySince.Response>? getUsdRateHistorySince =
            null,
        RecordingQueryHandler<GetUsdRateHistory.Query, GetUsdRateHistory.Response>? getUsdRateHistory = null,
        RecordingQueryHandler<GetCurrencyRateHistorySince.Query, GetCurrencyRateHistorySince.Response>?
            getCurrencyRateHistorySince = null,
        RecordingQueryHandler<GetCurrencyRateHistory.Query, GetCurrencyRateHistory.Response>? getCurrencyRateHistory =
            null)
    {
        return new BanguatExchangeRateClient(
            getCurrentUsdRate ?? new RecordingQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>(
                Result.Success(new GetCurrentUsdRate.Response(default, default))),
            getCurrentUsdRateText ??
            new RecordingQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response>(
                Result.Success(new GetCurrentUsdRateText.Response(string.Empty))),
            getAvailableCurrencies ??
            new RecordingQueryHandler<GetAvailableCurrencies.Query, GetAvailableCurrencies.Response>(
                Result.Success(new GetAvailableCurrencies.Response([]))),
            getCurrentRate ?? new RecordingQueryHandler<GetCurrentRate.Query, GetCurrentRate.Response>(
                Result.Success(new GetCurrentRate.Response([]))),
            getUsdRateHistorySince ??
            new RecordingQueryHandler<GetUsdRateHistorySince.Query, GetUsdRateHistorySince.Response>(
                Result.Success(new GetUsdRateHistorySince.Response([]))),
            getUsdRateHistory ?? new RecordingQueryHandler<GetUsdRateHistory.Query, GetUsdRateHistory.Response>(
                Result.Success(new GetUsdRateHistory.Response([]))),
            getCurrencyRateHistorySince ??
            new RecordingQueryHandler<GetCurrencyRateHistorySince.Query, GetCurrencyRateHistorySince.Response>(
                Result.Success(new GetCurrencyRateHistorySince.Response([]))),
            getCurrencyRateHistory ??
            new RecordingQueryHandler<GetCurrencyRateHistory.Query, GetCurrencyRateHistory.Response>(
                Result.Success(new GetCurrencyRateHistory.Response([]))));
    }
}