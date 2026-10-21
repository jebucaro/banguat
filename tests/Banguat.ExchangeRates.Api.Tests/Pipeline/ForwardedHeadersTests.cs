using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Api.Tests.Pipeline;

public sealed class ForwardedHeadersTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task OpenApi_FromTheGatewayOnLoopback_PointsBackThroughTheGateway()
    {
        string server = await GetOpenApiServerAsync(IPAddress.Loopback);

        Assert.Equal("https://gateway.example/api", server);
    }

    [Fact]
    public async Task OpenApi_FromAnUntrustedCaller_IgnoresTheForwardedHeaders()
    {
        string server = await GetOpenApiServerAsync(IPAddress.Parse("203.0.113.7"));

        Assert.Equal("http://localhost/", server);
    }

    [Fact]
    public async Task OpenApi_FromAConfiguredNetwork_TrustsTheForwardedHeaders()
    {
        string server = await GetOpenApiServerAsync(
            IPAddress.Parse("10.0.4.20"), ("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/16"));

        Assert.Equal("https://gateway.example/api", server);
    }

    [Fact]
    public async Task OpenApi_FromAConfiguredProxy_TrustsTheForwardedHeaders()
    {
        string server = await GetOpenApiServerAsync(
            IPAddress.Parse("10.0.4.20"), ("ForwardedHeaders:KnownProxies:0", "10.0.4.20"));

        Assert.Equal("https://gateway.example/api", server);
    }

    private async Task<string> GetOpenApiServerAsync(IPAddress remoteIp, params (string Key, string Value)[] settings)
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            // The OpenAPI document is only mapped in Development.
            builder.UseEnvironment("Development");
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(remoteIp)));
        });
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "gateway.example");
        request.Headers.Add("X-Forwarded-Prefix", "/api");

        using HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString()!;
    }

    // TestServer has no real connection, so give requests the caller address the scenario needs.
    private sealed class RemoteIpStartupFilter(IPAddress remoteIp) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = remoteIp;
                    return nextMiddleware(context);
                });
                next(app);
            };
        }
    }
}