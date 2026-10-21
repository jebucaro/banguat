using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scrutor;

namespace Banguat.ExchangeRates;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the exchange-rate client for a host with configuration, binding the
    /// <see cref="BanguatExchangeRateClientOptions.SectionName"/> and <see cref="CachingOptions.SectionName"/>
    /// sections. The optional delegates run after binding, so code wins over configuration.
    /// </summary>
    public static IHostApplicationBuilder AddBanguatExchangeRates(
        this IHostApplicationBuilder builder,
        Action<BanguatExchangeRateClientOptions>? configure = null,
        Action<CachingOptions>? configureCaching = null)
    {
        builder.Services.AddOptions<BanguatExchangeRateClientOptions>()
            .BindConfiguration(BanguatExchangeRateClientOptions.SectionName);
        builder.Services.AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName);

        builder.Services.AddBanguatExchangeRates(configure, configureCaching);

        return builder;
    }

    /// <summary>
    /// Registers the exchange-rate client from code alone, for hosts without configuration such as the CLI.
    /// </summary>
    public static IServiceCollection AddBanguatExchangeRates(
        this IServiceCollection services,
        Action<BanguatExchangeRateClientOptions>? configure = null,
        Action<CachingOptions>? configureCaching = null)
    {
        OptionsBuilder<BanguatExchangeRateClientOptions> clientOptions =
            services.AddOptions<BanguatExchangeRateClientOptions>();
        if (configure is not null)
        {
            clientOptions.Configure(configure);
        }

        // ValidateOnStart makes a generic host refuse to start on a bad value; hosts without one, such as the
        // CLI, still fail on first use with the same message instead of a confusing HttpClient error.
        clientOptions
            .Validate(o => o.BaseAddress is { IsAbsoluteUri: true },
                $"{BanguatExchangeRateClientOptions.SectionName}:BaseAddress must be an absolute URI.")
            .Validate(o => o.TotalTimeout > TimeSpan.Zero,
                $"{BanguatExchangeRateClientOptions.SectionName}:TotalTimeout must be greater than zero.")
            .ValidateOnStart();

        OptionsBuilder<CachingOptions> cachingOptions = services.AddOptions<CachingOptions>();
        if (configureCaching is not null)
        {
            cachingOptions.Configure(configureCaching);
        }

        cachingOptions
            .Validate(o => o.DurationOverrides.Values.All(duration => duration >= TimeSpan.Zero),
                $"{CachingOptions.SectionName}:DurationOverrides must not contain negative durations.")
            .ValidateOnStart();

        services.AddDistributedMemoryCache();

        // Hosts that call AddServiceDefaults also wrap this client in the standard resilience pipeline, sized to
        // the same budget there; this timeout is what bounds hosts without it, such as the CLI.
        services.AddHttpClient<IBanguatSoapTransport, BanguatSoapTransport>((provider, http) =>
        {
            BanguatExchangeRateClientOptions options =
                provider.GetRequiredService<IOptions<BanguatExchangeRateClientOptions>>().Value;
            http.BaseAddress = options.BaseAddress;
            http.Timeout = options.TotalTimeout;
        });

        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes
                .AssignableTo(typeof(IQueryHandler<,>))
                .Where(type => !type.IsGenericTypeDefinition), false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Decorate(typeof(IQueryHandler<,>), typeof(TracingDecorator.QueryHandler<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(CachingDecorator.QueryHandler<,>));

        services.AddScoped<IBanguatExchangeRateClient, BanguatExchangeRateClient>();

        services.AddSingleton<ICurrencyAliasCatalog, BundledCurrencyAliasCatalog>();
        services.AddScoped<ICurrencyResolver, CurrencyResolver>();

        // TryAdd so a host (or a test) can supply its own clock before calling this method.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRateCalendar, GuatemalaRateCalendar>();

        return services;
    }
}