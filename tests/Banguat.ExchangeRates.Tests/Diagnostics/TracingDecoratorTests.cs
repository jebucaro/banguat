using System.Diagnostics;
using System.Diagnostics.Metrics;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Microsoft.Extensions.Logging;
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

    private sealed class FailingHandler(Error error) : IQueryHandler<Probe.Query, string>
    {
        public Task<Result<string>> Handle(Probe.Query query, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Failure<string>(error));
        }
    }

    private sealed class RecordingLogger : ILogger<TracingDecorator.QueryHandler<Probe.Query, string>>
    {
        public List<LogLevel> LoggedLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullLogger.Instance.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LoggedLevels.Add(logLevel);
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
            new FailingHandler(Error.Failure("Probe.Failed", "boom")),
            NullLogger<TracingDecorator.QueryHandler<Probe.Query, string>>.Instance);

        Result<string> result = await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Activity activity = Assert.Single(activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Probe.Failed", activity.GetTagItem("banguat.error.code"));
    }

    [Theory]
    [InlineData(ErrorType.Validation, LogLevel.Information)]
    [InlineData(ErrorType.Problem, LogLevel.Warning)]
    [InlineData(ErrorType.Failure, LogLevel.Error)]
    public async Task Handle_Should_LogAtSeverityMatchingErrorType(ErrorType errorType, LogLevel expectedLevel)
    {
        Error error = new("Probe.Failed", "boom", errorType);
        RecordingLogger logger = new();

        TracingDecorator.QueryHandler<Probe.Query, string> decorator = new(
            new FailingHandler(error),
            logger);

        await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.Equal([expectedLevel], logger.LoggedLevels);
    }

    [Fact]
    public async Task Handle_Should_LogDebugOnSuccess()
    {
        RecordingLogger logger = new();

        TracingDecorator.QueryHandler<Probe.Query, string> decorator = new(
            new SucceedingHandler(),
            logger);

        await decorator.Handle(new Probe.Query(), CancellationToken.None);

        Assert.Equal([LogLevel.Debug], logger.LoggedLevels);
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