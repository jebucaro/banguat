namespace Banguat.ExchangeRates.Common;

/// <summary>
/// Bounds history queries before they reach Banguat. Otherwise an anonymous caller can trigger SOAP faults
/// (tripping the shared circuit breaker for everyone) or ask for decades of data under endless cache keys.
/// </summary>
public static class RateHistoryLimits
{
    /// <summary>
    /// Earlier starts return the same rows under a new cache key, and very early ones make Banguat fail.
    /// </summary>
    public static readonly DateOnly EarliestDate = new(1989, 11, 6);

    /// <summary>Inclusive of both ends.</summary>
    public const int MaxDays = 366;

    /// <summary>An open-ended "since" query passes today as the end, so it gets the same span limit.</summary>
    public static Result Validate(DateOnly from, DateOnly to, IRateCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (from < EarliestDate)
        {
            return Result.Failure(BanguatErrors.DateBeforeEarliest(EarliestDate));
        }

        if (from > calendar.Today || to > calendar.Today)
        {
            return Result.Failure(BanguatErrors.DateInFuture());
        }

        if (to < from)
        {
            return Result.Failure(BanguatErrors.InvalidDateRange());
        }

        return to.DayNumber - from.DayNumber >= MaxDays
            ? Result.Failure(BanguatErrors.DateRangeTooLong(MaxDays))
            : Result.Success();
    }
}