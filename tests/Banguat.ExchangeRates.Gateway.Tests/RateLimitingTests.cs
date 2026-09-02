using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Gateway.Tests;

public class RateLimitingTests
{
    private static (HttpListener Listener, string Address, ConcurrentQueue<string> ReceivedPaths) StartFakeBackend()
    {
        int port = GetFreePort();
        string prefix = $"http://127.0.0.1:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();

        ConcurrentQueue<string> receivedPaths = new();

        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    receivedPaths.Enqueue(context.Request.Url!.AbsolutePath);
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

        return (listener, $"http://127.0.0.1:{port}", receivedPaths);
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
        (HttpListener listener, string address, ConcurrentQueue<string> receivedPaths) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Destinations:Api", address);
                    builder.UseSetting("RateLimiting:Api:PermitLimit", "3");
                });
            using HttpClient client = factory.CreateClient();

            List<HttpStatusCode> statusCodes = [];
            for (int i = 0; i < 4; i++)
            {
                HttpResponseMessage response = await client.GetAsync("/api/currencies");
                statusCodes.Add(response.StatusCode);
            }

            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests],
                statusCodes);
            Assert.Equal(3, receivedPaths.Count);
            Assert.All(receivedPaths, path => Assert.Equal("/currencies", path));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Should_TrackApiAndMcpLimitsIndependently()
    {
        (HttpListener apiListener, string apiAddress, ConcurrentQueue<string> _) = StartFakeBackend();
        (HttpListener mcpListener, string mcpAddress, ConcurrentQueue<string> _) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Destinations:Api", apiAddress);
                    builder.UseSetting("Destinations:Mcp", mcpAddress);
                    builder.UseSetting("RateLimiting:Api:PermitLimit", "1");
                    builder.UseSetting("RateLimiting:Mcp:PermitLimit", "1");
                });
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage firstApi = await client.GetAsync("/api/currencies");
            HttpResponseMessage secondApi = await client.GetAsync("/api/currencies");
            HttpResponseMessage firstMcp = await client.GetAsync("/mcp/tools");

            Assert.Equal(HttpStatusCode.OK, firstApi.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, secondApi.StatusCode);
            Assert.Equal(HttpStatusCode.OK, firstMcp.StatusCode);
        }
        finally
        {
            apiListener.Stop();
            mcpListener.Stop();
        }
    }

    [Fact]
    public async Task Should_RouteBareMcpRequest_ToBackendRoot()
    {
        (HttpListener listener, string address, ConcurrentQueue<string> receivedPaths) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("Destinations:Mcp", address));
            using HttpClient client = factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/mcp");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(receivedPaths);
            Assert.Equal("/", receivedPaths.First());
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Should_TrackRateLimitsPerClientIp_Independently()
    {
        (HttpListener listener, string address, ConcurrentQueue<string> _) = StartFakeBackend();
        try
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Destinations:Api", address);
                    builder.UseSetting("RateLimiting:Api:PermitLimit", "1");
                    builder.ConfigureServices(services =>
                        services.AddSingleton<IStartupFilter>(new SpoofRemoteIpStartupFilter()));
                });

            using HttpClient clientA = factory.CreateClient();
            clientA.DefaultRequestHeaders.Add("X-Test-Client-Ip", "10.0.0.1");
            using HttpClient clientB = factory.CreateClient();
            clientB.DefaultRequestHeaders.Add("X-Test-Client-Ip", "10.0.0.2");

            HttpResponseMessage firstFromA = await clientA.GetAsync("/api/currencies");
            HttpResponseMessage secondFromA = await clientA.GetAsync("/api/currencies");
            HttpResponseMessage firstFromB = await clientB.GetAsync("/api/currencies");

            Assert.Equal(HttpStatusCode.OK, firstFromA.StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, secondFromA.StatusCode);
            Assert.Equal(HttpStatusCode.OK, firstFromB.StatusCode);
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class SpoofRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    string? spoofedIpHeader = context.Request.Headers["X-Test-Client-Ip"];
                    if (spoofedIpHeader is not null && IPAddress.TryParse(spoofedIpHeader, out IPAddress? spoofedIp))
                    {
                        context.Connection.RemoteIpAddress = spoofedIp;
                    }

                    await nextMiddleware();
                });
                next(app);
            };
        }
    }
}
