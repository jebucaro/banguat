using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class BanguatErrorsTests
{
    [Fact]
    public void TransportFailure_Should_BeFailureType()
    {
        var error = BanguatErrors.TransportFailure("connection reset");

        Assert.Equal("Banguat.TransportFailure", error.Code);
        Assert.Equal(ErrorType.Failure, error.Type);
        Assert.Contains("connection reset", error.Description);
    }

    [Fact]
    public void SoapFault_Should_BeProblemType()
    {
        var error = BanguatErrors.SoapFault("soap:Server", "bad request");

        Assert.Equal("Banguat.SoapFault", error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Contains("soap:Server", error.Description);
        Assert.Contains("bad request", error.Description);
    }

    [Fact]
    public void UnexpectedResponseShape_Should_BeProblemType()
    {
        var error = BanguatErrors.UnexpectedResponseShape("TipoCambioDia");

        Assert.Equal("Banguat.UnexpectedResponseShape", error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Contains("TipoCambioDia", error.Description);
    }

    [Fact]
    public void InvalidDateRange_Should_BeValidationType()
    {
        var error = BanguatErrors.InvalidDateRange();

        Assert.Equal("Banguat.InvalidDateRange", error.Code);
        Assert.Equal(ErrorType.Validation, error.Type);
    }
}