using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banguat.ExchangeRates.Gateway.Tests;

public sealed class ForwardedHeadersTests : IAsyncLifetime
{
    // Stands in for the API: echoes back the forwarding headers the gateway sent.
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
    public async Task ApiRoute_TellsTheBackendAboutTheStrippedPrefixAndOriginalHost()
    {
        string backendAddress = _backend.Urls.Single();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Destinations:Api", backendAddress));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://gateway.example")
        });

        string echoed = await client.GetStringAsync("/api/v1/currencies");

        Assert.Equal("path=/v1/currencies prefix=/api host=gateway.example proto=https", echoed);
    }

    private static WebApplication CreateEchoBackend()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();
        app.Run(context => context.Response.WriteAsync(
            $"path={context.Request.Path} " +
            $"prefix={context.Request.Headers["X-Forwarded-Prefix"]} " +
            $"host={context.Request.Headers["X-Forwarded-Host"]} " +
            $"proto={context.Request.Headers["X-Forwarded-Proto"]}"));
        return app;
    }
}