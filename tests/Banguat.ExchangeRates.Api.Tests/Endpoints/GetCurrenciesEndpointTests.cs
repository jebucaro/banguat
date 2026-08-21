using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Endpoints;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace Banguat.ExchangeRates.Api.Tests.Endpoints;

public class GetCurrenciesEndpointTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public async Task HandleAsync_ReturnsCurrenciesWithAliasesAndCount()
    {
        GetAvailableCurrencies.Response response = new(
        [
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(2), "Dólares de EE.UU."),
            new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(1), "Quetzales")
        ]);
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(response));

        Results<Ok<GetCurrenciesEndpoint.CurrenciesResponse>, ProblemHttpResult> result =
            await GetCurrenciesEndpoint.HandleAsync(_client, _aliasCatalog, CancellationToken.None);

        Ok<GetCurrenciesEndpoint.CurrenciesResponse> ok =
            Assert.IsType<Ok<GetCurrenciesEndpoint.CurrenciesResponse>>(result.Result);
        Assert.Equal(2, ok.Value!.Count);
        GetCurrenciesEndpoint.CurrencyEntry usd = Assert.Single(ok.Value.Currencies, c => c.Code == 2);
        Assert.Contains("USD", usd.Aliases, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_TransportFailure_ReturnsProblem()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GetAvailableCurrencies.Response>(
                Error.Failure("Banguat.TransportFailure", "boom")));

        Results<Ok<GetCurrenciesEndpoint.CurrenciesResponse>, ProblemHttpResult> result =
            await GetCurrenciesEndpoint.HandleAsync(_client, _aliasCatalog, CancellationToken.None);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
        Assert.Equal("boom", problem.ProblemDetails.Detail);
    }
}