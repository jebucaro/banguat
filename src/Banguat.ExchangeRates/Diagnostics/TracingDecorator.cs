using System.Diagnostics;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Microsoft.Extensions.Logging;

namespace Banguat.ExchangeRates.Diagnostics;

internal static partial class TracingDecorator
{
    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            string operationName = typeof(TQuery).DeclaringType?.Name ?? typeof(TQuery).Name;

            using Activity? activity = BanguatExchangeRatesDiagnostics.ActivitySource.StartActivity(
                $"Banguat.ExchangeRates.{operationName}", ActivityKind.Client);
            activity?.SetTag("banguat.operation", operationName);

            long startTimestamp = Stopwatch.GetTimestamp();

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string outcome = result.IsSuccess ? "success" : "failure";

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
                Log.Completed(logger, operationName, elapsedMs);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error.Description);
                activity?.SetTag("banguat.error.code", result.Error.Code);

                LogLevel logLevel = result.Error.Type switch
                {
                    ErrorType.Validation or ErrorType.NotFound => LogLevel.Information,
                    ErrorType.Problem or ErrorType.Timeout or ErrorType.Unavailable => LogLevel.Warning,
                    _ => LogLevel.Error
                };

                Log.CompletedWithError(
                    logger, logLevel, operationName, elapsedMs, result.Error.Code, result.Error.Description);
            }

            return result;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "Completed {Operation} in {ElapsedMs}ms")]
        public static partial void Completed(ILogger logger, string operation, double elapsedMs);

        [LoggerMessage("Completed {Operation} in {ElapsedMs}ms with error {ErrorCode}: {ErrorDescription}")]
        public static partial void CompletedWithError(
            ILogger logger, LogLevel level, string operation, double elapsedMs, string errorCode,
            string errorDescription);
    }
}