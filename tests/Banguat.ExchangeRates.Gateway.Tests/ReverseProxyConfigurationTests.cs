using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Banguat.ExchangeRates.Gateway.Tests;

public class ReverseProxyConfigurationTests
{
    [Fact]
    public void Should_ConfigureOnlyTheMcpRouteAndCluster()
    {
        using WebApplicationFactory<Program> factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        IProxyConfigProvider configProvider = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();

        IProxyConfig config = configProvider.GetConfig();

        RouteConfig mcpRoute = Assert.Single(config.Routes);
        Assert.Equal("mcp", mcpRoute.RouteId);
        Assert.Equal("/mcp/{**catch-all}", mcpRoute.Match.Path);
        Assert.Equal("mcp", mcpRoute.ClusterId);
        Assert.NotNull(mcpRoute.Transforms);
        Assert.Contains(mcpRoute.Transforms!,
            t => t.TryGetValue("PathRemovePrefix", out string? prefix) && prefix == "/mcp");

        ClusterConfig mcpCluster = Assert.Single(config.Clusters);
        Assert.Equal("mcp", mcpCluster.ClusterId);
        Assert.Equal("https+http://mcpserver", mcpCluster.Destinations!["mcp"].Address);
    }
}
