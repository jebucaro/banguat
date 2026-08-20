using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace Banguat.ExchangeRates;

public static class DependencyInjection
{
    public static IServiceCollection AddBanguatExchangeRates(
        this IServiceCollection services,
        Action<BanguatExchangeRateClientOptions>? configure = null)
    {
        BanguatExchangeRateClientOptions options = new();
        configure?.Invoke(options);

        services.AddHttpClient<IBanguatSoapTransport, BanguatSoapTransport>(http =>
        {
            http.BaseAddress = options.BaseAddress;
        });

        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes
                .AssignableTo(typeof(IQueryHandler<,>))
                .Where(type => !type.IsGenericTypeDefinition), false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Decorate(typeof(IQueryHandler<,>), typeof(TracingDecorator.QueryHandler<,>));

        services.AddScoped<IBanguatExchangeRateClient, BanguatExchangeRateClient>();

        services.AddSingleton<ICurrencyAliasCatalog, BundledCurrencyAliasCatalog>();

        return services;
    }
}