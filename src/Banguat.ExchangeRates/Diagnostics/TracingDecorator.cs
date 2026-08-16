using System.Diagnostics;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace Banguat.ExchangeRates.Diagnostics;

internal static class TracingDecorator
{
    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var operationName = typeof(TQuery).DeclaringType?.Name ?? typeof(TQuery).Name;

            using var activity = BanguatExchangeRatesDiagnostics.ActivitySource.StartActivity(
                $"Banguat.ExchangeRates.{operationName}", ActivityKind.Client);
            activity?.SetTag("banguat.operation", operationName);

            var startTimestamp = Stopwatch.GetTimestamp();

            var result = await innerHandler.Handle(query, cancellationToken);

            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var outcome = result.IsSuccess ? "success" : "failure";

            BanguatExchangeRatesDiagnostics.CallCount.Add(
                1,
                new KeyValuePair<string, object?>("operation", operationName),
                new KeyValuePair<string, object?>("outcome", outcome));

            BanguatExchangeRatesDiagnostics.CallDuration.Record(
                elapsedMs,
                new KeyValuePair<string, object?>("operation", operationName));

            if (result.IsSuccess)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogDebug("Completed {Operation} in {ElapsedMs}ms", operationName, elapsedMs);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Description);
                activity?.SetTag("banguat.error.code", result.Error.Code);
                logger.LogWarning(
                    "Completed {Operation} in {ElapsedMs}ms with error {ErrorCode}: {ErrorDescription}",
                    operationName, elapsedMs, result.Error.Code, result.Error.Description);
            }

            return result;
        }
    }
}