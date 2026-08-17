namespace Banguat.ExchangeRates;

public sealed class BanguatExchangeRateClientOptions
{
    public Uri BaseAddress { get; set; } = new("https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx");
}