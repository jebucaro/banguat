using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Banguat.ExchangeRates.Diagnostics;

public static class BanguatExchangeRatesDiagnostics
{
    public const string ActivitySourceName = "Banguat.ExchangeRates";

    public const string MeterName = "Banguat.ExchangeRates";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> CallCount = Meter.CreateCounter<long>(
        "banguat.exchangerates.calls",
        "{call}",
        "Number of Banguat exchange rate SOAP operations invoked.");

    internal static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        "banguat.exchangerates.call.duration",
        "ms",
        "Duration of Banguat exchange rate SOAP operations.");
}