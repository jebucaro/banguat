using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Banguat.ExchangeRates.Tests.Diagnostics;

public class CachingDecoratorTests
{
    private static class Probe
    {
        public sealed record CacheableQuery : IQuery<string>, ICacheableQuery
        {
            public TimeSpan CacheDuration => TimeSpan.FromMinutes(5);
        }

        public sealed record UncacheableQuery : IQuery<string>;
    }

    // Distinct declaring type from Probe so operationName ("SamplingProbe") doesn't collide with the
    // shared, process-wide CachingDecorator.LastWarningLoggedAtUtc entries other tests in this class prime
    // for "Probe" (e.g. via ThrowingDistributedCache).
    private static class SamplingProbe
    {
        public sealed record CacheableQuery : IQuery<string>, ICacheableQuery
        {
            public TimeSpan CacheDuration => TimeSpan.FromMinutes(5);
        }
    }

    private sealed class SucceedingCacheableHandler(string value) : IQueryHandler<Probe.CacheableQuery, string>
    {
        public int CallCount { get; private set; }

        public Task<Result<string>> Handle(Probe.CacheableQuery query, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Result.Success(value));
        }
    }

    private sealed class FailingCacheableHandler(Error error) : IQueryHandler<Probe.CacheableQuery, string>
    {
        public int CallCount { get; private set; }

        public Task<Result<string>> Handle(Probe.CacheableQuery query, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Result.Failure<string>(error));
        }
    }

    private sealed class RecordingUncacheableHandler : IQueryHandler<Probe.UncacheableQuery, string>
    {
        public int CallCount { get; private set; }

        public Task<Result<string>> Handle(Probe.UncacheableQuery query, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Result.Success("ok"));
        }
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public int SetCallCount { get; private set; }

        public TimeSpan? LastAbsoluteExpirationRelativeToNow { get; private set; }

        public byte[]? Get(string key) => _store.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(_store.GetValueOrDefault(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _store[key] = value;
            SetCallCount++;
            LastAbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("boom");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("boom");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("boom");

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("boom");

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Handle_Should_ReturnCachedValue_WithoutCallingInnerHandler_OnCacheHit()
    {
        FakeDistributedCache cache = new();
        SucceedingCacheableHandler handler = new("first");
        CachingDecorator.QueryHandler<Probe.CacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(),
            NullLogger<CachingDecorator.QueryHandler<Probe.CacheableQuery, string>>.Instance);

        Result<string> first = await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);
        Result<string> second = await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);

        Assert.Equal("first", first.Value);
        Assert.Equal("first", second.Value);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Handle_Should_StoreResult_WithQueryCacheDuration_OnCacheMiss()
    {
        FakeDistributedCache cache = new();
        SucceedingCacheableHandler handler = new("value");
        CachingDecorator.QueryHandler<Probe.CacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(),
            NullLogger<CachingDecorator.QueryHandler<Probe.CacheableQuery, string>>.Instance);

        await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);

        Assert.Equal(1, cache.SetCallCount);
        Assert.Equal(TimeSpan.FromMinutes(5), cache.LastAbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Handle_Should_UseConfiguredDurationOverride_WhenPresent()
    {
        FakeDistributedCache cache = new();
        SucceedingCacheableHandler handler = new("value");
        CachingOptions options = new()
        {
            DurationOverrides = new Dictionary<string, TimeSpan> { ["Probe"] = TimeSpan.FromMinutes(30) }
        };
        CachingDecorator.QueryHandler<Probe.CacheableQuery, string> decorator = new(
            handler, cache, options,
            NullLogger<CachingDecorator.QueryHandler<Probe.CacheableQuery, string>>.Instance);

        await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(30), cache.LastAbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task Handle_Should_BypassCache_ForNonCacheableQuery()
    {
        FakeDistributedCache cache = new();
        RecordingUncacheableHandler handler = new();
        CachingDecorator.QueryHandler<Probe.UncacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(),
            NullLogger<CachingDecorator.QueryHandler<Probe.UncacheableQuery, string>>.Instance);

        await decorator.Handle(new Probe.UncacheableQuery(), CancellationToken.None);
        await decorator.Handle(new Probe.UncacheableQuery(), CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(0, cache.SetCallCount);
    }

    [Fact]
    public async Task Handle_Should_NotCache_FailureResult()
    {
        FakeDistributedCache cache = new();
        FailingCacheableHandler handler = new(Error.Failure("Probe.Failed", "boom"));
        CachingDecorator.QueryHandler<Probe.CacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(),
            NullLogger<CachingDecorator.QueryHandler<Probe.CacheableQuery, string>>.Instance);

        await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);
        await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(0, cache.SetCallCount);
    }

    [Fact]
    public async Task Handle_Should_FailOpen_WhenCacheThrows()
    {
        ThrowingDistributedCache cache = new();
        SucceedingCacheableHandler handler = new("value");
        CachingDecorator.QueryHandler<Probe.CacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(),
            NullLogger<CachingDecorator.QueryHandler<Probe.CacheableQuery, string>>.Instance);

        Result<string> result = await decorator.Handle(new Probe.CacheableQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class SucceedingSamplingProbeHandler(string value) : IQueryHandler<SamplingProbe.CacheableQuery, string>
    {
        public Task<Result<string>> Handle(SamplingProbe.CacheableQuery query, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success(value));
        }
    }

    private sealed class ReadThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("boom");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("boom");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            Task.CompletedTask;

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class RecordingLogger : ILogger<CachingDecorator.QueryHandler<SamplingProbe.CacheableQuery, string>>
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
    public async Task Handle_Should_SampleWarningLogs_ForRepeatedCacheReadFailures()
    {
        ReadThrowingDistributedCache cache = new();
        SucceedingSamplingProbeHandler handler = new("value");
        RecordingLogger logger = new();
        CachingDecorator.QueryHandler<SamplingProbe.CacheableQuery, string> decorator = new(
            handler, cache, new CachingOptions(), logger);

        await decorator.Handle(new SamplingProbe.CacheableQuery(), CancellationToken.None);
        await decorator.Handle(new SamplingProbe.CacheableQuery(), CancellationToken.None);

        Assert.Equal(1, logger.LoggedLevels.Count(level => level == LogLevel.Warning));
        Assert.Equal(1, logger.LoggedLevels.Count(level => level == LogLevel.Debug));
    }
}
