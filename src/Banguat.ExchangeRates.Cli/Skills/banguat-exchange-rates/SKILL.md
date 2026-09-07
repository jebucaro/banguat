---
name: banguat-exchange-rates
description: Use this skill whenever a task needs a Guatemalan Quetzal (GTQ) exchange rate - today's buy/sell rate, historical rates over a date range, or the list of currencies Banguat (Guatemala's central bank) publishes rates for. Trigger on any mention of Banguat, Quetzal, GTQ, Guatemala's central bank, or a currency conversion/lookup involving Guatemala, even if the user doesn't name this CLI or say "exchange rate" explicitly. Use whenever the `banguat-exchangerates-cli` binary is available and no other tool (MCP server, API docs, internet access to Banguat's SOAP service) is - this skill is the only source of truth for how to drive it.
---

# Banguat Exchange Rates CLI

`banguat-exchangerates-cli` is a command-line tool that talks to Banguat's (Guatemala's central bank) SOAP exchange-rate service. Reach for it whenever you need a currency's buy/sell rate against the Quetzal (GTQ) and this binary is the only tool available - no MCP server, no internet access to Banguat's own docs, nothing else to go on but this skill.

If the binary isn't on PATH, check whether you're instead sitting in a checkout of its source repo (look for `Banguat.ExchangeRates.Cli.csproj` under `src/`) - if so, `dotnet run --project src/Banguat.ExchangeRates.Cli -- <command>` runs the exact same tool from source.

## Commands

| Command | Purpose |
|---|---|
| `currencies` | List every currency Banguat publishes rates for, with its numeric code and any known alias (e.g. USD, EUR). |
| `rate --currency <id\|alias>` | Today's buy/sell rate. Defaults to USD (`2`) if `--currency` is omitted. |
| `rate history --since <yyyy-MM-dd> --currency <id\|alias>` | Rate history from a date to today. |
| `rate history --from <yyyy-MM-dd> --to <yyyy-MM-dd> --currency <id\|alias>` | Rate history over a bounded range. Mutually exclusive with `--since`. |

Every command accepts `--output plain|rich|json` (`-o` for short); the default `rich` renders a formatted table for a human terminal. `reference.md` in this skill has the full option reference, exact JSON field shapes (including what a "no data for this date" response looks like), and the error format - read it before writing anything that parses this CLI's output, since guessing a field name wrong is the most common way to get this tool wrong.

## Quick start

1. Check `known-currencies.md` in this skill first. It's generated straight from this build's bundled alias catalog every time `skill install` runs - the exact same data `--currency` resolves against internally - so an alias listed there is guaranteed to resolve correctly. It is not a guess or a starting point to verify; treat it as ground truth. If the currency you need is listed, use that alias directly with `rate`/`rate history` - there is no reason to also run `currencies` to double-check it.
2. Only when the currency is *not* listed in `known-currencies.md`, run `currencies` to find its numeric code. Banguat's codes are its own numbering, not ISO 4217 - never guess a code from general knowledge, always look it up. Not every currency Banguat tracks has a bundled alias; that's expected, use the numeric code for those.
3. `--currency` takes either the numeric code or a known alias, case-insensitively.
4. Prefer `--output json` for anything you intend to parse or report back precisely. The `rich`/`plain` table formats exist for a human reading a terminal - re-deriving buy/sell numbers by scraping ASCII table borders is the wrong way to get this data into your answer.
5. Every failure (unknown currency, malformed date, missing required option) prints exactly one message and exits non-zero; it never throws with a stack trace for an expected failure. Check the exit code rather than guessing whether output text means success, and see `reference.md`'s Errors section for the exact shape per output mode.

## Updating this skill

This skill was installed by running `skill install` in the CLI. Re-run it (`skill install --scope local|user|both`) after upgrading the CLI to pick up any changes to this content.
