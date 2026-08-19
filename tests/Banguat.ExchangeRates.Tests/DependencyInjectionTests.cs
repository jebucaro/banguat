using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddBanguatExchangeRates_Should_ResolveClient()
    {
        var services = new ServiceCollection();
        services.AddBanguatExchangeRates();

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IBanguatExchangeRateClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ApplyOptions()
    {
        var services = new ServiceCollection();
        var customAddress = new Uri("https://example.test/TipoCambio.asmx");

        services.AddBanguatExchangeRates(options => options.BaseAddress = customAddress);

        using var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(IBanguatSoapTransport));

        Assert.Equal(customAddress, httpClient.BaseAddress);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_WrapHandlersWithTracingDecorator()
    {
        var services = new ServiceCollection();
        services.AddBanguatExchangeRates();

        using var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>();

        Assert.IsType<TracingDecorator.QueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>(handler);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ResolveCurrencyAliasCatalog()
    {
        var services = new ServiceCollection();
        services.AddBanguatExchangeRates();

        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<ICurrencyAliasCatalog>();

        Assert.NotNull(catalog);
    }
}