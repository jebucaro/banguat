using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banguat.ExchangeRates.Gateway.Tests;

public sealed class ForwardedHeadersTests : IAsyncLifetime
{
    // Stands in for the backend: echoes the path and forwarding headers the gateway sent.
    private readonly WebApplication _backend = CreateEchoBackend();

    public Task InitializeAsync()
    {
        return _backend.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _backend.DisposeAsync();
    }

    [Theory]
    [InlineData("Destinations:Mcp", "/mcp/ping", "path=/ping host=gateway.example proto=https")]
    [InlineData("Destinations:Api", "/api/v1/currencies", "path=/v1/currencies host=gateway.example proto=https")]
    public async Task Route_StripsThePrefixAndForwardsHostAndProto(string destinationSetting, string requestPath,
        string expected)
    {
        using HttpClient client = CreateGatewayClient(destinationSetting, out WebApplicationFactory<Program> factory);
        using (factory)
        {
            string echoed = await client.GetStringAsync(requestPath);

            Assert.Equal(expected, echoed);
        }
    }

    // Clients are configured with https://host/mcp (or /api): no trailing slash, no sub-path.
    [Theory]
    [InlineData("Destinations:Mcp", "/mcp")]
    [InlineData("Destinations:Api", "/api")]
    public async Task Route_WithoutTrailingSlash_ReachesTheBackendRoot(string destinationSetting, string requestPath)
    {
        using HttpClient client = CreateGatewayClient(destinationSetting, out WebApplicationFactory<Program> factory);
        using (factory)
        {
            string echoed = await client.GetStringAsync(requestPath);

            Assert.StartsWith("path=/ ", echoed);
        }
    }

    private HttpClient CreateGatewayClient(string destinationSetting, out WebApplicationFactory<Program> factory)
    {
        string backendAddress = _backend.Urls.Single();
        factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(destinationSetting, backendAddress));
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
