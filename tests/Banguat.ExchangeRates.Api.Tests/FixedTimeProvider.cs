namespace Banguat.ExchangeRates.Api.Tests;

/// <summary>Minimal <see cref="TimeProvider"/> stub so cache headers can be asserted at a pinned instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}