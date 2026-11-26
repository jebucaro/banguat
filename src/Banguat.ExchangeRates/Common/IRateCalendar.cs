namespace Banguat.ExchangeRates.Common;

/// <summary>
/// Banguat publishes one rate per Guatemala business day, so cached values live until the next local day starts
/// instead of for a rolling duration.
/// </summary>
public interface IRateCalendar
{
    DateOnly Today { get; }

    DateTimeOffset NextDayBoundary { get; }

    TimeSpan TimeUntilNextDay { get; }
}