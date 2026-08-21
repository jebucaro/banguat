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

        return services;
    }
}
