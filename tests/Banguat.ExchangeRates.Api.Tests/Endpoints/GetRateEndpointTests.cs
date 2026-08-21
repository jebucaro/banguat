using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Endpoints;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace Banguat.ExchangeRates.Api.Tests.Endpoints;

public class GetRateEndpointTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public async Task HandleAsync_KnownAlias_ReturnsRate()
    {
        CurrencyCode usd = new(2);
        GetCurrentRate.Response response = new(
            [new GetCurrentRate.RatePoint(new DateOnly(2026, 8, 18), 7.62157m, 7.62157m)]);
        _client.GetCurrentRateAsync(usd, Arg.Any<CancellationToken>()).Returns(Result.Success(response));

        Results<Ok<GetRateEndpoint.RateResponse>, ProblemHttpResult> result =
            await GetRateEndpoint.HandleAsync("usd", _client, _aliasCatalog, CancellationToken.None);

        Ok<GetRateEndpoint.RateResponse> ok = Assert.IsType<Ok<GetRateEndpoint.RateResponse>>(result.Result);
        Assert.Equal(2, ok.Value!.Currency);
        Assert.Equal(1, ok.Value.Count);
        Assert.Equal(7.62157m, ok.Value.Buy);
        Assert.Equal(new DateOnly(2026, 8, 18), ok.Value.Date);
        Assert.Equal("USD", ok.Value.CurrencyAlias);
    }

    [Fact]
    public async Task HandleAsync_NoDataForToday_ReturnsEmptyResult()
    {
        CurrencyCode eur = new(24);
        _client.GetCurrentRateAsync(eur, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new GetCurrentRate.Response([])));

        Results<Ok<GetRateEndpoint.RateResponse>, ProblemHttpResult> result =
            await GetRateEndpoint.HandleAsync("24", _client, _aliasCatalog, CancellationToken.None);

        Ok<GetRateEndpoint.RateResponse> ok = Assert.IsType<Ok<GetRateEndpoint.RateResponse>>(result.Result);
        Assert.Equal(0, ok.Value!.Count);
        Assert.Null(ok.Value.Date);
        Assert.Null(ok.Value.Buy);
    }

    [Fact]
    public async Task HandleAsync_UnknownCurrency_ReturnsNotFoundProblem()
    {
        Results<Ok<GetRateEndpoint.RateResponse>, ProblemHttpResult> result =
            await GetRateEndpoint.HandleAsync("not-a-currency", _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Contains("not-a-currency", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_ReturnsProblem()
    {
        CurrencyCode usd = new(2);
        _client.GetCurrentRateAsync(usd, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetCurrentRate.Response>(Error.Failure("Banguat.TransportFailure", "boom")));

        Results<Ok<GetRateEndpoint.RateResponse>, ProblemHttpResult> result =
            await GetRateEndpoint.HandleAsync("usd", _client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("boom", problem.ProblemDetails.Detail);
    }
}