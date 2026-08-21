using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Tests.Common;

public class ResultExtensionsTests
{
    [Fact]
    public void ToProblem_ValidationError_ReturnsBadRequest()
    {
        ProblemHttpResult problem = Error.Validation("Banguat.InvalidDateRange", "bad range").ToProblem();

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("bad range", problem.ProblemDetails.Detail);
    }

    [Fact]
    public void ToProblem_ProblemError_ReturnsBadGateway()
    {
        ProblemHttpResult problem = Error.Problem("Banguat.SoapFault", "fault").ToProblem();

        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
    }

    [Fact]
    public void ToProblem_FailureError_ReturnsInternalServerError()
    {
        ProblemHttpResult problem = Error.Failure("Banguat.TransportFailure", "boom").ToProblem();

        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }
}
