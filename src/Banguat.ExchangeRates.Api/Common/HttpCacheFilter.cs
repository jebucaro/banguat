using System.Globalization;
using System.Text.Json;
using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Banguat.ExchangeRates.Api.Common;

/// <summary>
/// Attaches calendar-aligned cache headers and an entity tag to a successful response.
/// </summary>
/// <param name="resolveLifetime">
/// How long this endpoint's representation stays valid, resolved per request against the shared rate
/// calendar so the advertised <c>max-age</c> expires at the same instant as the server-side cache entry.
/// </param>
internal sealed class HttpCacheFilter(Func<HttpContext, IRateCalendar, TimeSpan> resolveLifetime) : IEndpointFilter
{
    // Accept-Encoding because responses are compressed per client; Origin because the CORS policy echoes a
    // per-caller Access-Control-Allow-Origin, and a shared cache must not replay one origin's value.
    private const string VaryValue = "Accept-Encoding, Origin";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        object? result = await next(context);
        HttpResponse response = context.HttpContext.Response;

        if (CacheableValue(result) is not { } value)
        {
            // Never cache a failure. A transient 502 from the upstream SOAP service would otherwise be
            // replayed to every client for the rest of the day.
            response.Headers.CacheControl = "no-store";
            return result;
        }

        IServiceProvider services = context.HttpContext.RequestServices;
        IRateCalendar calendar = services.GetRequiredService<IRateCalendar>();
        HttpCacheOptions options = services.GetRequiredService<IOptions<HttpCacheOptions>>().Value;

        byte[] payload = Serialize(value, services);
        string etag = ResponseETag.Compute(payload);

        int maxAgeSeconds = (int)Math.Max(
            resolveLifetime(context.HttpContext, calendar).TotalSeconds,
            options.MinimumMaxAge.TotalSeconds);

        response.Headers.CacheControl = BuildCacheControl(maxAgeSeconds, options);
        response.Headers.ETag = etag;
        response.Headers.Vary = VaryValue;

        if (IsNotModified(context.HttpContext.Request, etag))
        {
            // The client already holds this representation. The handler still ran — Valkey makes that cheap
            // — but the body is skipped, which is where the cost actually is.
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        // Return the bytes already hashed rather than the original result, so the entity tag is guaranteed
        // to describe what was actually sent and the payload is serialized only once.
        return new PrecomputedJsonResult(payload);
    }

    /// <summary>
    /// Evaluates <c>If-None-Match</c> using the weak comparison RFC 9110 mandates for conditional GETs.
    /// </summary>
    private static bool IsNotModified(HttpRequest request, string etag)
    {
        StringValues ifNoneMatch = request.Headers.IfNoneMatch;
        if (StringValues.IsNullOrEmpty(ifNoneMatch))
        {
            return false;
        }

        // Base64 tags never contain a comma, so splitting on it cannot break a tag apart.
        return ifNoneMatch
            .OfType<string>()
            .SelectMany(headerValue => headerValue.Split(',', StringSplitOptions.TrimEntries))
            .Any(candidate => MatchesETag(candidate, etag));
    }

    private static bool MatchesETag(string candidate, string etag)
    {
        // "*" means "any current representation", and one always exists at this point.
        if (candidate == "*")
        {
            return true;
        }

        // Weak comparison ignores the W/ prefix; a proxy may have weakened our strong tag.
        if (candidate.StartsWith("W/", StringComparison.Ordinal))
        {
            candidate = candidate[2..];
        }

        return candidate.Length > 0 && string.Equals(candidate, etag, StringComparison.Ordinal);
    }

    /// <summary>Returns the payload of a cacheable 200 response, or <c>null</c> for anything else.</summary>
    private static object? CacheableValue(object? result)
    {
        IResult? actual = result switch
        {
            // Results<T1, T2> wraps the chosen result rather than implementing its interfaces directly.
            INestedHttpResult nested => nested.Result,
            IResult direct => direct,
            _ => null
        };

        return actual is IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK }
               && actual is IValueHttpResult { Value: not null } valued
            ? valued.Value
            : null;
    }

    private static byte[] Serialize(object value, IServiceProvider services)
    {
        JsonSerializerOptions serializerOptions =
            services.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions ?? JsonSerializerOptions.Web;

        // The runtime type must be passed explicitly; serializing through `object` would emit "{}".
        return JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), serializerOptions);
    }

    private static string BuildCacheControl(int maxAgeSeconds, HttpCacheOptions options)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"public, max-age={maxAgeSeconds}, s-maxage={maxAgeSeconds}, " +
            $"stale-while-revalidate={(int)options.StaleWhileRevalidate.TotalSeconds}");
    }

    private sealed class PrecomputedJsonResult(byte[] payload) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.ContentLength = payload.Length;
            return httpContext.Response.Body.WriteAsync(payload, 0, payload.Length);
        }
    }
}