using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetUsdRateHistoryTests
{
    // 2026-09-23T01:00Z is 19:00 on 2026-09-22 in Guatemala: the UTC date has already rolled over while
    // the Guatemala day still has five hours left. Pinning this instant keeps the expected values explicit.
    private static readonly IRateCalendar Calendar =
        new GuatemalaRateCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero)));
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoResult><Vars><Var><moneda>2</moneda><fecha>01/08/2026</fecha><venta>7.6350</venta><compra>7.6231</compra></Var><Var><moneda>2</moneda><fecha>05/08/2026</fecha><venta>7.62541</venta><compra>7.62484</compra></Var></Vars><TotalItems>2</TotalItems></TipoCambioRangoResult></TipoCambioRangoResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetUsdRateHistory.Handler handler = new(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rates.Count);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Value.Rates[0].Date);
        Assert.Equal(7.6231m, result.Value.Rates[0].Buy);
        Assert.Equal(7.6350m, result.Value.Rates[0].Sell);
        Assert.Equal(new DateOnly(2026, 8, 5), result.Value.Rates[1].Date);
        Assert.Equal(7.62484m, result.Value.Rates[1].Buy);
        Assert.Equal(7.62541m, result.Value.Rates[1].Sell);
        Assert.Equal("TipoCambioRango", transport.LastOperationName);
        Assert.Equal(
            "01/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
        Assert.Equal(
            "05/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechafin")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidDateRange_WhenToIsBeforeFrom()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetUsdRateHistory.Handler handler = new(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidDateRange", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetUsdRateHistory.Handler handler = new(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        FakeBanguatSoapTransport transport = new(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        GetUsdRateHistory.Handler handler = new(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public void GetCacheDuration_Should_ReturnStableHistory_WhenRangeEndedBeforeToday()
    {
        GetUsdRateHistory.Query query = new(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 5));

        Assert.Equal(CacheDurationDefaults.StableHistory, query.GetCacheDuration(Calendar));
    }

    // Regression guard: the boundary used to be evaluated with DateTime.UtcNow, so during the final six
    // hours of a Guatemala day a range ending today was misread as closed history and cached for 24h.
    [Fact]
    public void GetCacheDuration_Should_ExpireAtNextGuatemalaDay_WhenRangeEndsToday()
    {
        GetUsdRateHistory.Query query = new(new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 22));

        // At this instant the UTC date is already the 23rd while Guatemala is still on the 22nd, so the
        // two boundaries disagree and the old comparison took the wrong branch.
        DateOnly utcToday = DateOnly.FromDateTime(new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc));
        Assert.True(query.To < utcToday, "the superseded UTC check classified this range as closed history");
        Assert.False(query.To < Calendar.Today, "the Guatemala calendar sees a range that reaches today");

        Assert.Equal(TimeSpan.FromHours(5), query.GetCacheDuration(Calendar));
        Assert.NotEqual(CacheDurationDefaults.StableHistory, query.GetCacheDuration(Calendar));
    }
}