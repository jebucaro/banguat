using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates;

public sealed class BanguatExchangeRateClient(
    IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response> getCurrentUsdRate,
    IQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response> getCurrentUsdRateText,
    IQueryHandler<GetAvailableCurrencies.Query, GetAvailableCurrencies.Response> getAvailableCurrencies,
    IQueryHandler<GetCurrentRate.Query, GetCurrentRate.Response> getCurrentRate,
    IQueryHandler<GetUsdRateHistorySince.Query, GetUsdRateHistorySince.Response> getUsdRateHistorySince,
    IQueryHandler<GetUsdRateHistory.Query, GetUsdRateHistory.Response> getUsdRateHistory,
    IQueryHandler<GetCurrencyRateHistorySince.Query, GetCurrencyRateHistorySince.Response> getCurrencyRateHistorySince,
    IQueryHandler<GetCurrencyRateHistory.Query, GetCurrencyRateHistory.Response> getCurrencyRateHistory)
    : IBanguatExchangeRateClient
{
    public Task<Result<GetCurrentUsdRate.Response>> GetCurrentUsdRateAsync(
        CancellationToken cancellationToken = default)
    {
        return getCurrentUsdRate.Handle(new GetCurrentUsdRate.Query(), cancellationToken);
    }

    public Task<Result<GetCurrentUsdRateText.Response>> GetCurrentUsdRateTextAsync(
        CancellationToken cancellationToken = default)
    {
        return getCurrentUsdRateText.Handle(new GetCurrentUsdRateText.Query(), cancellationToken);
    }

    public Task<Result<GetAvailableCurrencies.Response>> GetAvailableCurrenciesAsync(
        CancellationToken cancellationToken = default)
    {
        return getAvailableCurrencies.Handle(new GetAvailableCurrencies.Query(), cancellationToken);
    }

    public Task<Result<GetCurrentRate.Response>> GetCurrentRateAsync(
        CurrencyCode currency, CancellationToken cancellationToken = default)
    {
        return getCurrentRate.Handle(new GetCurrentRate.Query(currency), cancellationToken);
    }

    public Task<Result<GetUsdRateHistorySince.Response>> GetUsdRateHistorySinceAsync(
        DateOnly since, CancellationToken cancellationToken = default)
    {
        return getUsdRateHistorySince.Handle(new GetUsdRateHistorySince.Query(since), cancellationToken);
    }

    public Task<Result<GetUsdRateHistory.Response>> GetUsdRateHistoryAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        return getUsdRateHistory.Handle(new GetUsdRateHistory.Query(from, to), cancellationToken);
    }

    public Task<Result<GetCurrencyRateHistorySince.Response>> GetCurrencyRateHistorySinceAsync(
        DateOnly since, CurrencyCode currency, CancellationToken cancellationToken = default)
    {
        return getCurrencyRateHistorySince.Handle(new GetCurrencyRateHistorySince.Query(since, currency),
            cancellationToken);
    }

    public Task<Result<GetCurrencyRateHistory.Response>> GetCurrencyRateHistoryAsync(
        DateOnly from, DateOnly to, CurrencyCode currency, CancellationToken cancellationToken = default)
    {
        return getCurrencyRateHistory.Handle(new GetCurrencyRateHistory.Query(from, to, currency), cancellationToken);
    }
}