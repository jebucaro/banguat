using System.Reflection;
using System.Text.Json;

namespace Banguat.ExchangeRates.Common;

public sealed class BundledCurrencyAliasCatalog : ICurrencyAliasCatalog
{
    private readonly Dictionary<string, CurrencyCode> _aliasToCode;
    private readonly Dictionary<CurrencyCode, string> _codeToAlias;

    public BundledCurrencyAliasCatalog()
    {
        Assembly assembly = typeof(BundledCurrencyAliasCatalog).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("CurrencyAliases.json", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        Dictionary<string, int> raw = JsonSerializer.Deserialize<Dictionary<string, int>>(stream)!;

        _aliasToCode = new Dictionary<string, CurrencyCode>(StringComparer.OrdinalIgnoreCase);
        foreach ((string alias, int value) in raw)
        {
            _aliasToCode[alias] = new CurrencyCode(value);
        }

        _codeToAlias = BuildCodeToAliasIndex(_aliasToCode);
    }

    internal static Dictionary<CurrencyCode, string> BuildCodeToAliasIndex(
        IReadOnlyDictionary<string, CurrencyCode> aliasToCode)
    {
        Dictionary<CurrencyCode, string> codeToAlias = [];

        foreach ((string alias, CurrencyCode code) in aliasToCode)
        {
            if (codeToAlias.TryGetValue(code, out string? existingAlias))
            {
                throw new InvalidOperationException(
                    $"Currency code {code.Value} has more than one alias: '{existingAlias}' and '{alias}'. " +
                    "Each currency must have exactly one alias.");
            }

            codeToAlias[code] = alias;
        }

        return codeToAlias;
    }

    public bool TryResolve(string alias, out CurrencyCode code)
    {
        return _aliasToCode.TryGetValue(alias, out code);
    }

    public string? GetAlias(CurrencyCode code)
    {
        return _codeToAlias.TryGetValue(code, out string? alias) ? alias : null;
    }

    public IReadOnlyList<string> AllAliases => _aliasToCode.Keys.ToList();
}
