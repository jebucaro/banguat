using System.Collections.Concurrent;
using System.Text.Json;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Banguat.ExchangeRates.Diagnostics;

internal static partial class CachingDecorator
{
    private static readonly ConcurrentDictionary<string, DateTime> LastWarningLoggedAtUtc = new();
    private static readonly TimeSpan WarningSampleInterval = TimeSpan.FromMinutes(1);

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        IDistributedCache cache,
        IOptions<CachingOptions> cachingOptions,
        IRateCalendar calendar,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            if (query is not ICacheableQuery cacheableQuery)
            {
                return await innerHandler.Handle(query, cancellationToken);
            }

            string operationName = typeof(TQuery).DeclaringType?.Name ?? typeof(TQuery).Name;
            string cacheKey = $"banguat:{operationName}:{JsonSerializer.Serialize(query)}";

            TResponse? cached = await TryGetAsync(cacheKey, operationName, cancellationToken);
            if (cached is not null)
            {
                return Result.Success(cached);
            }

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            if (result.IsSuccess)
            {
                TimeSpan duration = cachingOptions.Value.DurationOverrides.GetValueOrDefault(
                    operationName, cacheableQuery.GetCacheDuration(calendar));

                // A zero (or negative) duration is how an operator turns caching off for one operation.
                if (duration > TimeSpan.Zero)
                {
                    await TrySetAsync(cacheKey, operationName, result.Value, duration, cancellationToken);
                }
            }

            return result;
        }

        private async Task<TResponse?> TryGetAsync(string cacheKey, string operationName,
            CancellationToken cancellationToken)
        {
            try
            {
                byte[]? cached = await cache.GetAsync(cacheKey, cancellationToken);
                if (cached is null)
                {
                    BanguatExchangeRatesDiagnostics.CacheLookupCount.Add(
                        1, new KeyValuePair<string, object?>("outcome", "miss"));
                    return default;
                }

                TResponse? result = JsonSerializer.Deserialize<TResponse>(cached);
                BanguatExchangeRatesDiagnostics.CacheLookupCount.Add(
                    1, new KeyValuePair<string, object?>("outcome", "hit"));
                return result;
            }
            // A cache backend's own timeout surfaces as a cancellation too; only the caller's cancellation propagates.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                BanguatExchangeRatesDiagnostics.CacheLookupCount.Add(
                    1, new KeyValuePair<string, object?>("outcome", "error"));
                LogCacheFailure(operationName, "read", ex);
                return default;
            }
        }

        private async Task TrySetAsync(
            string cacheKey, string operationName, TResponse value, TimeSpan duration,
            CancellationToken cancellationToken)
        {
            try
            {
                byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(value);
                await cache.SetAsync(
                    cacheKey,
                    serialized,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration },
                    cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogCacheFailure(operationName, "write", ex);
            }
        }

        private void LogCacheFailure(string operationName, string operationKind, Exception ex)
        {
            LogCacheFailure(operationName, operationKind, ex, logger);
        }

        private static void LogCacheFailure(string operationName, string operationKind, Exception ex, ILogger logger)
        {
            string key = $"{operationName}:{operationKind}";
            DateTime now = DateTime.UtcNow;
            bool shouldWarn = !LastWarningLoggedAtUtc.TryGetValue(key, out DateTime last)
                              || now - last >= WarningSampleInterval;

            if (shouldWarn)
            {
                LastWarningLoggedAtUtc[key] = now;
                Log.CacheFailed(logger, LogLevel.Warning, ex, operationKind, operationName);
            }
            else
            {
                Log.CacheFailed(logger, LogLevel.Debug, ex, operationKind, operationName);
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage("Cache {OperationKind} failed for {Operation}")]
        public static partial void CacheFailed(
            ILogger logger, LogLevel level, Exception ex, string operationKind, string operation);
    }
}