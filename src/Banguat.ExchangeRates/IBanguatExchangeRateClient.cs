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

    /// <summary>
    /// Pass since alone for an open-ended range, or from and to for a bounded one. Anything else fails with
    /// Banguat.InvalidHistoryRange.
    /// </summary>
    Task<Result<GetRateHistory.Response>> GetRateHistoryAsync(
        CurrencyCode currency,
        DateOnly? since = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}