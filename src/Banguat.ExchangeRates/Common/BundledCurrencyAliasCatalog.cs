using System.Reflection;
using System.Text.Json;

namespace Banguat.ExchangeRates.Common;

public sealed class BundledCurrencyAliasCatalog : ICurrencyAliasCatalog
{
    private readonly Dictionary<string, CurrencyCode> _aliasToCode;
    private readonly Dictionary<CurrencyCode, List<string>> _codeToAliases;

    public BundledCurrencyAliasCatalog()
    {
        Assembly assembly = typeof(BundledCurrencyAliasCatalog).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("CurrencyAliases.json", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        Dictionary<string, int> raw = JsonSerializer.Deserialize<Dictionary<string, int>>(stream)!;

        _aliasToCode = new Dictionary<string, CurrencyCode>(StringComparer.OrdinalIgnoreCase);
        _codeToAliases = [];

        foreach ((string alias, int value) in raw)
        {
            CurrencyCode code = new(value);
            _aliasToCode[alias] = code;

            if (!_codeToAliases.TryGetValue(code, out List<string>? aliases))
            {
                aliases = [];
                _codeToAliases[code] = aliases;
            }

            aliases.Add(alias);
        }
    }

    public bool TryResolve(string alias, out CurrencyCode code)
    {
        return _aliasToCode.TryGetValue(alias, out code);
    }

    public IReadOnlyList<string> GetAliases(CurrencyCode code)
    {
        return _codeToAliases.TryGetValue(code, out List<string>? aliases) ? aliases.AsReadOnly() : [];
    }

    public IReadOnlyList<string> AllAliases => _aliasToCode.Keys.ToList();
}