using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Api.Tests.Pipeline;

public class EndpointMetadataTests
{
    [Theory]
    [InlineData("v1/rates/{currency}")]
    [InlineData("v1/rates/{currency}/history")]
    [InlineData("v1/currencies")]
    public void Endpoint_DeclaresEveryUpstreamFailureStatus(string route)
    {
        using ApiFactory factory = new();
        EndpointDataSource dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        RouteEndpoint endpoint = dataSource.Endpoints.OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText?.TrimStart('/') == route);

        int[] statuses = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Select(m => m.StatusCode)
            .ToArray();

        Assert.Contains(StatusCodes.Status502BadGateway, statuses);
        Assert.Contains(StatusCodes.Status503ServiceUnavailable, statuses);
        Assert.Contains(StatusCodes.Status504GatewayTimeout, statuses);
    }
}