using System.Xml.Linq;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Tests.Soap.Models;

public class SoapVariableTests
{
    [Fact]
    public void FromElement_Should_MapAllFields()
    {
        var element = XElement.Parse(
            """<Variable xmlns="http://www.banguat.gob.gt/variables/ws/"><moneda>2</moneda><descripcion>Dólares de EE.UU.</descripcion></Variable>""");

        SoapVariable? result = SoapVariable.FromElement(element);

        Assert.NotNull(result);
        Assert.Equal(2, result.Moneda);
        Assert.Equal("Dólares de EE.UU.", result.Descripcion);
    }

    [Fact]
    public void FromElement_Should_ReturnNullWhenMonedaMissing()
    {
        var element = XElement.Parse(
            """<Variable xmlns="http://www.banguat.gob.gt/variables/ws/"><descripcion>Dólares de EE.UU.</descripcion></Variable>""");

        Assert.Null(SoapVariable.FromElement(element));
    }
}
