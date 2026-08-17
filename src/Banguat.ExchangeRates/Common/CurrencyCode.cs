namespace Banguat.ExchangeRates.Common;

public readonly record struct CurrencyCode(int Value)
{
    public override string ToString() => Value.ToString();
}
