using Banguat.ExchangeRates.Diagnostics;

namespace Banguat.ExchangeRates.Common;

public static class RateCalendarExtensions
{
    /// <summary>
    /// Lifetime for a result covering a date range that ends on <paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// Defined once and shared by the query records (which set the Valkey entry lifetime) and the API's
    /// cache headers (which set <c>max-age</c>), so the two layers cannot classify the same range
    /// differently and expire at different instants.
    /// </remarks>
    public static TimeSpan LifetimeForRangeEnding(this IRateCalendar calendar, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return to < calendar.Today
            ? CacheDurationDefaults.StableHistory
            : calendar.TimeUntilNextDay;
    }
}