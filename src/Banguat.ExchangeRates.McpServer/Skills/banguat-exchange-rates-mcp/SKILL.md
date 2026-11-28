---
name: banguat-exchange-rates-mcp
description: Use this skill whenever a task needs a Guatemalan Quetzal (GTQ) exchange rate - today's buy/sell rate, historical rates over a date range, or the currencies Banguat (Guatemala's central bank) publishes. Trigger on any mention of Banguat, Quetzal, GTQ, Guatemala's central bank, or a currency lookup involving Guatemala, even if the user doesn't say "exchange rate". It covers the get_currencies, get_rate and get_rate_history tools of the banguat MCP server.
---

# Banguat exchange rates over MCP

The tool descriptions say what each tool takes. This is what they leave out.

## Picking the currency

- Pass a common alias straight to the tool: `usd`, `eur`. No lookup first.
- Call `get_currencies` only for a currency you can't name an alias for. Banguat's codes are its own numbering, not
  ISO 4217 (USD is 2, not 840), so never guess a code from general knowledge.

## Reading an empty rate

- A `get_rate` result with no date, buy or sell means Banguat hasn't published a rate for that currency today, for
  example on a weekend or a holiday. The currency is fine.
- Say that plainly, or call `get_rate_history` for the last few days and report the most recent rate with its date.

## Long periods

- One `get_rate_history` call covers at most 366 days. For a longer period, make several calls and join the results.

## When a tool fails

- Tool errors carry a hint, like the list of valid currencies for an unknown one. Follow it.
- A timeout from Banguat is temporary. Retry in a moment and keep the same currency.
