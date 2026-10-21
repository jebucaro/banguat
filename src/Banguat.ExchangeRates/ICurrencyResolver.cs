using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates;

public interface ICurrencyResolver
{
    /// <summary>
    /// Resolves a currency alias (e.g. "USD") or a Banguat numeric code (e.g. "24") to a <see cref="CurrencyCode"/>,
    /// failing with <see cref="BanguatErrors.UnknownCurrency"/> when it is neither, or with the upstream error when
    /// checking a numeric code against Banguat's catalog finds Banguat unreachable.
    /// </summary>
    Task<Result<CurrencyCode>> ResolveAsync(string currency, CancellationToken cancellationToken = default);
}