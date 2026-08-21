using Banguat.ExchangeRates.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Common;

public static class CurrencyRouteBinder
{
    public static bool TryResolve(string currency, ICurrencyAliasCatalog aliasCatalog, out CurrencyCode code)
    {
        if (int.TryParse(currency, out int numeric))
        {
            code = new CurrencyCode(numeric);
            return true;
        }

        return aliasCatalog.TryResolve(currency, out code);
    }

    public static ProblemHttpResult UnknownCurrencyProblem(string currency)
    {
        return TypedResults.Problem(
            $"Unknown currency '{currency}'. Call GET /currencies to see valid codes and aliases.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Unknown currency");
    }
}