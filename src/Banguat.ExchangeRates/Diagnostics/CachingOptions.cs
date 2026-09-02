namespace Banguat.ExchangeRates.Diagnostics;

public sealed class CachingOptions
{
    public Dictionary<string, TimeSpan> DurationOverrides { get; init; } = new();
}
