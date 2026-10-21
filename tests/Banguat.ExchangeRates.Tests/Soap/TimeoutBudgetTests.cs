using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Soap;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Tests.Soap;

public class TimeoutBudgetTests
{
    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static ServiceProvider BuildProvider(Action<BanguatExchangeRateClientOptions>? configure = null)
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates(configure);
        services.AddHttpClient(nameof(IBanguatSoapTransport))
            .ConfigurePrimaryHttpMessageHandler(() => new HangingHandler());

        return services.BuildServiceProvider();
    }

    private static Task<Result<XDocument>> InvokeAsync(
        IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        IBanguatSoapTransport transport = provider.GetRequiredService<IBanguatSoapTransport>();
        return transport.InvokeAsync(
            "TipoCambioDia", new XElement(BanguatSoapNamespaces.Service + "TipoCambioDia"), cancellationToken);
    }

    [Fact]
    public void Options_Should_DefaultToTenSecondTotal()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), new BanguatExchangeRateClientOptions().TotalTimeout);
    }

    [Fact]
    public async Task Transport_Should_ReturnTimeoutWithinBudget_WhenUpstreamHangs()
    {
        await using ServiceProvider provider = BuildProvider(options =>
            options.TotalTimeout = TimeSpan.FromMilliseconds(300));
        Stopwatch stopwatch = Stopwatch.StartNew();

        Result<XDocument> result = await InvokeAsync(provider);

        stopwatch.Stop();
        Assert.Equal("Banguat.Timeout", result.Error.Code);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1.5), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task InvokeAsync_Should_PropagateCallerCancellation_WhenUpstreamHangs()
    {
        await using ServiceProvider provider = BuildProvider();
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(provider, cts.Token));
    }
}