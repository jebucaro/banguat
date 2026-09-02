namespace Banguat.ExchangeRates.Diagnostics;

internal static class CacheDurationDefaults
{
    internal static readonly TimeSpan CurrentRate = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan Catalog = TimeSpan.FromHours(24);
    internal static readonly TimeSpan HistoricalRange = TimeSpan.FromHours(24);
}
