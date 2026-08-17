using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class CurrencyCodeTests
{
    [Fact]
    public void Equals_Should_BeTrueForSameValue()
    {
        var a = new CurrencyCode(2);
        var b = new CurrencyCode(2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_Should_BeFalseForDifferentValue()
    {
        var a = new CurrencyCode(2);
        var b = new CurrencyCode(3);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToString_Should_ReturnUnderlyingValue()
    {
        var code = new CurrencyCode(2);

        Assert.Equal("2", code.ToString());
    }
}
