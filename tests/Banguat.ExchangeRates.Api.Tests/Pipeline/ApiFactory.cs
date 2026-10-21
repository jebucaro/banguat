using Banguat.ExchangeRates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Banguat.ExchangeRates.Api.Tests.Pipeline;

/// <summary>
/// Runs the real API pipeline (routing, /v1 group, HTTP caching filter, exception handling) with the Banguat
/// client substituted and an in-memory cache in place of Valkey.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public IBanguatExchangeRateClient Client { get; } = Substitute.For<IBanguatExchangeRateClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:cache", "localhost:6379,abortConnect=false");
        builder.UseSetting("Aspire:StackExchange:Redis:DisableHealthChecks", "true");
        builder.UseSetting("Aspire:StackExchange:Redis:DisableTracing", "true");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IDistributedCache>(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            services.AddScoped(_ => Client);
        });
    }
}