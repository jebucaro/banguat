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

        using ServiceProvider provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IBanguatExchangeRateClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ApplyOptions()
    {
        var services = new ServiceCollection();
        var customAddress = new Uri("https://example.test/TipoCambio.asmx");

        services.AddBanguatExchangeRates(options => options.BaseAddress = customAddress);

        using ServiceProvider provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient httpClient = httpClientFactory.CreateClient(nameof(IBanguatSoapTransport));

        Assert.Equal(customAddress, httpClient.BaseAddress);
    }
}
