namespace Banguat.ExchangeRates.Common.Messaging;

public interface ICacheableQuery
{
    /// <summary>
    /// How long the result stays valid. The calendar is passed in so this follows the Guatemala day; against UTC,
    /// the last six hours of every day would be wrong.
    /// </summary>
    TimeSpan GetCacheDuration(IRateCalendar calendar);
}