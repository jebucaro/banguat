namespace Banguat.ExchangeRates.Tests.Diagnostics;

// ActivitySource listeners are process-global, so one test's listener sees activities from tests running in
// parallel. Any class that registers one on the library's source joins this collection to run sequentially.
[CollectionDefinition(Name)]
public sealed class ActivityListenerCollection
{
    public const string Name = "ActivityListener";
}