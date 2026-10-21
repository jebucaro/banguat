using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetRateHistoryTests
{
    // 2026-09-23T01:00Z is 19:00 on 2026-09-22 in Guatemala: the UTC date has already rolled over while
    // the Guatemala day still has five hours left. Pinning this instant keeps the expected values explicit.
    private static readonly IRateCalendar Calendar =
        new GuatemalaRateCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Handle_Should_Since_ReturnRatePoints_OnSuccess()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialMonedaResult><Vars><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta>17.0271</venta><compra>17.0241</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioFechaInicialMonedaResult></TipoCambioFechaInicialMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), new DateOnly(2026, 8, 17)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        GetRateHistory.RatePoint point = Assert.Single(result.Value.Rates);
        Assert.Equal(17.0241m, point.Buy);
        Assert.Equal(17.0271m, point.Sell);
        Assert.Equal("TipoCambioFechaInicialMoneda", transport.LastOperationName);
        Assert.Equal(
            "17/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
        Assert.Equal(
            "18",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "moneda")!.Value);
    }

    [Fact]
    public async Task Handle_Should_Since_ReturnUnexpectedShape_WhenResultMissing()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), new DateOnly(2026, 8, 17)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_Since_PropagateTransportFailure()
    {
        FakeBanguatSoapTransport transport = new(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), new DateOnly(2026, 8, 17)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public void GetCacheDuration_Should_Since_ExpireAtNextGuatemalaDay()
    {
        GetRateHistory.Query query = new(new CurrencyCode(24), new DateOnly(2020, 1, 1));

        Assert.Equal(TimeSpan.FromHours(5), query.GetCacheDuration(Calendar));
    }

    [Fact]
    public async Task Handle_Should_Since_ReturnUnexpectedShape_WhenARowHasEmptyVenta()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialMonedaResult><Vars><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta></venta><compra>17.0241</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioFechaInicialMonedaResult></TipoCambioFechaInicialMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), new DateOnly(2026, 8, 17)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_Since_ReturnUnexpectedShape_WhenTotalItemsExceedsRows()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialMonedaResult><Vars><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta>17.0271</venta><compra>17.0241</compra></Var></Vars><TotalItems>6</TotalItems></TipoCambioFechaInicialMonedaResult></TipoCambioFechaInicialMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), new DateOnly(2026, 8, 17)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_Range_ReturnRatePoints_OnSuccess()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoMonedaResult><Vars><Var><moneda>18</moneda><fecha>03/08/2026</fecha><venta>17.2985</venta><compra>17.2953</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 1),
                To: new DateOnly(2026, 8, 3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        GetRateHistory.RatePoint point = Assert.Single(result.Value.Rates);
        Assert.Equal(17.2953m, point.Buy);
        Assert.Equal(17.2985m, point.Sell);
        Assert.Equal("TipoCambioRangoMoneda", transport.LastOperationName);
        Assert.Equal(
            "18",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "moneda")!.Value);
        Assert.Equal(
            "01/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
        Assert.Equal(
            "03/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechafin")!.Value);
    }

    [Fact]
    public async Task Handle_Should_Range_ReturnInvalidDateRange_WhenToIsBeforeFrom()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 3),
                To: new DateOnly(2026, 8, 1)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidDateRange", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_Range_ReturnUnexpectedShape_WhenResultMissing()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 1),
                To: new DateOnly(2026, 8, 3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_Range_PropagateTransportFailure()
    {
        FakeBanguatSoapTransport transport = new(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 1),
                To: new DateOnly(2026, 8, 3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public void GetCacheDuration_Should_Range_ReturnStableHistory_WhenRangeEndedBeforeToday()
    {
        GetRateHistory.Query query = new(new CurrencyCode(24), From: new DateOnly(2020, 1, 1),
            To: new DateOnly(2020, 1, 5));

        Assert.Equal(CacheDurationDefaults.StableHistory, query.GetCacheDuration(Calendar));
    }

    // Regression guard: the boundary used to be evaluated with DateTime.UtcNow, so during the final six
    // hours of a Guatemala day a range ending today was misread as closed history and cached for 24h.
    [Fact]
    public void GetCacheDuration_Should_Range_ExpireAtNextGuatemalaDay_WhenRangeEndsToday()
    {
        GetRateHistory.Query query = new(
            new CurrencyCode(24), From: new DateOnly(2026, 9, 15), To: new DateOnly(2026, 9, 22));

        // At this instant the UTC date is already the 23rd while Guatemala is still on the 22nd, so the
        // two boundaries disagree and the old comparison took the wrong branch.
        DateOnly utcToday = DateOnly.FromDateTime(new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc));
        Assert.True(query.To!.Value < utcToday, "the superseded UTC check classified this range as closed history");
        Assert.False(query.To!.Value < Calendar.Today, "the Guatemala calendar sees a range that reaches today");

        Assert.Equal(TimeSpan.FromHours(5), query.GetCacheDuration(Calendar));
        Assert.NotEqual(CacheDurationDefaults.StableHistory, query.GetCacheDuration(Calendar));
    }

    [Fact]
    public async Task Handle_Should_Range_ReturnUnexpectedShape_WhenARowHasEmptyVenta()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoMonedaResult><Vars><Var><moneda>18</moneda><fecha>03/08/2026</fecha><venta></venta><compra>17.2953</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 1),
                To: new DateOnly(2026, 8, 3)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_Range_ReturnUnexpectedShape_WhenTotalItemsExceedsRows()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoMonedaResult><Vars><Var><moneda>18</moneda><fecha>03/08/2026</fecha><venta>17.2985</venta><compra>17.2953</compra></Var></Vars><TotalItems>6</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(18), From: new DateOnly(2026, 8, 1),
                To: new DateOnly(2026, 8, 3)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Handle_Should_ReturnInvalidHistoryRange_WhenDatesDoNotFormOneShape(
        bool sinceGiven, bool fromGiven, bool toGiven)
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetRateHistory.Handler handler = new(transport, Calendar);
        GetRateHistory.Query query = new(
            new CurrencyCode(18),
            sinceGiven ? new DateOnly(2026, 8, 1) : null,
            fromGiven ? new DateOnly(2026, 8, 1) : null,
            toGiven ? new DateOnly(2026, 8, 3) : null);

        Result<GetRateHistory.Response> result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidHistoryRange", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_Since_RejectWithoutCallingBanguat_WhenBeforeEarliestDate()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(2), new DateOnly(1, 1, 1)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateBeforeEarliest", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    // "since" runs to today, so an old start date is as wide as a decades-long explicit range.
    [Fact]
    public async Task Handle_Should_Since_RejectWithoutCallingBanguat_WhenSpanExceedsMaxDays()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(2), new DateOnly(2020, 1, 1)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateRangeTooLong", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_Range_RejectWithoutCallingBanguat_WhenToIsInTheFuture()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(new XDocument()));
        GetRateHistory.Handler handler = new(transport, Calendar);

        Result<GetRateHistory.Response> result = await handler.Handle(
            new GetRateHistory.Query(new CurrencyCode(2), From: new DateOnly(2026, 9, 1),
                To: new DateOnly(2026, 9, 23)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.DateInFuture", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }
}