using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Aliases;

public interface ICurrencyOverrideSource
{
    /// <summary>Loads the override map. Implementations are not required to use a case-insensitive comparer — callers must normalize.</summary>
    IReadOnlyDictionary<string, CurrencyCode> Load();
}
