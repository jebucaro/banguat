using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;

namespace Banguat.ExchangeRates.Tests;

internal sealed class RecordingQueryHandler<TQuery, TResponse>(Result<TResponse> response)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public TQuery? LastQuery { get; private set; }

    public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;
        return Task.FromResult(response);
    }
}