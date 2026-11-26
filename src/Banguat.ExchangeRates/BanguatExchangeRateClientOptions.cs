namespace Banguat.ExchangeRates;

public sealed class BanguatExchangeRateClientOptions
{
    public const string SectionName = "Banguat";

    public Uri BaseAddress { get; set; } = new("https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx");

    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(10);
}