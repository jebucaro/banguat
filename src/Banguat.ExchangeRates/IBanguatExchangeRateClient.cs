using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates;

public interface IBanguatExchangeRateClient
{
    Task<Result<GetCurrentUsdRate.Response>> GetCurrentUsdRateAsync(CancellationToken cancellationToken = default);

    Task<Result<GetCurrentUsdRateText.Response>> GetCurrentUsdRateTextAsync(
        CancellationToken cancellationToken = default);

    Task<Result<GetAvailableCurrencies.Response>> GetAvailableCurrenciesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<GetCurrentRate.Response>> GetCurrentRateAsync(
        CurrencyCode currency, CancellationToken cancellationToken = default);

    Task<Result<GetUsdRateHistorySince.Response>> GetUsdRateHistorySinceAsync(
        DateOnly since, CancellationToken cancellationToken = default);

    Task<Result<GetUsdRateHistory.Response>> GetUsdRateHistoryAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<Result<GetCurrencyRateHistorySince.Response>> GetCurrencyRateHistorySinceAsync(
        DateOnly since, CurrencyCode currency, CancellationToken cancellationToken = default);

    Task<Result<GetCurrencyRateHistory.Response>> GetCurrencyRateHistoryAsync(
        DateOnly from, DateOnly to, CurrencyCode currency, CancellationToken cancellationToken = default);
}