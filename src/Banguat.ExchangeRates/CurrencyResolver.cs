using System.Globalization;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates;

public sealed class CurrencyResolver(IBanguatExchangeRateClient client, ICurrencyAliasCatalog aliasCatalog)
    : ICurrencyResolver
{
    private const int MaxSuggestDistance = 2;

    // Aliases are three letters and Banguat codes fit in an int, so anything longer is unknown. Checking first
    // keeps a huge input from paying for a fuzzy match against every alias.
    private const int MaxInputLength = 16;

    public async Task<Result<CurrencyCode>> ResolveAsync(
        string currency, CancellationToken cancellationToken = default)
    {
        if (currency.Length > MaxInputLength)
        {
            return Result.Failure<CurrencyCode>(BanguatErrors.UnknownCurrency(currency, null));
        }

        if (aliasCatalog.TryResolve(currency, out CurrencyCode aliased))
        {
            return Result.Success(aliased);
        }

        // Digits only: a sign, whitespace or a culture-specific format is not a Banguat code.
        if (!int.TryParse(currency, NumberStyles.None, CultureInfo.InvariantCulture, out int numeric))
        {
            return Result.Failure<CurrencyCode>(
                BanguatErrors.UnknownCurrency(currency, SuggestNearestAlias(currency)));
        }

        CurrencyCode code = new(numeric);

        // A code with a bundled alias is known to exist, so it skips the live lookup.
        if (aliasCatalog.GetAlias(code) is not null)
        {
            return Result.Success(code);
        }

        if (numeric == 0)
        {
            return Result.Failure<CurrencyCode>(BanguatErrors.UnknownCurrencyCode(numeric));
        }

        Result<GetAvailableCurrencies.Response> available =
            await client.GetAvailableCurrenciesAsync(cancellationToken);

        if (available.IsFailure)
        {
            // Banguat is down: stop here so the caller gets the gateway error after one call, not two.
            // Any other catalog failure fails open and lets the rate request report its own outcome.
            return BanguatErrors.IndicatesUnreachable(available.Error)
                ? Result.Failure<CurrencyCode>(available.Error)
                : Result.Success(code);
        }

        return available.Value.Currencies.Any(entry => entry.Code == code)
            ? Result.Success(code)
            : Result.Failure<CurrencyCode>(BanguatErrors.UnknownCurrencyCode(numeric));
    }

    private string? SuggestNearestAlias(string value)
    {
        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (string candidate in aliasCatalog.AllAliases)
        {
            int distance = LevenshteinDistance(value.ToUpperInvariant(), candidate.ToUpperInvariant());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance <= MaxSuggestDistance ? best : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] distances = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
    }
}