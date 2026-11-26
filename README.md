# Banguat exchange rates, as a library, a CLI + skill and an MCP server

Banguat (Guatemala's central bank) publishes exchange rates over an old SOAP service. This repo wraps it in a
.NET library and exposes it to AI agents two ways, so the two approaches can be compared side by side:

- **CLI + skill**: `Banguat.ExchangeRates.Cli` plus the `banguat-exchange-rates` skill it installs for Claude Code.
- **MCP server**: `Banguat.ExchangeRates.McpServer`, over stdio (local) or HTTP (remote, behind a YARP gateway).
- **REST API**: `Banguat.ExchangeRates.Api`, a plain HTTP API for comparison, behind the same gateway.

It was built for the talk "MCP no es la unica respuesta: CLI + Skills vs MCP para agentes de IA en .NET".

## What's in here

| Project | What it is |
|---|---|
| `Banguat.ExchangeRates` | The library: SOAP transport, parsing, caching, tracing. Both front ends use it. |
| `Banguat.ExchangeRates.Cli` | The CLI and the skill it embeds (`skill install`). |
| `Banguat.ExchangeRates.McpServer` | The MCP server with `get_currencies`, `get_rate` and `get_rate_history`. |
| `Banguat.ExchangeRates.Api` | A slim REST API (`/v1/currencies`, `/v1/rates/{currency}`, `/v1/rates/{currency}/history`). |
| `Banguat.ExchangeRates.Gateway` | YARP in front of the other two: `/api` goes to the Api, `/mcp` to the MCP server. |
| `Banguat.ExchangeRates.AppHost` | Aspire: runs everything locally and publishes to Azure Container Apps (see `deploy/`). |

## Try it

Build once first (`dotnet build Banguat.ExchangeRates.slnx`). The `--no-build` flags matter for stdio: a build
step can print to stdout and corrupt the MCP stream.

**CLI**

```bash
dotnet run --no-build --project src/Banguat.ExchangeRates.Cli -- rate --currency usd -o json
dotnet run --no-build --project src/Banguat.ExchangeRates.Cli -- skill install --scope local
```

**MCP over stdio (local)**

```bash
claude mcp add banguat -- dotnet run --no-build --project src/Banguat.ExchangeRates.McpServer -- --stdio
```

**MCP over HTTP (remote shape)**

```bash
aspire run
```

Then point an MCP client at the gateway's `/mcp` URL (`http://localhost:5274/mcp` with the default launch
settings), or call the API at `http://localhost:5274/api/v1/rates/usd`. The Aspire dashboard shows the traces for MCP calls, and for CLI runs too if you set
`OTEL_EXPORTER_OTLP_ENDPOINT` to the dashboard's OTLP endpoint.

## Tests

```bash
dotnet test Banguat.ExchangeRates.slnx
```
