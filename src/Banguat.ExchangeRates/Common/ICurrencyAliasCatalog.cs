namespace Banguat.ExchangeRates.Common;

public interface ICurrencyAliasCatalog
{
    bool TryResolve(string alias, out CurrencyCode code);

    string? GetAlias(CurrencyCode code);

    IReadOnlyList<string> AllAliases { get; }
}