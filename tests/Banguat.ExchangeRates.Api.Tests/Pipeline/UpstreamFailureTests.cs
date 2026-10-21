using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Banguat.ExchangeRates.Api.Tests.Pipeline;

public sealed class UpstreamFailureTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _http;

    public UpstreamFailureTests()
    {
        _http = _factory.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
    }

    private void GivenRateFails(Error error)
    {
        _factory.Client.GetCurrentRateAsync(new CurrencyCode(2), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrentRate.Response>(error));
    }

    private static async Task AssertGatewayProblemAsync(HttpResponseMessage response, int status, Error error)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(error.Description, body.RootElement.GetProperty("detail").GetString());
        Assert.Equal(error.Code, body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetRate_UpstreamTimeout_Returns504ProblemWithNoStore()
    {
        GivenRateFails(BanguatErrors.Timeout());

        using HttpResponseMessage response = await _http.GetAsync("/v1/rates/usd");

        await AssertGatewayProblemAsync(response, StatusCodes.Status504GatewayTimeout, BanguatErrors.Timeout());
    }

    [Fact]
    public async Task GetRate_CircuitOpen_Returns503Problem()
    {
        GivenRateFails(BanguatErrors.Unavailable());

        using HttpResponseMessage response = await _http.GetAsync("/v1/rates/usd");

        await AssertGatewayProblemAsync(
            response, StatusCodes.Status503ServiceUnavailable, BanguatErrors.Unavailable());
    }

    [Fact]
    public async Task GetRate_TransportFailure_Returns502Problem()
    {
        GivenRateFails(BanguatErrors.TransportFailure());

        using HttpResponseMessage response = await _http.GetAsync("/v1/rates/usd");

        await AssertGatewayProblemAsync(response, StatusCodes.Status502BadGateway, BanguatErrors.TransportFailure());
    }

    [Fact]
    public async Task GetRate_UnexpectedException_ReturnsGeneric500Problem()
    {
        _factory.Client.GetCurrentRateAsync(new CurrencyCode(2), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("secret detail"));

        using HttpResponseMessage response = await _http.GetAsync("/v1/rates/usd");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret detail", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task GetRate_Success_ReturnsETagAndRevalidatesTo304()
    {
        _factory.Client.GetCurrentRateAsync(new CurrencyCode(2), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrentRate.Response(
                [new GetCurrentRate.RatePoint(new DateOnly(2026, 10, 20), 7.6m, 7.61m)])));

        using HttpResponseMessage first = await _http.GetAsync("/v1/rates/usd");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        EntityTagHeaderValue etag = Assert.IsType<EntityTagHeaderValue>(first.Headers.ETag);

        using HttpRequestMessage revalidate = new(HttpMethod.Get, "/v1/rates/usd");
        revalidate.Headers.IfNoneMatch.Add(etag);
        using HttpResponseMessage second = await _http.SendAsync(revalidate);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task GetRateHistory_CatalogTimesOutForUnaliasedCode_Returns504()
    {
        _factory.Client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetAvailableCurrencies.Response>(BanguatErrors.Timeout()));

        using HttpResponseMessage response = await _http.GetAsync("/v1/rates/28/history?since=2026-08-01");

        await AssertGatewayProblemAsync(response, StatusCodes.Status504GatewayTimeout, BanguatErrors.Timeout());
        await _factory.Client.DidNotReceive().GetRateHistoryAsync(
            Arg.Any<CurrencyCode>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(),
            Arg.Any<CancellationToken>());
    }
}