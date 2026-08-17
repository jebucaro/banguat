using System.Xml.Linq;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Tests.Soap.Models;

public class SoapVarTests
{
    [Fact]
    public void FromElement_Should_MapAllFields()
    {
        var element = XElement.Parse(
            """<Var xmlns="http://www.banguat.gob.gt/variables/ws/"><moneda>2</moneda><fecha>17/08/2026</fecha><venta>7.62484</venta><compra>7.6188</compra></Var>""");

        var result = SoapVar.FromElement(element);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 8, 17), result.Fecha);
        Assert.Equal(7.62484m, result.Venta);
        Assert.Equal(7.6188m, result.Compra);
    }

    [Fact]
    public void FromElement_Should_ReturnNullWhenFieldMissing()
    {
        var element = XElement.Parse(
            """<Var xmlns="http://www.banguat.gob.gt/variables/ws/"><moneda>2</moneda><fecha>17/08/2026</fecha></Var>""");

        Assert.Null(SoapVar.FromElement(element));
    }
}