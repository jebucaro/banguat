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
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider();

        IBanguatExchangeRateClient client = provider.GetRequiredService<IBanguatExchangeRateClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ApplyOptions()
    {
        ServiceCollection services = new();
        Uri customAddress = new("https://example.test/TipoCambio.asmx");

        services.AddBanguatExchangeRates(options => options.BaseAddress = customAddress);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient(nameof(IBanguatSoapTransport));

        Assert.Equal(customAddress, httpClient.BaseAddress);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_WrapHandlersWithTracingDecorator()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider();

        IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response> handler =
            provider.GetRequiredService<IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>();

        Assert.IsType<TracingDecorator.QueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>(handler);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ResolveCurrencyAliasCatalog()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider();

        ICurrencyAliasCatalog catalog = provider.GetRequiredService<ICurrencyAliasCatalog>();

        Assert.NotNull(catalog);
    }
}