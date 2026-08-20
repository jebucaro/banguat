using Banguat.ExchangeRates.Common;
using ModelContextProtocol;

namespace Banguat.ExchangeRates.McpServer.Tools;

public static class CurrencyArgument
{
    public static CurrencyCode Resolve(string currency, ICurrencyAliasCatalog aliasCatalog)
    {
        if (int.TryParse(currency, out int numeric))
        {
            return new CurrencyCode(numeric);
        }

        if (aliasCatalog.TryResolve(currency, out CurrencyCode code))
        {
            return code;
        }

        throw new McpException($"Unknown currency '{currency}'. Call get_currencies to see valid codes and aliases.");
    }
}
