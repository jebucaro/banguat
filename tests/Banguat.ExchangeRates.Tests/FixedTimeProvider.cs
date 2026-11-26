namespace Banguat.ExchangeRates.Tests;

// Tiny TimeProvider stub, so we don't pull in Microsoft.Extensions.TimeProvider.Testing for a handful of instants.
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return UtcNow;
    }
}