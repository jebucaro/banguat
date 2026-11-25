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
    public void ToProblem_TimeoutError_Returns504()
    {
        Error error = BanguatErrors.Timeout();

        ProblemHttpResult problem = error.ToProblem();

        Assert.Equal(StatusCodes.Status504GatewayTimeout, problem.StatusCode);
        Assert.Equal(error.Description, problem.ProblemDetails.Detail);
        Assert.Equal(error.Code, problem.ProblemDetails.Title);
    }

    [Fact]
    public void ToProblem_UnavailableError_Returns503()
    {
        Error error = BanguatErrors.Unavailable();

        ProblemHttpResult problem = error.ToProblem();

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal(error.Description, problem.ProblemDetails.Detail);
        Assert.Equal(error.Code, problem.ProblemDetails.Title);
    }

    [Fact]
    public void ToProblem_ProblemError_Returns502()
    {
        Error error = BanguatErrors.TransportFailure();

        ProblemHttpResult problem = error.ToProblem();

        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal(error.Description, problem.ProblemDetails.Detail);
        Assert.Equal(error.Code, problem.ProblemDetails.Title);
    }

    [Fact]
    public void ToProblem_FailureError_ReturnsInternalServerError()
    {
        ProblemHttpResult problem = Error.Failure("Some.Failure", "boom").ToProblem();

        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }
}