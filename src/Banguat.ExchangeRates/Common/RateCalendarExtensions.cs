using Banguat.ExchangeRates.Diagnostics;

namespace Banguat.ExchangeRates.Common;

public static class RateCalendarExtensions
{
    public static TimeSpan LifetimeForRangeEnding(this IRateCalendar calendar, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return to < calendar.Today
            ? CacheDurationDefaults.StableHistory
            : calendar.TimeUntilNextDay;
    }
}