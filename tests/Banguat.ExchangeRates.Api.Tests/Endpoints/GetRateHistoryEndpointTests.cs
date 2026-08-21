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

    [Fact]
    public async Task HandleAsync_Since_ReturnsHistory()
    {
        CurrencyCode eur = new(24);
        GetCurrencyRateHistorySince.Response response = new(
            [new GetCurrencyRateHistorySince.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", "2026-08-01", null, null, _client, _aliasCatalog, CancellationToken.None);

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
        GetCurrencyRateHistory.Response response = new(
            [new GetCurrencyRateHistory.RatePoint(new DateOnly(2026, 8, 1), 1.1523m, 1.1523m)]);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(response));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", null, "2026-08-01", "2026-08-02", _client, _aliasCatalog, CancellationToken.None);

        Ok<GetRateHistoryEndpoint.RateHistoryResponse> ok =
            Assert.IsType<Ok<GetRateHistoryEndpoint.RateHistoryResponse>>(result.Result);
        Assert.Equal(1, ok.Value!.Count);
    }

    [Fact]
    public async Task HandleAsync_FromAndTo_EmptyRange_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrencyRateHistoryAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), eur,
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrencyRateHistory.Response([])));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "24", null, "2026-08-01", "2026-08-02", _client, _aliasCatalog, CancellationToken.None);

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
                "not-a-currency", "2026-08-01", null, null, _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NeitherSinceNorRange_ReturnsBadRequestProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", null, null, null, _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_BothSinceAndRange_ReturnsBadRequestProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", "2026-08-01", "2026-08-01", "2026-08-02", _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_PartialRange_ReturnsBadRequestProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", null, "2026-08-01", null, _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidDate_ReturnsBadRequestProblem()
    {
        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", "08/01/2026", null, null, _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_ReturnsProblem()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrencyRateHistorySinceAsync(new DateOnly(2026, 8, 1), usd, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrencyRateHistorySince.Response>(
                Error.Failure("Banguat.TransportFailure", "boom")));

        Results<Ok<GetRateHistoryEndpoint.RateHistoryResponse>, ProblemHttpResult> result =
            await GetRateHistoryEndpoint.HandleAsync(
                "usd", "2026-08-01", null, null, _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("boom", problem.ProblemDetails.Detail);
    }
}