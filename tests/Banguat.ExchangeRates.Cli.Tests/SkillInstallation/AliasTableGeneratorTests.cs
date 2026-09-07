using Banguat.ExchangeRates.Cli.SkillInstallation;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Tests.SkillInstallation;

public class AliasTableGeneratorTests
{
    [Fact]
    public void Generate_UsesTheStandardKnownCurrenciesRelativePath()
    {
        SkillAssetFile file = AliasTableGenerator.Generate(new BundledCurrencyAliasCatalog());

        Assert.Equal("known-currencies.md", file.RelativePath);
    }

    [Fact]
    public void Generate_ListsEveryBundledAliasWithItsNumericCode()
    {
        SkillAssetFile file = AliasTableGenerator.Generate(new BundledCurrencyAliasCatalog());

        Assert.Contains("| USD | 2 |", file.Content);
        Assert.Contains("| GTQ | 1 |", file.Content);
        Assert.Contains("| VES | 41 |", file.Content);
    }

    [Fact]
    public void Generate_ProducesExactlyOneRowPerBundledAlias()
    {
        BundledCurrencyAliasCatalog catalog = new();

        SkillAssetFile file = AliasTableGenerator.Generate(catalog);

        int rowCount = file.Content.Split('\n').Count(line => line.StartsWith('|') && !line.Contains("---"));
        Assert.Equal(catalog.AllAliases.Count + 1, rowCount); // +1 for the header row
    }
}
