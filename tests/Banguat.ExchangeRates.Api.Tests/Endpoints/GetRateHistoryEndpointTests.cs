using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Endpoints;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace Banguat.ExchangeRates.Api.Tests.Endpoints;

public class GetRateHistoryEndpointTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();
    private readonly ICurrencyResolver _currencyResolver;

    public GetRateHistoryEndpointTests()
    {
        _currencyResolver = new CurrencyResolver(_client, _aliasCatalog);
    }

    [Fact]
    public async Task HandleAsync_Since_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetRateHistory.Response response = new(
            [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetRateHistoryAsync(eur, new DateOnly(2026, 8, 1), null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", new GetRateHistoryEndpoint.HistoryRange("2026-08-01", null, null), _client, _currencyResolver,
                _aliasCatalog, CancellationToken.None);

        Ok<GetRateHistoryEndpoint.RateHistoryResponse> ok =
            Assert.IsType<Ok<GetRateHistoryEndpoint.RateHistoryResponse>>(result.Result);
        Assert.Equal(1, ok.Value!.Count);
        Assert.Equal(24, ok.Value.Currency);
        Assert.Equal(new DateOnly(2026, 8, 1), ok.Value.History[0].Date);
        Assert.Equal("EUR", ok.Value.CurrencyAlias);
    }

    [Fact]
    public async Task HandleAsync_FromAndTo_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetRateHistory.Response response = new(
            [new GetRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetRateHistoryAsync(eur, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", new GetRateHistoryEndpoint.HistoryRange(null, "2026-08-01", "2026-08-02"), _client,
                _currencyResolver, _aliasCatalog,
                CancellationToken.None);

        Ok<GetRateHistoryEndpoint.RateHistoryResponse> ok =
            Assert.IsType<Ok<GetRateHistoryEndpoint.RateHistoryResponse>>(result.Result);
        Assert.Equal(1, ok.Value!.Count);
    }

    [Fact]
    public async Task HandleAsync_FromAndTo_EmptyRange_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetRateHistoryAsync(eur, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetRateHistory.Response([])));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", new GetRateHistoryEndpoint.HistoryRange(null, "2026-08-01", "2026-08-02"), _client,
                _currencyResolver, _aliasCatalog,
                CancellationToken.None);

        Ok<GetRateHistoryEndpoint.RateHistoryResponse> ok =
            Assert.IsType<Ok<GetRateHistoryEndpoint.RateHistoryResponse>>(result.Result);
        Assert.Equal(0, ok.Value!.Count);
        Assert.Empty(ok.Value.History);
    }

    [Fact]
    public async Task HandleAsync_UnknownCurrency_ReturnsNotFoundProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "not-a-currency", new GetRateHistoryEndpoint.HistoryRange("2026-08-01", null, null), _client,
                _currencyResolver, _aliasCatalog,
                CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    // Validity rules live in the library (see GetRateHistoryTests). The endpoint just forwards the parsed dates
    // and turns the library's error into a 400.
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-08-01", "2026-08-01", "2026-08-02")]
    [InlineData(null, "2026-08-01", null)]
    public async Task HandleAsync_InvalidHistoryRange_ReturnsBadRequestProblem(string? since, string? from, string? to)
    {
        _client.GetRateHistoryAsync(Arg.Any<CurrencyCode>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(),
                Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetRateHistory.Response>(BanguatErrors.InvalidHistoryRange()));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", new GetRateHistoryEndpoint.HistoryRange(since, from, to), _client, _currencyResolver,
                _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("Banguat.InvalidHistoryRange", problem.ProblemDetails.Title);
        await _client.Received(1).GetRateHistoryAsync(
            new CurrencyCode(2), ParseOrNull(since), ParseOrNull(from), ParseOrNull(to), Arg.Any<CancellationToken>());
    }

    private static DateOnly? ParseOrNull(string? value)
    {
        return value is null ? null : DateOnly.Parse(value);
    }

    [Fact]
    public async Task HandleAsync_InvalidDate_ReturnsBadRequestProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", new GetRateHistoryEndpoint.HistoryRange("08/01/2026", null, null), _client, _currencyResolver,
                _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_ReturnsProblem()
    {
        CurrencyCode usd = new(2);
        _client.GetRateHistoryAsync(usd, new DateOnly(2026, 8, 1), null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetRateHistory.Response>(
                Error.Failure("Banguat.TransportFailure", "boom")));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", new GetRateHistoryEndpoint.HistoryRange("2026-08-01", null, null), _client, _currencyResolver,
                _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("boom", problem.ProblemDetails.Detail);
    }
}