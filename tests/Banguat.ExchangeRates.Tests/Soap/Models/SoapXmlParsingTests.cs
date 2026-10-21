using System.Xml.Linq;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Tests.Soap.Models;

public class SoapXmlParsingTests
{
    [Fact]
    public void ParseDate_Should_ParseValidDdMmYyyy()
    {
        DateOnly? result = SoapXmlParsing.ParseDate("17/08/2026");

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
        decimal? result = SoapXmlParsing.ParseDecimal("7.61992");

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
        IReadOnlyList<string>? result = SoapXmlParsing.ParseList<string>(
            null, BanguatSoapNamespaces.Service + "Item", _ => "x");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseList_Should_ReturnNull_WhenAnyItemFailsToParse()
    {
        XElement container = new(
            BanguatSoapNamespaces.Service + "Container",
            new XElement(BanguatSoapNamespaces.Service + "Item", "1"),
            new XElement(BanguatSoapNamespaces.Service + "Item", "skip"),
            new XElement(BanguatSoapNamespaces.Service + "Item", "3"));

        IReadOnlyList<int?>? result = SoapXmlParsing.ParseList<int?>(
            container,
            BanguatSoapNamespaces.Service + "Item",
            element => int.TryParse(element.Value, out int value) ? (int?)value : null);

        Assert.Null(result);
    }

    [Fact]
    public void ParseList_Should_MapEachItem_WhenAllParse()
    {
        XElement container = new(
            BanguatSoapNamespaces.Service + "Container",
            new XElement(BanguatSoapNamespaces.Service + "Item", "1"),
            new XElement(BanguatSoapNamespaces.Service + "Item", "3"));

        IReadOnlyList<int?>? result = SoapXmlParsing.ParseList<int?>(
            container,
            BanguatSoapNamespaces.Service + "Item",
            element => int.TryParse(element.Value, out int value) ? (int?)value : null);

        Assert.Equal([1, 3], result);
    }

    [Fact]
    public void ParseList_Should_ReturnEmpty_WhenTotalItemsIsZero()
    {
        XElement result = XElement.Parse(
            """<Result xmlns="http://www.banguat.gob.gt/variables/ws/"><Vars /><TotalItems>0</TotalItems></Result>""");

        IReadOnlyList<string>? items = SoapXmlParsing.ParseList<string>(
            result.Element(BanguatSoapNamespaces.Service + "Vars"), BanguatSoapNamespaces.Service + "Var", _ => "x");

        Assert.NotNull(items);
        Assert.Empty(items);
        Assert.True(SoapXmlParsing.CheckTotalItems(result, items.Count));
    }

    [Fact]
    public void CheckTotalItems_Should_Pass_WhenElementAbsent()
    {
        XElement result = XElement.Parse("""<Result xmlns="http://www.banguat.gob.gt/variables/ws/" />""");

        Assert.True(SoapXmlParsing.CheckTotalItems(result, 3));
    }

    [Fact]
    public void CheckTotalItems_Should_Fail_WhenCountDiffers()
    {
        XElement result = XElement.Parse(
            """<Result xmlns="http://www.banguat.gob.gt/variables/ws/"><TotalItems>2</TotalItems></Result>""");

        Assert.False(SoapXmlParsing.CheckTotalItems(result, 1));
    }

    [Fact]
    public void CheckTotalItems_Should_Fail_WhenValueIsNotANumber()
    {
        XElement result = XElement.Parse(
            """<Result xmlns="http://www.banguat.gob.gt/variables/ws/"><TotalItems>many</TotalItems></Result>""");

        Assert.False(SoapXmlParsing.CheckTotalItems(result, 1));
    }
}