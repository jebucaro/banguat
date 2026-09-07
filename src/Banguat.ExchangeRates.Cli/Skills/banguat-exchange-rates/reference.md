# Command reference

Invoke the binary directly - `banguat-exchangerates-cli <command> [options]`. If you're running from a source checkout instead of an installed binary, replace `banguat-exchangerates-cli` with `dotnet run --project src/Banguat.ExchangeRates.Cli --` in every example below (note the `--` before the command).

## Global option

- `--output`, `-o` - `plain` | `rich` | `json`. Defaults to `rich`. `json` is the only mode worth parsing programmatically - `plain` and `rich` are for a human terminal and their column layout isn't a stable contract.

## `currencies`

Lists every currency Banguat publishes rates for.

```
banguat-exchangerates-cli currencies [--output plain|rich|json]
```

JSON shape:

```json
{
  "count": 40,
  "currencies": [
    { "code": 1, "description": "Quetzales", "alias": "GTQ" },
    { "code": 2, "description": "Dólares de EE.UU.", "alias": "USD" },
    { "code": 22, "description": "Bolivares Venezolanos", "alias": null }
  ],
  "help": ["rate --currency <id|alias>", "rate history --since <date> --currency <id|alias>"]
}
```

`alias` is `null` (or absent in `plain`/`rich`) for currencies with no bundled shortcut - about a quarter of the ~40 currencies Banguat tracks fall into this bucket, so don't assume every result has one. Descriptions are in Spanish (this is a Guatemalan government service); codes and aliases are what you actually pass to `--currency`.

## `rate`

Today's buy/sell rate for one currency.

```
banguat-exchangerates-cli rate [--currency <id|alias>] [--output plain|rich|json]
```

`--currency` defaults to `2` (USD) when omitted.

JSON shape:

```json
{
  "date": "2026-09-07",
  "currency": 2,
  "currencyAlias": "USD",
  "buy": 7.6222,
  "sell": 7.6222,
  "help": ["rate history --since <date> --currency 2"]
}
```

When Banguat has no rate published for today (rare, but possible), the response has no `date`/`buy`/`sell`/`help` fields at all - just:

```json
{ "currency": 2, "currencyAlias": "USD", "count": 0 }
```

Check for `count` before assuming `buy`/`sell` are present.

## `rate history`

Rate history either from a date to today, or over a bounded range.

```
banguat-exchangerates-cli rate history --since <yyyy-MM-dd> [--currency <id|alias>] [--output plain|rich|json]
banguat-exchangerates-cli rate history --from <yyyy-MM-dd> --to <yyyy-MM-dd> [--currency <id|alias>] [--output plain|rich|json]
```

`--since` and `--from`/`--to` are mutually exclusive - provide one or the other, not both, not neither; the CLI rejects either mistake with an error rather than guessing your intent.

JSON shape:

```json
{
  "currency": 2,
  "currencyAlias": "USD",
  "count": 2,
  "history": [
    { "date": "2026-09-06", "buy": 7.62635, "sell": 7.62635 },
    { "date": "2026-09-07", "buy": 7.6222, "sell": 7.6222 }
  ]
}
```

A range with no published rates in it comes back the same shape with `"count": 0` and `"history": []` - not an error, just an empty result. This is the one command where "no data" and "success" look almost identical in JSON mode, so check `count` rather than the exit code alone if you need to distinguish "nothing published in this range" from "I got data."

## Currency resolution

`--currency` accepts:

1. A raw Banguat numeric code (e.g. `24` for Euro).
2. A known alias, case-insensitive (e.g. `USD`, `eur`, `Jpy`). Check `known-currencies.md` in this skill first - it's generated from the same bundled catalog this CLI actually resolves against, so it's always accurate for the build you're running. Only fall back to `currencies` if the currency isn't listed there; roughly a quarter of Banguat's currencies have no bundled alias, and there's no way to guess which in advance.

An unrecognized alias fails with a structured error, plus a "did you mean" suggestion when your input is close to a real alias (e.g. `--currency USB` suggests `USD`).

## `skill install`

Installs (or re-installs) this skill for Claude Code:

```
banguat-exchangerates-cli skill install --scope local|user|both
```

`local` writes to the current directory's `.claude/skills/`, `user` to the home directory's, default is `local`. You won't normally need to run this yourself - it's how this skill got here, and it's what a human would re-run after upgrading the CLI to refresh this content.

## Errors

Every command fails the same way: one message plus a non-zero exit code. In `--output json` mode the message is `{ "error": "<message>" }` on stdout; in `plain`/`rich` it's a plain or red-highlighted line on stdout. There is never a stack trace for an expected failure (bad input, no data for a date) - only for a genuinely unhandled exception, which would indicate a bug in the CLI itself rather than a usage mistake.

Example, an unrecognized currency in JSON mode:

```json
{ "error": "Unknown currency 'USB'. Did you mean: USD? Run 'currencies' to see all codes." }
```
