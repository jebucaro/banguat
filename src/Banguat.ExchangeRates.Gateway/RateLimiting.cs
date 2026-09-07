namespace Banguat.ExchangeRates.Gateway;

internal static class RateLimitPolicies
{
    internal const string Api = "api";
    internal const string Mcp = "mcp";
}

internal static class RateLimitDefaults
{
    internal const int ApiPermitLimit = 60;
    internal const int McpPermitLimit = 30;
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    internal const int SegmentsPerWindow = 6;
}