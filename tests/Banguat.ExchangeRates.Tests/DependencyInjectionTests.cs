using System.Diagnostics;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Tests.Diagnostics;
using Banguat.ExchangeRates.Tests.Features;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Tests;

[Collection(ActivityListenerCollection.Name)]
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
    public void AddBanguatExchangeRates_Should_WrapHandlersWithCachingDecorator()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider();

        IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response> handler =
            provider.GetRequiredService<IQueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>();

        Assert.IsType<CachingDecorator.QueryHandler<GetCurrentUsdRate.Query, GetCurrentUsdRate.Response>>(handler);
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

    [Fact]
    public void AddBanguatExchangeRates_Should_ResolveDistributedCache()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider();

        IDistributedCache cache = provider.GetRequiredService<IDistributedCache>();

        Assert.NotNull(cache);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ApplyCachingOptions()
    {
        ServiceCollection services = new();

        services.AddBanguatExchangeRates(
            configureCaching: caching => caching.DurationOverrides["Test"] = TimeSpan.FromMinutes(1));

        using ServiceProvider provider = services.BuildServiceProvider();
        CachingOptions options = provider.GetRequiredService<CachingOptions>();

        Assert.Equal(TimeSpan.FromMinutes(1), options.DurationOverrides["Test"]);
    }

    [Fact]
    public async Task AddBanguatExchangeRates_Should_RecordTracingActivity_ThroughResolvedHandlerChain()
    {
        List<Activity> activities = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        ServiceCollection services = new();
        services.AddBanguatExchangeRates();
        services.AddScoped<IBanguatSoapTransport>(_ => new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(Error.Failure("Test.Fake", "fake"))));

        using ServiceProvider provider = services.BuildServiceProvider();

        IQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response> handler =
            provider.GetRequiredService<IQueryHandler<GetCurrentUsdRateText.Query, GetCurrentUsdRateText.Response>>();

        await handler.Handle(new GetCurrentUsdRateText.Query(), CancellationToken.None);

        Assert.Contains(activities, a => a.GetTagItem("banguat.operation") as string == "GetCurrentUsdRateText");
    }
}