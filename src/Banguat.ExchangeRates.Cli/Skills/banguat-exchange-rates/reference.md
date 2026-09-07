# Command reference

## Global option

- `--output`, `-o` - `plain` | `rich` | `json`. Defaults to `rich`. `json` is the safest choice when parsing output programmatically.

## `currencies`

Lists every currency Banguat publishes rates for.

```
currencies [--output plain|rich|json]
```

JSON shape:

```json
{
  "count": 42,
  "currencies": [
    { "code": 1, "description": "Quetzales", "alias": "GTQ" },
    { "code": 2, "description": "Dólares de EE.UU.", "alias": "USD" }
  ],
  "help": ["rate --currency <id|alias>", "rate history --since <date> --currency <id|alias>"]
}
```

## `rate`

Today's buy/sell rate for one currency.

```
rate [--currency <id|alias>] [--output plain|rich|json]
```

`--currency` defaults to `2` (USD) when omitted.

JSON shape:

```json
{
  "date": "2026-09-07",
  "currency": 2,
  "currencyAlias": "USD",
  "buy": 7.6215,
  "sell": 7.6217,
  "help": ["rate history --since <date> --currency 2"]
}
```

## `rate history`

Rate history either from a date to today, or over a bounded range.

```
rate history --since <yyyy-MM-dd> [--currency <id|alias>] [--output plain|rich|json]
rate history --from <yyyy-MM-dd> --to <yyyy-MM-dd> [--currency <id|alias>] [--output plain|rich|json]
```

`--since` and `--from`/`--to` are mutually exclusive - provide one or the other, not both, not neither.

JSON shape:

```json
{
  "currency": 2,
  "currencyAlias": "USD",
  "count": 2,
  "history": [
    { "date": "2026-09-05", "buy": 7.6201, "sell": 7.6203 },
    { "date": "2026-09-06", "buy": 7.6215, "sell": 7.6217 }
  ]
}
```

## Currency resolution

`--currency` accepts:

1. A raw Banguat numeric code (e.g. `24`).
2. A known alias, case-insensitive (e.g. `USD`, `EUR`). Run `currencies` to see which codes have one.

An unrecognized alias fails with a structured error, plus a "did you mean" suggestion when one is close.

## Errors

Every command fails the same way: a message plus a non-zero exit code. In `--output json` mode the message is `{ "error": "<message>" }` on stdout; in `plain`/`rich` it's a plain or red-highlighted line on stdout. There is never a stack trace for an expected failure (bad input, no data for a date) - only for a genuinely unhandled exception.
