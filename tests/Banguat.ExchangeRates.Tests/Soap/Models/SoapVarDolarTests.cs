using System.Xml.Linq;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Tests.Soap.Models;

public class SoapVarDolarTests
{
    [Fact]
    public void FromElement_Should_MapAllFields()
    {
        var element = XElement.Parse(
            """<VarDolar xmlns="http://www.banguat.gob.gt/variables/ws/"><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar>""");

        SoapVarDolar? result = SoapVarDolar.FromElement(element);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 8, 17), result.Fecha);
        Assert.Equal(7.61992m, result.Referencia);
    }

    [Fact]
    public void FromElement_Should_ReturnNullWhenFieldMissing()
    {
        var element = XElement.Parse(
            """<VarDolar xmlns="http://www.banguat.gob.gt/variables/ws/"><fecha>17/08/2026</fecha></VarDolar>""");

        Assert.Null(SoapVarDolar.FromElement(element));
    }
}
