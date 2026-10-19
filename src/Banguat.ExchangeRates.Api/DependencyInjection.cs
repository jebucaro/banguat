using Banguat.ExchangeRates.Api.Common;
using Scrutor;

namespace Banguat.ExchangeRates.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiEndpoints(
        this IServiceCollection services, Action<HttpCacheOptions>? configureCaching = null)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo<IEndpoint>(), false)
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        HttpCacheOptions cacheOptions = new();
        configureCaching?.Invoke(cacheOptions);
        services.AddSingleton(cacheOptions);

        return services;
    }
}
