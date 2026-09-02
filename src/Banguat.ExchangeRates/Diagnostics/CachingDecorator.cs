using System.Text.Json;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Banguat.ExchangeRates.Diagnostics;

internal static class CachingDecorator
{
    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        IDistributedCache cache,
        CachingOptions cachingOptions,
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
                TimeSpan duration = cachingOptions.DurationOverrides.GetValueOrDefault(
                    operationName, cacheableQuery.CacheDuration);
                await TrySetAsync(cacheKey, operationName, result.Value, duration, cancellationToken);
            }

            return result;
        }

        private async Task<TResponse?> TryGetAsync(string cacheKey, string operationName, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? cached = await cache.GetAsync(cacheKey, cancellationToken);
                return cached is null ? default : JsonSerializer.Deserialize<TResponse>(cached);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache read failed for {Operation}", operationName);
                return default;
            }
        }

        private async Task TrySetAsync(
            string cacheKey, string operationName, TResponse value, TimeSpan duration, CancellationToken cancellationToken)
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache write failed for {Operation}", operationName);
            }
        }
    }
}
