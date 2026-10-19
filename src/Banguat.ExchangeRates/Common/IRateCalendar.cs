namespace Banguat.ExchangeRates.Common;

/// <summary>
/// Single source of truth for Banguat's publication calendar. Banguat publishes at most one reference
/// rate per business day on the Guatemala civil calendar, so cached values stay valid until the next
/// local day begins rather than for a rolling duration.
/// </summary>
public interface IRateCalendar
{
    /// <summary>Current date on the Guatemala civil calendar, which trails UTC by six hours.</summary>
    DateOnly Today { get; }

    /// <summary>The absolute instant at which the next Guatemala day begins.</summary>
    DateTimeOffset NextDayBoundary { get; }

    /// <summary>
    /// Time remaining until <see cref="NextDayBoundary"/>. Both the Valkey entry lifetime and the HTTP
    /// <c>max-age</c> derive from this value so the two layers expire together instead of stacking.
    /// </summary>
    TimeSpan TimeUntilNextDay { get; }
}
