using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class BundledCurrencyAliasCatalogTests
{
    private readonly BundledCurrencyAliasCatalog _catalog = new();

    [Fact]
    public void TryResolve_WhenAliasKnown_ReturnsTrueAndCode()
    {
        bool resolved = _catalog.TryResolve("USD", out CurrencyCode code);

        Assert.True(resolved);
        Assert.Equal(new CurrencyCode(2), code);
    }

    [Fact]
    public void TryResolve_IsCaseInsensitive()
    {
        bool resolved = _catalog.TryResolve("usd", out CurrencyCode code);

        Assert.True(resolved);
        Assert.Equal(new CurrencyCode(2), code);
    }

    [Fact]
    public void TryResolve_WhenAliasUnknown_ReturnsFalse()
    {
        bool resolved = _catalog.TryResolve("ZZZ", out _);

        Assert.False(resolved);
    }

    [Fact]
    public void GetAlias_WhenCodeKnown_ReturnsItsAlias()
    {
        string? alias = _catalog.GetAlias(new CurrencyCode(2));

        Assert.Equal("USD", alias);
    }

    [Fact]
    public void GetAlias_WhenCodeUnknown_ReturnsNull()
    {
        string? alias = _catalog.GetAlias(new CurrencyCode(999));

        Assert.Null(alias);
    }

    [Fact]
    public void AllAliases_ContainsBundledTokens()
    {
        Assert.Contains("USD", _catalog.AllAliases);
        Assert.Contains("GTQ", _catalog.AllAliases);
    }

    [Fact]
    public void BuildCodeToAliasIndex_WhenTwoAliasesShareACode_ThrowsInvalidOperationException()
    {
        Dictionary<string, CurrencyCode> aliasToCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new CurrencyCode(2),
            ["DOLLAR"] = new CurrencyCode(2)
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => BundledCurrencyAliasCatalog.BuildCodeToAliasIndex(aliasToCode));

        Assert.Contains("2", exception.Message);
    }
}
