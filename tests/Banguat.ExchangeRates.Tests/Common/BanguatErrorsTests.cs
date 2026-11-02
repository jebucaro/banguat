using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class BanguatErrorsTests
{
    [Fact]
    public void Timeout_Should_BeTimeoutTypeWithGenericCopy()
    {
        Error error = BanguatErrors.Timeout();

        Assert.Equal("Banguat.Timeout", error.Code);
        Assert.Equal(ErrorType.Timeout, error.Type);
        Assert.Equal(
            "The Banguat exchange rate service did not respond in time. Try again shortly.", error.Description);
    }

    [Fact]
    public void Unavailable_Should_BeUnavailableTypeWithGenericCopy()
    {
        Error error = BanguatErrors.Unavailable();

        Assert.Equal("Banguat.Unavailable", error.Code);
        Assert.Equal(ErrorType.Unavailable, error.Type);
        Assert.Equal(
            "The Banguat exchange rate service is temporarily unavailable. Try again shortly.", error.Description);
    }

    [Fact]
    public void TransportFailure_Should_BeProblemTypeWithGenericCopy()
    {
        Error error = BanguatErrors.TransportFailure();

        Assert.Equal("Banguat.TransportFailure", error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Equal(
            "The Banguat exchange rate service could not be reached. Try again shortly.", error.Description);
    }

    [Fact]
    public void SoapFault_Should_BeProblemTypeWithGenericCopy()
    {
        Error error = BanguatErrors.SoapFault();

        Assert.Equal("Banguat.SoapFault", error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Equal(
            "The Banguat exchange rate service returned an error. Try again shortly.", error.Description);
    }

    [Fact]
    public void UnexpectedResponseShape_Should_BeProblemTypeWithGenericCopy()
    {
        Error error = BanguatErrors.UnexpectedResponseShape();

        Assert.Equal("Banguat.UnexpectedResponseShape", error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Equal(
            "The Banguat exchange rate service returned an invalid response. Try again shortly.", error.Description);
    }

    [Fact]
    public void IndicatesUnreachable_Should_BeTrue_ForTimeoutUnavailableAndTransportFailure()
    {
        Assert.True(BanguatErrors.IndicatesUnreachable(BanguatErrors.Timeout()));
        Assert.True(BanguatErrors.IndicatesUnreachable(BanguatErrors.Unavailable()));
        Assert.True(BanguatErrors.IndicatesUnreachable(BanguatErrors.TransportFailure()));
    }

    [Fact]
    public void IndicatesUnreachable_Should_BeFalse_ForSoapFaultAndUnexpectedResponseShape()
    {
        Assert.False(BanguatErrors.IndicatesUnreachable(BanguatErrors.SoapFault()));
        Assert.False(BanguatErrors.IndicatesUnreachable(BanguatErrors.UnexpectedResponseShape()));
    }

    [Fact]
    public void InvalidDateRange_Should_BeValidationType()
    {
        Error error = BanguatErrors.InvalidDateRange();

        Assert.Equal("Banguat.InvalidDateRange", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
    }

    [Fact]
    public void UnknownCurrency_Should_BeNotFoundType()
    {
        Error error = BanguatErrors.UnknownCurrency("USSD", "USD");

        Assert.Equal("Banguat.UnknownCurrency", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("Unknown currency 'USSD'. Did you mean: USD?", error.Description);
    }

    [Fact]
    public void UnknownCurrencyCode_Should_ExplainBanguatNumbering()
    {
        Error error = BanguatErrors.UnknownCurrencyCode(840);

        Assert.Equal("Banguat.UnknownCurrency", error.Code);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Contains("'840'", error.Description);
        Assert.Contains("not ISO 4217", error.Description);
    }

    // The description reaches API, MCP and CLI callers and the MCP server's logs, so a multi-megabyte input
    // must not come back verbatim.
    [Fact]
    public void UnknownCurrency_Should_TruncateLongInput()
    {
        Error error = BanguatErrors.UnknownCurrency(new string('z', 1_000), null);

        Assert.Equal("Unknown currency 'zzzzzzzzzzzzzzzz...'.", error.Description);
    }

    [Fact]
    public void UnknownCurrency_Should_ReplaceControlCharacters()
    {
        Error error = BanguatErrors.UnknownCurrency("usd\r\nFAKE\u001b[31m", null);

        Assert.Equal("Unknown currency 'usd??FAKE?[31m'.", error.Description);
    }
}