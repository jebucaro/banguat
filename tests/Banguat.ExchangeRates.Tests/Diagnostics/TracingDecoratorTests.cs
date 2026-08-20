using System.Diagnostics;
using System.Diagnostics.Metrics;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banguat.ExchangeRates.Tests.Diagnostics;

[Collection(ActivityListenerCollection.Name)]
public class TracingDecoratorTests
{
    private static class Probe
    {
        public sealed record Query : IQuery<string>;
    }

    private sealed class SucceedingHandler : IQueryHandler<Probe.Query, string>
    {
        public Task<Result<string>> Handle(Probe.Query query, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success("ok"));
        }
    }

    private sealed class FailingHandler : IQueryHandler<Probe.Query, string>
    {
        public Task<Result<string>> Handle(Probe.Query query, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Failure<string>(Error.Failure("Probe.Failed", "boom")));
        }
    }

    [Fact]
    public async Task Handle_Should_RecordSuccessActivity()
    {
        List<Activity> activities = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        TracingDecorator.QueryHandler<Probe.Query, string> decorator = new(
            new SucceedingHandler(),
            NullLogger<TracingDecorator.QueryHandler<Probe.Query, string>>.Instance);

        Result<string> result = await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Activity activity = Assert.Single(activities);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal("Probe", activity.GetTagItem("banguat.operation"));
    }

    [Fact]
    public async Task Handle_Should_RecordFailureActivityWithErrorTag()
    {
        List<Activity> activities = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        TracingDecorator.QueryHandler<Probe.Query, string> decorator = new(
            new FailingHandler(),
            NullLogger<TracingDecorator.QueryHandler<Probe.Query, string>>.Instance);

        Result<string> result = await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Activity activity = Assert.Single(activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Probe.Failed", activity.GetTagItem("banguat.error.code"));
    }

    [Fact]
    public async Task Handle_Should_RecordCallCountMetric()
    {
        List<long> measurements = new();
        using MeterListener meterListener = new();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == BanguatExchangeRatesDiagnostics.MeterName &&
                instrument.Name == "banguat.exchangerates.calls")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((_, measurement, _, _) => measurements.Add(measurement));
        meterListener.Start();

        TracingDecorator.QueryHandler<Probe.Query, string> decorator = new(
            new SucceedingHandler(),
            NullLogger<TracingDecorator.QueryHandler<Probe.Query, string>>.Instance);

        await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.Equal([1L], measurements);
    }
}