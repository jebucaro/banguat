using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates.Tests;

public class BanguatExchangeRateClientTests
{
    [Fact]
    public async Task GetCurrentUsdRateAsync_Should_DelegateToHandler()
    {
        Result<GetCurrentUsdRate.Response> expected =
            Result.Success(new GetCurrentUsdRate.Response(new DateOnly(2026, 8, 17), 7.61992m));
        RecordingQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(handler);

        Result<GetCurrentUsdRate.Response> result = await client.GetCurrentUsdRateAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetCurrentUsdRateTextAsync_Should_DelegateToHandler()
    {
        Result<GetCurrentUsdRateText.Response> expected = Result.Success(new GetCurrentUsdRateText.Response("text"));
        RecordingQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrentUsdRateText: handler);

        Result<GetCurrentUsdRateText.Response> result = await client.GetCurrentUsdRateTextAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetAvailableCurrenciesAsync_Should_DelegateToHandler()
    {
        Result<GetAvailableCurrencies.Response> expected = Result.Success(new GetAvailableCurrencies.Response([]));
        RecordingQueryHandler<GetAvailableCurrencies.Query, GetAvailableCurrencies.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getAvailableCurrencies: handler);

        Result<GetAvailableCurrencies.Response> result = await client.GetAvailableCurrenciesAsync();

        Assert.Same(expected, result);
        Assert.NotNull(handler.LastQuery);
    }

    [Fact]
    public async Task GetCurrentRateAsync_Should_DelegateToHandler_WithCurrency()
    {
        Result<GetCurrentRate.Response> expected = Result.Success(new GetCurrentRate.Response([]));
        RecordingQueryHandler<GetCurrentRate.Query, GetCurrentRate.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrentRate: handler);
        CurrencyCode currency = new(18);

        Result<GetCurrentRate.Response> result = await client.GetCurrentRateAsync(currency);

        Assert.Same(expected, result);
        Assert.Equal(currency, handler.LastQuery!.Currency);
    }

    [Fact]
    public async Task GetUsdRateHistorySinceAsync_Should_DelegateToHandler_WithDate()
    {
        Result<GetUsdRateHistorySince.Response> expected = Result.Success(new GetUsdRateHistorySince.Response([]));
        RecordingQueryHandler<GetUsdRateHistorySince.Query, GetUsdRateHistorySince.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getUsdRateHistorySince: handler);
        DateOnly since = new(2026, 8, 1);

        Result<GetUsdRateHistorySince.Response> result = await client.GetUsdRateHistorySinceAsync(since);

        Assert.Same(expected, result);
        Assert.Equal(since, handler.LastQuery!.Since);
    }

    [Fact]
    public async Task GetUsdRateHistoryAsync_Should_DelegateToHandler_WithDateRange()
    {
        Result<GetUsdRateHistory.Response> expected = Result.Success(new GetUsdRateHistory.Response([]));
        RecordingQueryHandler<GetUsdRateHistory.Query, GetUsdRateHistory.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getUsdRateHistory: handler);
        DateOnly from = new(2026, 8, 1);
        DateOnly to = new(2026, 8, 5);

        Result<GetUsdRateHistory.Response> result = await client.GetUsdRateHistoryAsync(from, to);

        Assert.Same(expected, result);
        Assert.Equal(from, handler.LastQuery!.From);
        Assert.Equal(to, handler.LastQuery!.To);
    }

    [Fact]
    public async Task GetCurrencyRateHistorySinceAsync_Should_DelegateToHandler_WithDateAndCurrency()
    {
        Result<GetCurrencyRateHistorySince.Response> expected =
            Result.Success(new GetCurrencyRateHistorySince.Response([]));
        RecordingQueryHandler<GetCurrencyRateHistorySince.Query, GetCurrencyRateHistorySince.Response> handler =
            new(
                expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrencyRateHistorySince: handler);
        DateOnly since = new(2026, 8, 1);
        CurrencyCode currency = new(18);

        Result<GetCurrencyRateHistorySince.Response> result =
            await client.GetCurrencyRateHistorySinceAsync(since, currency);

        Assert.Same(expected, result);
        Assert.Equal(since, handler.LastQuery!.Since);
        Assert.Equal(currency, handler.LastQuery!.Currency);
    }

    [Fact]
    public async Task GetCurrencyRateHistoryAsync_Should_DelegateToHandler_WithDateRangeAndCurrency()
    {
        Result<GetCurrencyRateHistory.Response> expected = Result.Success(new GetCurrencyRateHistory.Response([]));
        RecordingQueryHandler<GetCurrencyRateHistory.Query, GetCurrencyRateHistory.Response> handler = new(expected);
        IBanguatExchangeRateClient client = CreateClient(getCurrencyRateHistory: handler);
        DateOnly from = new(2026, 8, 1);
        DateOnly to = new(2026, 8, 5);
        CurrencyCode currency = new(18);

        Result<GetCurrencyRateHistory.Response> result = await client.GetCurrencyRateHistoryAsync(from, to, currency);

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