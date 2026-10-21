namespace Banguat.ExchangeRates.Common.Messaging;

public interface ICacheableQuery
{
    /// <summary>
    /// How long this query's result stays valid, resolved against Banguat's publication calendar.
    /// </summary>
    /// <remarks>
    /// The calendar is passed in rather than read from an ambient clock so freshness decisions are made on
    /// the Guatemala civil day. Evaluating them against UTC misclassifies the final six hours of every
    /// local day, when the UTC date has already rolled over but Banguat has not published again.
    /// </remarks>
    TimeSpan GetCacheDuration(IRateCalendar calendar);
}