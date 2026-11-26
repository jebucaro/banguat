namespace Banguat.ExchangeRates.Diagnostics;

public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    // Keyed by slice name, e.g. "GetRateHistory".
    public Dictionary<string, TimeSpan> DurationOverrides { get; init; } = new();
}