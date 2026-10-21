using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using NSubstitute;

namespace Banguat.ExchangeRates.Tests;

public class CurrencyResolverTests
{
    private readonly IBanguatExchangeRateClient _client = Substitute.For<IBanguatExchangeRateClient>();
    private readonly CurrencyResolver _resolver;

    public CurrencyResolverTests()
    {
        _resolver = new CurrencyResolver(_client, new BundledCurrencyAliasCatalog());
    }

    private void GivenAvailableCurrencies(params int[] codes)
    {
        GetAvailableCurrencies.Response response = new(codes
            .Select(code => new GetAvailableCurrencies.CurrencyCatalogEntry(new CurrencyCode(code), $"Currency {code}"))
            .ToList());
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(response));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    [InlineData("Usd")]
    public async Task ResolveAsync_Should_ReturnCode_WhenAliasKnown(string currency)
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync(currency);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CurrencyCode(2), result.Value);
        await _client.DidNotReceive().GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Should_SkipLiveLookup_WhenNumericCodeHasBundledAlias()
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync("24");

        Assert.True(result.IsSuccess);
        Assert.Equal(new CurrencyCode(24), result.Value);
        await _client.DidNotReceive().GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnCode_WhenNumericCodeIsInLiveCatalog()
    {
        GivenAvailableCurrencies(2, 24, 99);

        Result<CurrencyCode> result = await _resolver.ResolveAsync("99");

        Assert.True(result.IsSuccess);
        Assert.Equal(new CurrencyCode(99), result.Value);
    }

    [Fact]
    public async Task ResolveAsync_Should_FailWithIsoHint_WhenNumericCodeIsNotInLiveCatalog()
    {
        GivenAvailableCurrencies(2, 24);

        Result<CurrencyCode> result = await _resolver.ResolveAsync("840");

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnknownCurrency", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Contains("'840'", result.Error.Description);
        Assert.Contains("ISO 4217", result.Error.Description);
    }

    [Fact]
    public async Task ResolveAsync_Should_FailWithoutLiveLookup_WhenCodeIsZero()
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync("0");

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnknownCurrency", result.Error.Code);
        await _client.DidNotReceive().GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnUpstreamError_WhenCatalogTimesOut()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(
            Result.Failure<GetAvailableCurrencies.Response>(BanguatErrors.Timeout()));

        Result<CurrencyCode> result = await _resolver.ResolveAsync("99");

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.Timeout", result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnUpstreamError_WhenCatalogUnreachable()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(
            Result.Failure<GetAvailableCurrencies.Response>(BanguatErrors.TransportFailure()));

        Result<CurrencyCode> result = await _resolver.ResolveAsync("99");

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_Should_FailOpen_WhenCatalogReturnsSoapFault()
    {
        _client.GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(
            Result.Failure<GetAvailableCurrencies.Response>(BanguatErrors.SoapFault()));

        Result<CurrencyCode> result = await _resolver.ResolveAsync("99");

        Assert.True(result.IsSuccess);
        Assert.Equal(new CurrencyCode(99), result.Value);
    }

    [Theory]
    [InlineData("-2")]
    [InlineData("+2")]
    [InlineData(" 2")]
    [InlineData("2 ")]
    [InlineData("2.0")]
    [InlineData("99999999999")]
    [InlineData("")]
    public async Task ResolveAsync_Should_Fail_WhenValueIsNotStrictDigits(string currency)
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync(currency);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnknownCurrency", result.Error.Code);
        await _client.DidNotReceive().GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Should_SuggestNearestAlias_WhenAliasIsMistyped()
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync("USSD");

        Assert.True(result.IsFailure);
        Assert.Equal("Unknown currency 'USSD'. Did you mean: USD?", result.Error.Description);
    }

    [Fact]
    public async Task ResolveAsync_Should_NotSuggest_WhenNoAliasIsClose()
    {
        Result<CurrencyCode> result = await _resolver.ResolveAsync("not-a-currency");

        Assert.True(result.IsFailure);
        Assert.Equal("Unknown currency 'not-a-currency'.", result.Error.Description);
    }

    [Fact]
    public async Task ResolveAsync_Should_PassCancellationTokenToLiveLookup()
    {
        GivenAvailableCurrencies(99);
        using CancellationTokenSource cts = new();

        await _resolver.ResolveAsync("99", cts.Token);

        await _client.Received(1).GetAvailableCurrenciesAsync(cts.Token);
    }

    // Every alias is three letters, so nothing this long can be an alias, a Banguat code or close enough
    // for a suggestion. Rejecting it before the catalog keeps a huge input from costing a fuzzy match per alias.
    [Fact]
    public async Task ResolveAsync_Should_RejectOverlongInput_WithoutTouchingTheCatalog()
    {
        ICurrencyAliasCatalog catalog = Substitute.For<ICurrencyAliasCatalog>();
        CurrencyResolver resolver = new(_client, catalog);

        Result<CurrencyCode> result = await resolver.ResolveAsync(new string('U', 1_000_000));

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnknownCurrency", result.Error.Code);
        Assert.True(result.Error.Description.Length < 100);
        Assert.Empty(catalog.ReceivedCalls());
        await _client.DidNotReceive().GetAvailableCurrenciesAsync(Arg.Any<CancellationToken>());
    }
}
