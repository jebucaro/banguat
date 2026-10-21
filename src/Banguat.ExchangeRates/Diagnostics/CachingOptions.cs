namespace Banguat.ExchangeRates.Diagnostics;

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    // Keyed by slice name (e.g. "GetRateHistory"); bound from "Caching:DurationOverrides:<slice>".
    public Dictionary<string, TimeSpan> DurationOverrides { get; init; } = new();
}