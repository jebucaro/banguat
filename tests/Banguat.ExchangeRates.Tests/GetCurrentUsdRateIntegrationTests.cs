using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Tests.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Banguat.ExchangeRates.Tests;

[Trait("Category", "Integration")]
[Collection(ActivityListenerCollection.Name)]
public class GetCurrentUsdRateIntegrationTests
{
    [Fact]
    public async Task GetCurrentUsdRateAsync_Should_ReturnTodaysRate_FromLiveService()
    {
        var services = new ServiceCollection();
        services.AddBanguatExchangeRates();
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IBanguatExchangeRateClient>();

        var result = await client.GetCurrentUsdRateAsync();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : string.Empty);
        Assert.True(result.Value.Rate > 0);
        Assert.True(result.Value.Date >= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7));
    }
}