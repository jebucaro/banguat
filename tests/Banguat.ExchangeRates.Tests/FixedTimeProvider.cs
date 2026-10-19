namespace Banguat.ExchangeRates.Tests;

/// <summary>
/// Minimal <see cref="TimeProvider"/> stub. Avoids taking a dependency on
/// Microsoft.Extensions.TimeProvider.Testing for the handful of instants these tests pin.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
