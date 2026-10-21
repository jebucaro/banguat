namespace Banguat.ExchangeRates.Common;

/// <summary>
/// Bounds every history query before it reaches Banguat. Without them an anonymous caller could ask for
/// dates Banguat answers with a SOAP fault (tripping the shared circuit breaker for everyone) or for decades
/// of data under endlessly varied cache keys.
/// </summary>
public static class RateHistoryLimits
{
    /// <summary>
    /// First day Banguat's history operations return data for. Earlier starts return the same rows under a
    /// new cache key, and very early ones make Banguat fail.
    /// </summary>
    public static readonly DateOnly EarliestDate = new(1989, 11, 6);

    /// <summary>Longest range, in days with both ends included, that one query may cover.</summary>
    public const int MaxDays = 366;

    /// <summary>
    /// Checks an inclusive <paramref name="from"/>..<paramref name="to"/> range. An open-ended "since" query
    /// passes today as <paramref name="to"/>, so it is held to the same span.
    /// </summary>
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