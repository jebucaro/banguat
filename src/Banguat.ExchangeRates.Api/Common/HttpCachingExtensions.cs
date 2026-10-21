using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Api.Common;

internal static class HttpCachingExtensions
{
    /// <summary>
    /// Serves this endpoint with calendar-aligned cache headers and conditional-request support.
    /// </summary>
    internal static RouteHandlerBuilder WithHttpCaching(
        this RouteHandlerBuilder builder, Func<HttpContext, IRateCalendar, TimeSpan> resolveLifetime)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resolveLifetime);

        return builder
            .AddEndpointFilter(new HttpCacheFilter(resolveLifetime))
            .Produces(StatusCodes.Status304NotModified);
    }

    /// <summary>
    /// Caches until the next Guatemala day, for endpoints whose representation always reflects today.
    /// </summary>
    internal static RouteHandlerBuilder WithHttpCaching(this RouteHandlerBuilder builder)
    {
        return builder.WithHttpCaching(static (_, calendar) => calendar.TimeUntilNextDay);
    }
}