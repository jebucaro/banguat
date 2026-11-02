using System.Diagnostics;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Tests.Diagnostics;
using Banguat.ExchangeRates.Tests.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Banguat.ExchangeRates.Tests;

[Collection(ActivityListenerCollection.Name)]
public class DependencyInjectionTests
{
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
    public void AddBanguatExchangeRates_Should_ResolveCurrencyResolver()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates();

        using ServiceProvider provider = services.BuildServiceProvider(true);
        using IServiceScope scope = provider.CreateScope();

        ICurrencyResolver resolver = scope.ServiceProvider.GetRequiredService<ICurrencyResolver>();

        Assert.IsType<CurrencyResolver>(resolver);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_ApplyCachingOptions()
    {
        ServiceCollection services = new();

        services.AddBanguatExchangeRates(
            configureCaching: caching => caching.DurationOverrides["Test"] = TimeSpan.FromMinutes(1));

        using ServiceProvider provider = services.BuildServiceProvider();
        CachingOptions options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(1), options.DurationOverrides["Test"]);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_RejectRelativeBaseAddress()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates(options => options.BaseAddress = new Uri("/relative", UriKind.Relative));

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BanguatExchangeRateClientOptions>>().Value);
        Assert.Contains("Banguat:BaseAddress", exception.Message);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_RejectNonPositiveTimeout()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates(options => options.TotalTimeout = TimeSpan.Zero);

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BanguatExchangeRateClientOptions>>().Value);
        Assert.Contains("Banguat:TotalTimeout", exception.Message);
    }

    [Fact]
    public void AddBanguatExchangeRates_Should_RejectNegativeCacheDurationOverride()
    {
        ServiceCollection services = new();
        services.AddBanguatExchangeRates(
            configureCaching: caching => caching.DurationOverrides["Test"] = TimeSpan.FromMinutes(-1));

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<CachingOptions>>().Value);
        Assert.Contains("Caching:DurationOverrides", exception.Message);
    }

    [Fact]
    public void AddBanguatExchangeRates_OnHostBuilder_Should_BindConfigurationSections()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Banguat:BaseAddress"] = "https://example.test/TipoCambio.asmx",
            ["Banguat:TotalTimeout"] = "00:00:20",
            ["Caching:DurationOverrides:GetRateHistory"] = "00:05:00"
        });

        builder.AddBanguatExchangeRates();
        using IHost host = builder.Build();

        BanguatExchangeRateClientOptions client =
            host.Services.GetRequiredService<IOptions<BanguatExchangeRateClientOptions>>().Value;
        CachingOptions caching = host.Services.GetRequiredService<IOptions<CachingOptions>>().Value;
        Assert.Equal(new Uri("https://example.test/TipoCambio.asmx"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(20), client.TotalTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), caching.DurationOverrides["GetRateHistory"]);
    }

    [Fact]
    public void AddBanguatExchangeRates_OnHostBuilder_Should_LetCodeOverrideConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Banguat:TotalTimeout"] = "00:00:20"
        });

        builder.AddBanguatExchangeRates(options => options.TotalTimeout = TimeSpan.FromSeconds(5));
        using IHost host = builder.Build();

        Assert.Equal(TimeSpan.FromSeconds(5),
            host.Services.GetRequiredService<IOptions<BanguatExchangeRateClientOptions>>().Value.TotalTimeout);
    }

    [Fact]
    public async Task AddBanguatExchangeRates_OnHostBuilder_Should_FailStartup_WhenConfigurationIsInvalid()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Banguat:TotalTimeout"] = "00:00:00"
        });

        builder.AddBanguatExchangeRates();
        using IHost host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
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