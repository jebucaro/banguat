using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Options;

namespace Banguat.ExchangeRates.Api.Tests.Common;

public class HttpCacheFilterTests
{
    private sealed record Payload(int Currency, decimal Buy);

    // 2026-09-23T01:00Z is 19:00 on 2026-09-22 in Guatemala, leaving exactly five hours (18000s) of the
    // local day. Every max-age expectation below is derived from that instant.
    private static readonly DateTimeOffset PinnedInstant = new(2026, 9, 23, 1, 0, 0, TimeSpan.Zero);
    private const int SecondsUntilNextGuatemalaDay = 5 * 60 * 60;

    private static DefaultHttpContext CreateContext(HttpCacheOptions? options = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IRateCalendar>(
            new GuatemalaRateCalendar(new FixedTimeProvider(PinnedInstant)));
        services.AddSingleton(Options.Create(options ?? new HttpCacheOptions()));

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static async Task<(DefaultHttpContext Context, object? Result)> RunAsync(
        object? endpointResult,
        Func<HttpContext, IRateCalendar, TimeSpan>? resolveLifetime = null,
        HttpCacheOptions? options = null,
        Action<DefaultHttpContext>? configureRequest = null)
    {
        DefaultHttpContext context = CreateContext(options);
        configureRequest?.Invoke(context);

        HttpCacheFilter filter = new(resolveLifetime ?? ((_, calendar) => calendar.TimeUntilNextDay));
        EndpointFilterInvocationContext invocation = EndpointFilterInvocationContext.Create(context);

        object? result = await filter.InvokeAsync(invocation, _ => ValueTask.FromResult(endpointResult));

        return (context, result);
    }

    private static Results<Ok<Payload>, ProblemHttpResult> Success(Payload payload)
    {
        return TypedResults.Ok(payload);
    }

    [Fact]
    public async Task Success_SetsMaxAgeToTimeRemainingInTheGuatemalaDay()
    {
        (DefaultHttpContext context, _) = await RunAsync(Success(new Payload(2, 7.65m)));

        Assert.Equal(
            $"public, max-age={SecondsUntilNextGuatemalaDay}, s-maxage={SecondsUntilNextGuatemalaDay}, "
            + "stale-while-revalidate=60",
            context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Success_SetsAQuotedEntityTag()
    {
        (DefaultHttpContext context, _) = await RunAsync(Success(new Payload(2, 7.65m)));

        string etag = context.Response.Headers.ETag.ToString();
        Assert.StartsWith("\"", etag, StringComparison.Ordinal);
        Assert.EndsWith("\"", etag, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Success_VariesOnAcceptEncodingAndOrigin()
    {
        (DefaultHttpContext context, _) = await RunAsync(Success(new Payload(2, 7.65m)));

        Assert.Equal("Accept-Encoding, Origin", context.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task Success_ProducesTheSameTagForTheSamePayload()
    {
        (DefaultHttpContext first, _) = await RunAsync(Success(new Payload(2, 7.65m)));
        (DefaultHttpContext second, _) = await RunAsync(Success(new Payload(2, 7.65m)));

        Assert.Equal(first.Response.Headers.ETag.ToString(), second.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task Success_ProducesADifferentTagWhenThePayloadChanges()
    {
        (DefaultHttpContext first, _) = await RunAsync(Success(new Payload(2, 7.65m)));
        (DefaultHttpContext second, _) = await RunAsync(Success(new Payload(2, 7.66m)));

        Assert.NotEqual(first.Response.Headers.ETag.ToString(), second.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task Success_WritesTheSerializedPayloadThatWasHashed()
    {
        (DefaultHttpContext context, object? result) = await RunAsync(Success(new Payload(2, 7.65m)));
        context.Response.Body = new MemoryStream();

        await Assert.IsType<IResult>(result, false).ExecuteAsync(context);

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Equal("""{"currency":2,"buy":7.65}""", body);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
    }

    // Errors are never stored, otherwise an edge would cache the 502 and serve it to everyone until the boundary.
    [Fact]
    public async Task Failure_IsMarkedNoStoreAndCarriesNoEntityTag()
    {
        Results<Ok<Payload>, ProblemHttpResult> failure =
            TypedResults.Problem("upstream unavailable", statusCode: StatusCodes.Status502BadGateway);

        (DefaultHttpContext context, object? result) = await RunAsync(failure);

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.True(StringValues.IsNullOrEmpty(context.Response.Headers.ETag));
        Assert.Same(failure.Result, Assert.IsType<INestedHttpResult>(result, false).Result);
    }

    [Fact]
    public async Task MaxAge_IsFlooredSoItNeverApproachesZeroAtTheBoundary()
    {
        (DefaultHttpContext context, _) = await RunAsync(
            Success(new Payload(2, 7.65m)),
            (_, _) => TimeSpan.FromSeconds(1));

        Assert.Contains("max-age=30", context.Response.Headers.CacheControl.ToString());
        Assert.Contains("s-maxage=30", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task StaleWhileRevalidate_IsTakenFromOptions()
    {
        HttpCacheOptions options = new() { StaleWhileRevalidate = TimeSpan.FromSeconds(15) };

        (DefaultHttpContext context, _) = await RunAsync(Success(new Payload(2, 7.65m)), options: options);

        Assert.Contains("stale-while-revalidate=15", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Lifetime_CanBeResolvedFromTheRequest()
    {
        (DefaultHttpContext context, _) = await RunAsync(
            Success(new Payload(2, 7.65m)),
            (http, calendar) => http.Request.Query.ContainsKey("to")
                ? TimeSpan.FromHours(24)
                : calendar.TimeUntilNextDay,
            configureRequest: http => http.Request.QueryString = new QueryString("?to=2020-01-05"));

        Assert.Contains("max-age=86400", context.Response.Headers.CacheControl.ToString());
    }

    private static async Task<string> TagFor(Payload payload)
    {
        (DefaultHttpContext context, _) = await RunAsync(Success(payload));
        return context.Response.Headers.ETag.ToString();
    }

    private static Task<(DefaultHttpContext Context, object? Result)> RunWithIfNoneMatch(
        Payload payload, string ifNoneMatch)
    {
        return RunAsync(
            Success(payload),
            configureRequest: http => http.Request.Headers.IfNoneMatch = ifNoneMatch);
    }

    [Fact]
    public async Task IfNoneMatch_MatchingTag_Returns304WithNoBody()
    {
        Payload payload = new(2, 7.65m);
        string etag = await TagFor(payload);

        (DefaultHttpContext context, object? result) = await RunWithIfNoneMatch(payload, etag);

        Assert.Equal(StatusCodes.Status304NotModified,
            Assert.IsType<IStatusCodeHttpResult>(result, false).StatusCode);
        Assert.Null(context.Response.ContentLength);

        // The revalidation response must still carry the tag and freshness so the client can cache again.
        Assert.Equal(etag, context.Response.Headers.ETag.ToString());
        Assert.Contains($"max-age={SecondsUntilNextGuatemalaDay}",
            context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task IfNoneMatch_StaleTag_Returns200WithTheNewPayload()
    {
        (_, object? result) = await RunWithIfNoneMatch(new Payload(2, 7.66m), "\"a-previous-tag\"");

        Assert.IsNotType<StatusCodeHttpResult>(result);
    }

    // A proxy may downgrade a strong tag to a weak one; RFC 9110 weak comparison must still match.
    [Fact]
    public async Task IfNoneMatch_WeakenedTag_Returns304()
    {
        Payload payload = new(2, 7.65m);
        string etag = await TagFor(payload);

        (_, object? result) = await RunWithIfNoneMatch(payload, $"W/{etag}");

        Assert.Equal(StatusCodes.Status304NotModified,
            Assert.IsType<IStatusCodeHttpResult>(result, false).StatusCode);
    }

    [Fact]
    public async Task IfNoneMatch_Wildcard_Returns304()
    {
        (_, object? result) = await RunWithIfNoneMatch(new Payload(2, 7.65m), "*");

        Assert.Equal(StatusCodes.Status304NotModified,
            Assert.IsType<IStatusCodeHttpResult>(result, false).StatusCode);
    }

    [Fact]
    public async Task IfNoneMatch_ListContainingTheCurrentTag_Returns304()
    {
        Payload payload = new(2, 7.65m);
        string etag = await TagFor(payload);

        (_, object? result) = await RunWithIfNoneMatch(payload, $"\"stale-one\", {etag} , \"stale-two\"");

        Assert.Equal(StatusCodes.Status304NotModified,
            Assert.IsType<IStatusCodeHttpResult>(result, false).StatusCode);
    }

    [Fact]
    public async Task IfNoneMatch_ListWithoutTheCurrentTag_Returns200()
    {
        (_, object? result) = await RunWithIfNoneMatch(new Payload(2, 7.65m), "\"stale-one\", \"stale-two\"");

        Assert.IsNotType<StatusCodeHttpResult>(result);
    }

    // Conditional headers must not resurrect a failed response as a cheap 304.
    [Fact]
    public async Task IfNoneMatch_OnAFailure_IsIgnored()
    {
        Results<Ok<Payload>, ProblemHttpResult> failure =
            TypedResults.Problem("upstream unavailable", statusCode: StatusCodes.Status502BadGateway);

        (DefaultHttpContext context, object? result) = await RunAsync(
            failure, configureRequest: http => http.Request.Headers.IfNoneMatch = "*");

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Same(failure.Result, Assert.IsType<INestedHttpResult>(result, false).Result);
    }
}