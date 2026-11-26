using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates;

public interface ICurrencyResolver
{
    /// <summary>
    /// Takes an alias ("USD") or a Banguat numeric code ("24"). Unknown input fails with UnknownCurrency, and if
    /// checking a numeric code finds Banguat unreachable you get that upstream error instead.
    /// </summary>
    Task<Result<CurrencyCode>> ResolveAsync(string currency, CancellationToken cancellationToken = default);
}