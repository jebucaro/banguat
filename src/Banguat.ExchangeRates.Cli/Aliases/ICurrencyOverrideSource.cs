using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Aliases;

public interface ICurrencyOverrideSource
{
    IReadOnlyDictionary<string, CurrencyCode> Load();
}
