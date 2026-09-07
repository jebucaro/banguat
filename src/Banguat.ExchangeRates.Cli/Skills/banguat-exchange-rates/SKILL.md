---
name: banguat-exchange-rates-cli
description: Use when you need Guatemalan Quetzal exchange rates via the Banguat.ExchangeRates.Cli tool - covers listing currencies, today's rate, and rate history, including currency alias resolution and output modes. Use when the Banguat CLI binary is available but no MCP server or docs are.
---

# Banguat Exchange Rates CLI

This CLI talks to Banguat's (Guatemala's central bank) SOAP exchange rate service. Use it when you need a currency's buy/sell rate against the Quetzal and only have the compiled CLI binary available, no MCP server, no internet access to Banguat's docs.

## Commands

- `currencies` - list every currency Banguat publishes rates for, with its numeric code and any known alias (e.g. USD, EUR).
- `rate --currency <id|alias>` - today's buy/sell rate. Defaults to USD (2) if `--currency` is omitted.
- `rate history --since <yyyy-MM-dd> --currency <id|alias>` - rate history from a date to today.
- `rate history --from <yyyy-MM-dd> --to <yyyy-MM-dd> --currency <id|alias>` - rate history over a bounded range. Mutually exclusive with `--since`.

See `reference.md` in this skill for the full option reference, JSON shapes, and error format.

## Quick start

1. Run `currencies` first if you don't already know a currency's numeric code or alias.
2. `--currency` accepts either the numeric Banguat code or a known alias (case-insensitive). Aliases aren't guaranteed for every currency; fall back to the numeric code from `currencies` when there isn't one.
3. Add `--output json` to any command for machine-readable output instead of the default Rich table.

## Updating this skill

This skill was installed by running `skill install` in the CLI. Re-run it (`skill install --scope local|user|both`) after upgrading the CLI to pick up any changes to this content.
