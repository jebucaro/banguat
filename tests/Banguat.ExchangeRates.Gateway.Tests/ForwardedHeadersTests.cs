using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banguat.ExchangeRates.Gateway.Tests;

public sealed class ForwardedHeadersTests : IAsyncLifetime
{
    // Stands in for the MCP server: echoes back the path and forwarding headers the gateway sent.
    private readonly WebApplication _backend = CreateEchoBackend();

    public Task InitializeAsync()
    {
        return _backend.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _backend.DisposeAsync();
    }

    [Fact]
    public async Task McpRoute_StripsThePrefixAndForwardsHostAndProto()
    {
        using HttpClient client = CreateGatewayClient(out WebApplicationFactory<Program> factory);
        using (factory)
        {
            string echoed = await client.GetStringAsync("/mcp/ping");

            Assert.Equal("path=/ping host=gateway.example proto=https", echoed);
        }
    }

    // Clients are configured with https://host/mcp, no trailing slash and no sub-path.
    [Fact]
    public async Task McpRoute_WithoutTrailingSlash_ReachesTheBackendRoot()
    {
        using HttpClient client = CreateGatewayClient(out WebApplicationFactory<Program> factory);
        using (factory)
        {
            string echoed = await client.GetStringAsync("/mcp");

            Assert.StartsWith("path=/ ", echoed);
        }
    }

    private HttpClient CreateGatewayClient(out WebApplicationFactory<Program> factory)
    {
        string backendAddress = _backend.Urls.Single();
        factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Destinations:Mcp", backendAddress));
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://gateway.example")
        });
    }

    private static WebApplication CreateEchoBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();
        app.Run(context => context.Response.WriteAsync(
            $"path={context.Request.Path} " +
            $"host={context.Request.Headers["X-Forwarded-Host"]} " +
            $"proto={context.Request.Headers["X-Forwarded-Proto"]}"));
        return app;
    }
}
