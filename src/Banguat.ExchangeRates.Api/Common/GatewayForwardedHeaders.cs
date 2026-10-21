using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Banguat.ExchangeRates.Api.Common;

/// <summary>
/// Lets the API see requests the way the client sent them to the gateway (scheme, host and the stripped
/// path prefix), so generated links like the OpenAPI server URL point back through the gateway.
/// </summary>
/// <remarks>
/// Only proxies listed under <c>ForwardedHeaders:KnownProxies</c> or <c>ForwardedHeaders:KnownNetworks</c>,
/// on top of loopback, are trusted. Anyone else calling the API directly can't spoof the headers.
/// </remarks>
internal static class GatewayForwardedHeaders
{
    public const string SectionName = "ForwardedHeaders";

    public static IServiceCollection AddGatewayForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto |
                                           ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedPrefix;

                IConfigurationSection section = configuration.GetSection(SectionName);
                foreach (string proxy in section.GetSection("KnownProxies").Get<string[]>() ?? [])
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (string network in section.GetSection("KnownNetworks").Get<string[]>() ?? [])
                {
                    options.KnownIPNetworks.Add(IPNetwork.Parse(network));
                }
            });

        return services;
    }
}