namespace Banguat.ExchangeRates.Tests.Diagnostics;

// ActivitySource.AddActivityListener is process-global static state: a listener registered by one
// test observes activities started by any other test running concurrently. xUnit parallelizes
// different test classes by default, so every test class that registers a listener on
// BanguatExchangeRatesDiagnostics.ActivitySourceName must share this collection to run sequentially
// relative to each other and avoid cross-test activity leakage.
[CollectionDefinition(Name)]
public sealed class ActivityListenerCollection
{
    public const string Name = "ActivityListener";
}
