namespace Banguat.ExchangeRates.Common.Messaging;

public interface ICacheableQuery
{
    TimeSpan CacheDuration { get; }
}
