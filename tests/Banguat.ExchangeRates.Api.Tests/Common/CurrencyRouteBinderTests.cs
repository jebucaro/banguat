using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Tests.Common;

public class CurrencyRouteBinderTests
{
    private readonly ICurrencyAliasCatalog _aliasCatalog = new BundledCurrencyAliasCatalog();

    [Fact]
    public void TryResolve_NumericString_ReturnsTrueAndCurrencyCode()
    {
        bool resolved = CurrencyRouteBinder.TryResolve("24", _aliasCatalog, out CurrencyCode code);

        Assert.True(resolved);
        Assert.Equal(24, code.Value);
    }

    [Fact]
    public void TryResolve_KnownAlias_ReturnsTrueAndCurrencyCode()
    {
        bool resolved = CurrencyRouteBinder.TryResolve("usd", _aliasCatalog, out CurrencyCode code);

        Assert.True(resolved);
        Assert.Equal(2, code.Value);
    }

    [Fact]
    public void TryResolve_UnknownValue_ReturnsFalse()
    {
        bool resolved = CurrencyRouteBinder.TryResolve("not-a-currency", _aliasCatalog, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void UnknownCurrencyProblem_ReturnsNotFoundWithCurrencyInDetail()
    {
        ProblemHttpResult problem = CurrencyRouteBinder.UnknownCurrencyProblem("not-a-currency");

        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Contains("not-a-currency", problem.ProblemDetails.Detail);
    }
}
