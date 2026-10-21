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
    /// Gets a currency's rate history: pass <paramref name="since"/> alone for an open-ended range to today,
    /// or both <paramref name="from"/> and <paramref name="to"/> for a bounded one. Any other combination
    /// fails with <c>Banguat.InvalidHistoryRange</c>.
    /// </summary>
    Task<Result<GetRateHistory.Response>> GetRateHistoryAsync(
        CurrencyCode currency,
        DateOnly? since = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}