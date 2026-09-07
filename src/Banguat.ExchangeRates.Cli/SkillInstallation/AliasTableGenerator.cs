using System.Text;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.SkillInstallation;

public static class AliasTableGenerator
{
    public const string RelativePath = "known-currencies.md";

    public static SkillAssetFile Generate(ICurrencyAliasCatalog catalog)
    {
        IOrderedEnumerable<string> aliases = catalog.AllAliases.OrderBy(alias => alias, StringComparer.Ordinal);

        StringBuilder content = new();
        content.AppendLine("# Known currency aliases");
        content.AppendLine();
        content.AppendLine(
            "Generated from this CLI build's bundled alias catalog - regenerated every time `skill install` " +
            "runs, so this is exactly what `--currency <alias>` resolves to internally. Treat every row below " +
            "as certain: there is no need to cross-check an alias listed here against `currencies` before " +
            "using it. If a currency isn't listed here, it has no bundled alias; run `currencies` to find its " +
            "numeric code instead.");
        content.AppendLine();
        content.AppendLine("| Alias | Code |");
        content.AppendLine("|---|---|");

        foreach (string alias in aliases)
        {
            catalog.TryResolve(alias, out CurrencyCode code);
            content.AppendLine($"| {alias} | {code.Value} |");
        }

        return new SkillAssetFile(RelativePath, content.ToString());
    }
}
