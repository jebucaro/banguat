using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Common;

public static class ResultExtensions
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        int statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Problem => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

        return TypedResults.Problem(error.Description, statusCode: statusCode, title: error.Code);
    }
}