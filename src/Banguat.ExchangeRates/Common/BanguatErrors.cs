using System.Globalization;

namespace Banguat.ExchangeRates.Common;

public static class BanguatErrors
{
    private const string TransportFailureCode = "Banguat.TransportFailure";

    private const int MaxEchoLength = 16;

    // Descriptions reach API, MCP and CLI callers verbatim, so they never carry hostnames, exception messages
    // or SOAP fault text; the raw reason is logged and traced by the transport instead.
    public static Error Timeout()
    {
        return Error.Timeout(
            "Banguat.Timeout",
            "The Banguat exchange rate service did not respond in time. Try again shortly.");
    }

    public static Error Unavailable()
    {
        return Error.Unavailable(
            "Banguat.Unavailable",
            "The Banguat exchange rate service is temporarily unavailable. Try again shortly.");
    }

    public static Error TransportFailure()
    {
        return Error.Problem(
            TransportFailureCode,
            "The Banguat exchange rate service could not be reached. Try again shortly.");
    }

    public static Error SoapFault()
    {
        return Error.Problem(
            "Banguat.SoapFault",
            "The Banguat exchange rate service returned an error. Try again shortly.");
    }

    public static Error UnexpectedResponseShape()
    {
        return Error.Problem(
            "Banguat.UnexpectedResponseShape",
            "The Banguat exchange rate service returned an invalid response. Try again shortly.");
    }

    /// <summary>
    /// True when the error means Banguat could not be reached or did not answer, as opposed to answering
    /// with something unusable.
    /// </summary>
    internal static bool IndicatesUnreachable(Error error)
    {
        return error.Type is ErrorType.Timeout or ErrorType.Unavailable || error.Code == TransportFailureCode;
    }

    public static Error InvalidDateRange()
    {
        return Error.Validation(
            "Banguat.InvalidDateRange",
            "The end date must not be earlier than the start date.");
    }

    public static Error DateBeforeEarliest(DateOnly earliest)
    {
        return Error.Validation(
            "Banguat.DateBeforeEarliest",
            $"Banguat has no rates before {earliest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.");
    }

    public static Error DateInFuture()
    {
        return Error.Validation(
            "Banguat.DateInFuture",
            "Dates must not be later than today in Guatemala.");
    }

    public static Error DateRangeTooLong(int maxDays)
    {
        return Error.Validation(
            "Banguat.DateRangeTooLong",
            $"A history query may cover at most {maxDays} days. Split longer periods into several queries.");
    }

    public static Error InvalidHistoryRange()
    {
        return Error.Validation(
            "Banguat.InvalidHistoryRange",
            "Provide either a since date, or both from and to dates (not both, not neither).");
    }

    public static Error UnknownCurrency(string currency, string? suggestion)
    {
        return Error.NotFound(
            "Banguat.UnknownCurrency",
            suggestion is null
                ? $"Unknown currency '{Echo(currency)}'."
                : $"Unknown currency '{Echo(currency)}'. Did you mean: {suggestion}?");
    }

    // Caller input is echoed back to API, MCP and CLI callers and lands in logs, so it is cut short and stripped
    // of control characters that could fake log lines or drive a terminal.
    private static string Echo(string value)
    {
        string shown = value.Length > MaxEchoLength ? value[..MaxEchoLength] + "..." : value;

        return string.Concat(shown.Select(c => char.IsControl(c) ? '?' : c));
    }

    public static Error UnknownCurrencyCode(int code)
    {
        return Error.NotFound(
            "Banguat.UnknownCurrency",
            $"Unknown currency '{code}'. Banguat uses its own numeric codes, not ISO 4217.");
    }
}