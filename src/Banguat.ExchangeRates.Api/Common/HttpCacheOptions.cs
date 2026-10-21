namespace Banguat.ExchangeRates.Api.Common;

/// <summary>
/// Tunable parts of the HTTP cache headers. The <c>max-age</c> itself is not configurable: it is derived
/// from Banguat's publication calendar so it cannot drift from the server-side cache lifetime.
/// </summary>
public sealed class HttpCacheOptions
{
    public const string SectionName = "HttpCaching";

    /// <summary>
    /// Window during which a shared cache may serve a stale representation while refreshing in the
    /// background, so the day-boundary expiry does not produce a thundering herd at local midnight.
    /// </summary>
    public TimeSpan StaleWhileRevalidate { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Floor applied to <c>max-age</c>. Without it, a request arriving a second before the boundary would
    /// advertise <c>max-age=1</c> and effectively disable client caching for that request.
    /// </summary>
    public TimeSpan MinimumMaxAge { get; set; } = TimeSpan.FromSeconds(30);
}