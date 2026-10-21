using Banguat.ExchangeRates.Api.Common;
using Scrutor;

namespace Banguat.ExchangeRates.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiEndpoints(this IServiceCollection services)
    {
        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo<IEndpoint>(), false)
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        services.AddOptions<HttpCacheOptions>()
            .BindConfiguration(HttpCacheOptions.SectionName)
            .Validate(o => o.StaleWhileRevalidate >= TimeSpan.Zero,
                $"{HttpCacheOptions.SectionName}:StaleWhileRevalidate must not be negative.")
            .Validate(o => o.MinimumMaxAge >= TimeSpan.Zero,
                $"{HttpCacheOptions.SectionName}:MinimumMaxAge must not be negative.")
            .ValidateOnStart();

        return services;
    }
}