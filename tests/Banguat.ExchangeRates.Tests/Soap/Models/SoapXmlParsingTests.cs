using System.Xml.Linq;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Tests.Soap.Models;

public class SoapXmlParsingTests
{
    [Fact]
    public void ParseDate_Should_ParseValidDdMmYyyy()
    {
        var result = SoapXmlParsing.ParseDate("17/08/2026");

        Assert.Equal(new DateOnly(2026, 8, 17), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void ParseDate_Should_ReturnNullForInvalidInput(string? value)
    {
        Assert.Null(SoapXmlParsing.ParseDate(value));
    }

    [Fact]
    public void ParseDecimal_Should_ParseInvariantCultureValue()
    {
        var result = SoapXmlParsing.ParseDecimal("7.61992");

        Assert.Equal(7.61992m, result);
    }

    [Fact]
    public void ParseDecimal_Should_ReturnNullForInvalidInput()
    {
        Assert.Null(SoapXmlParsing.ParseDecimal("Error"));
    }

    [Fact]
    public void ParseInt_Should_ParseValidInteger()
    {
        Assert.Equal(2, SoapXmlParsing.ParseInt("2"));
    }

    [Fact]
    public void ParseInt_Should_ReturnNullForInvalidInput()
    {
        Assert.Null(SoapXmlParsing.ParseInt("abc"));
    }

    [Fact]
    public void ParseList_Should_ReturnEmptyWhenContainerIsNull()
    {
        var result = SoapXmlParsing.ParseList<string>(
            null, BanguatSoapNamespaces.Service + "Item", _ => "x");

        Assert.Empty(result);
    }

    [Fact]
    public void ParseList_Should_MapEachItemAndSkipNulls()
    {
        var container = new XElement(
            BanguatSoapNamespaces.Service + "Container",
            new XElement(BanguatSoapNamespaces.Service + "Item", "1"),
            new XElement(BanguatSoapNamespaces.Service + "Item", "skip"),
            new XElement(BanguatSoapNamespaces.Service + "Item", "3"));

        var result = SoapXmlParsing.ParseList<int?>(
            container,
            BanguatSoapNamespaces.Service + "Item",
            element => int.TryParse(element.Value, out var value) ? (int?)value : null);

        Assert.Equal([1, 3], result);
    }
}