using Banguat.ExchangeRates.Cli.Aliases;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Tests.Aliases;

internal sealed class StubCurrencyOverrideSource(IReadOnlyDictionary<string, CurrencyCode>? overrides = null)
    : ICurrencyOverrideSource
{
    private readonly IReadOnlyDictionary<string, CurrencyCode> _overrides =
        overrides ?? new Dictionary<string, CurrencyCode>();

    public IReadOnlyDictionary<string, CurrencyCode> Load()
    {
        return _overrides;
    }
}

internal sealed class ThrowingCurrencyOverrideSource(string message) : ICurrencyOverrideSource
{
    public IReadOnlyDictionary<string, CurrencyCode> Load()
    {
        throw new CurrencyOverrideLoadException(message);
    }
}