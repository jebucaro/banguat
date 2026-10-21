namespace Banguat.ExchangeRates.Diagnostics;

internal static class CacheDurationDefaults
{
    /// <summary>
    /// Lifetime for a date range that ended before today on the Guatemala calendar. Such a range can no
    /// longer gain data points, so it is cached long. It is not cached indefinitely because Banguat could
    /// in principle correct a published value.
    /// </summary>
    internal static readonly TimeSpan StableHistory = TimeSpan.FromHours(24);
}