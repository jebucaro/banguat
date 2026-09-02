using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Banguat.ExchangeRates.Gateway.Tests;

public class RateLimitingTests
{
    private static (HttpListener Listener, string Address) StartFakeBackend()
    {
        int port = GetFreePort();
        string prefix = $"http://127.0.0.1:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        });

        return (listener, $"http://127.0.0.1:{port}");
    }

    private static int GetFreePort()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Should_Return429_AfterPermitLimitExceeded_OnApiRoute()
    {
        (HttpListener listener, string address) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Destinations:Api"] = address,
                        ["RateLimiting:Api:PermitLimit"] = "3"
                    })));
            using HttpClient client = factory.CreateClient();

            List<HttpStatusCode> statusCodes = [];
            for (int i = 0; i < 4; i++)
            {
                HttpResponseMessage response = await client.GetAsync("/api/currencies");
                statusCodes.Add(response.StatusCode);
            }

            Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statusCodes.Take(3));
            Assert.Equal(HttpStatusCode.TooManyRequests, statusCodes[3]);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Should_TrackApiAndMcpLimitsIndependently()
    {
        (HttpListener apiListener, string apiAddress) = StartFakeBackend();
        (HttpListener mcpListener, string mcpAddress) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Destinations:Api"] = apiAddress,
                        ["Destinations:Mcp"] = mcpAddress,
                        ["RateLimiting:Api:PermitLimit"] = "1",
                        ["RateLimiting:Mcp:PermitLimit"] = "1"
                    })));
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage firstApi = await client.GetAsync("/api/currencies");
            HttpResponseMessage secondApi = await client.GetAsync("/api/currencies");
            HttpResponseMessage firstMcp = await client.GetAsync("/mcp/tools");

            Assert.NotEqual(HttpStatusCode.TooManyRequests, firstApi.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, secondApi.StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, firstMcp.StatusCode);
        }
        finally
        {
            apiListener.Stop();
            mcpListener.Stop();
        }
    }
}
