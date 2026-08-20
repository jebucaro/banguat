namespace Banguat.ExchangeRates.Common;

public interface ICurrencyAliasCatalog
{
    bool TryResolve(string alias, out CurrencyCode code);

    IReadOnlyList<string> GetAliases(CurrencyCode code);

    IReadOnlyList<string> AllAliases { get; }
}