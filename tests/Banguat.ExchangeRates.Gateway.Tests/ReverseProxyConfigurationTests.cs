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
        Assert.Equal(RateLimitPolicies.Api, apiRoute.RateLimiterPolicy);
        Assert.Equal("api", apiRoute.ClusterId);
        Assert.NotNull(apiRoute.Transforms);
        Assert.Contains(apiRoute.Transforms!,
            t => t.TryGetValue("PathRemovePrefix", out string? prefix) && prefix == "/api");

        ClusterConfig apiCluster = Assert.Single(config.Clusters, c => c.ClusterId == "api");
        Assert.Equal("http://api", apiCluster.Destinations!["api"].Address);

        RouteConfig mcpRoute = Assert.Single(config.Routes, r => r.RouteId == "mcp");
        Assert.Equal("/mcp/{**catch-all}", mcpRoute.Match.Path);
        Assert.Equal(RateLimitPolicies.Mcp, mcpRoute.RateLimiterPolicy);
        Assert.Equal("mcp", mcpRoute.ClusterId);
        Assert.NotNull(mcpRoute.Transforms);
        Assert.Contains(mcpRoute.Transforms!,
            t => t.TryGetValue("PathRemovePrefix", out string? prefix) && prefix == "/mcp");

        ClusterConfig mcpCluster = Assert.Single(config.Clusters, c => c.ClusterId == "mcp");
        Assert.Equal("http://mcpserver", mcpCluster.Destinations!["mcp"].Address);
    }
}