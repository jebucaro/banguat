namespace Banguat.ExchangeRates;

public sealed class BanguatExchangeRateClientOptions
{
    public const string SectionName = "Banguat";

    public Uri BaseAddress { get; set; } = new("https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx");

    // How long a caller waits on Banguat at most; a hung service fails with a timeout error after this.
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(10);
}