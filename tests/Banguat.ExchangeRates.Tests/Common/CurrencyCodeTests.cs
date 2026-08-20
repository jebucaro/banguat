using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Tests.Common;

public class CurrencyCodeTests
{
    [Fact]
    public void Equals_Should_BeTrueForSameValue()
    {
        CurrencyCode a = new(2);
        CurrencyCode b = new(2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_Should_BeFalseForDifferentValue()
    {
        CurrencyCode a = new(2);
        CurrencyCode b = new(3);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToString_Should_ReturnUnderlyingValue()
    {
        CurrencyCode code = new(2);

        Assert.Equal("2", code.ToString());
    }
}