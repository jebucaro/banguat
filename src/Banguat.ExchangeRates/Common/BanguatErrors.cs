namespace Banguat.ExchangeRates.Common;

public static class BanguatErrors
{
    public static Error TransportFailure(string reason)
    {
        return Error.Failure(
            "Banguat.TransportFailure",
            $"The request to the Banguat exchange rate service failed: {reason}");
    }

    public static Error SoapFault(string faultCode, string faultString)
    {
        return Error.Problem(
            "Banguat.SoapFault",
            $"The Banguat exchange rate service returned a SOAP fault ({faultCode}): {faultString}");
    }

    public static Error UnexpectedResponseShape(string operation)
    {
        return Error.Problem(
            "Banguat.UnexpectedResponseShape",
            $"The response for operation '{operation}' did not have the expected shape.");
    }

    public static Error InvalidDateRange()
    {
        return Error.Validation(
            "Banguat.InvalidDateRange",
            "The end date must not be earlier than the start date.");
    }
}