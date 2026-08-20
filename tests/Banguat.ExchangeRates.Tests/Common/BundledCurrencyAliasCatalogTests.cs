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
    public void GetAliases_WhenCodeKnown_ReturnsItsAliases()
    {
        IReadOnlyList<string> aliases = _catalog.GetAliases(new CurrencyCode(2));

        Assert.Contains("USD", aliases);
    }

    [Fact]
    public void GetAliases_WhenCodeUnknown_ReturnsEmpty()
    {
        IReadOnlyList<string> aliases = _catalog.GetAliases(new CurrencyCode(999));

        Assert.Empty(aliases);
    }

    [Fact]
    public void AllAliases_ContainsBundledTokens()
    {
        Assert.Contains("USD", _catalog.AllAliases);
        Assert.Contains("GTQ", _catalog.AllAliases);
    }
}