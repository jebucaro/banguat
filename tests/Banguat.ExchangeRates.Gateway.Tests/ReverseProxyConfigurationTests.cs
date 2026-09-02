using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Banguat.ExchangeRates.Gateway.Tests;

public class ReverseProxyConfigurationTests
{
    [Fact]
    public void Should_ConfigureApiAndMcpRoutesAndClusters()
    {
        using WebApplicationFactory<Program> factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        IProxyConfigProvider configProvider = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();

        IProxyConfig config = configProvider.GetConfig();

        RouteConfig apiRoute = Assert.Single(config.Routes, r => r.RouteId == "api");
        Assert.Equal("/api/{**catch-all}", apiRoute.Match.Path);
        Assert.Equal("api", apiRoute.RateLimiterPolicy);
        Assert.Equal("api", apiRoute.ClusterId);

        ClusterConfig apiCluster = Assert.Single(config.Clusters, c => c.ClusterId == "api");
        Assert.Equal("http://api", apiCluster.Destinations!["api"].Address);

        RouteConfig mcpRoute = Assert.Single(config.Routes, r => r.RouteId == "mcp");
        Assert.Equal("/mcp/{**catch-all}", mcpRoute.Match.Path);
        Assert.Equal("mcp", mcpRoute.RateLimiterPolicy);
        Assert.Equal("mcp", mcpRoute.ClusterId);

        ClusterConfig mcpCluster = Assert.Single(config.Clusters, c => c.ClusterId == "mcp");
        Assert.Equal("http://mcpserver", mcpCluster.Destinations!["mcp"].Address);
    }
}
