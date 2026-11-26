namespace Banguat.ExchangeRates.Diagnostics;

internal static class CacheDurationDefaults
{
    // Ranges that ended before today can't gain points, so they're cached long, but not forever in case Banguat
    // corrects a value.
    internal static readonly TimeSpan StableHistory = TimeSpan.FromHours(24);
}