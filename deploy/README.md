# Deploying the Banguat.ExchangeRates demo to a VPS

This guide deploys the solution (REST API, MCP server, and the YARP gateway, with
a Valkey cache) to a single VPS using Docker Compose, behind a Caddy TLS edge that
obtains and renews a trusted Let's Encrypt certificate automatically.

The CLI is distributed and run separately (it talks to the deployed API); it is not
part of the container stack.

```
  Internet --443 TLS--> [ Caddy ]  (Let's Encrypt, auto-renew)
                           | http
                           v
                       [ gateway (YARP) ] --/api--> [ api ]  --+--> [ cache ]
                           |                --/mcp--> [ mcpserver ] --/
                           (only Caddy is published to the public interface)
```

## 1. Prerequisites

| Requirement | Detail |
|-------------|--------|
| **Domain** | A registered domain or subdomain for the demo, e.g. `demo.example.com`. Let's Encrypt (HTTP-01) will **not** issue a certificate for a bare IP address. |
| **DNS** | An `A` record for that domain pointing at the VPS public IPv4 address. (Add an `AAAA` record too if the VPS has IPv6.) Verify with `dig +short demo.example.com` before deploying. |
| **VPS size** | ~2 GB RAM tier recommended (DigitalOcean / Vultr class). The stack runs five containers (caddy, gateway, api, mcpserver, cache) plus the Aspire dashboard; the ~1 GB tier is tight and can OOM. Exact pricing changes — check the provider's current pricing page. |
| **Open ports** | Inbound TCP **80** and **443** open on the VPS firewall / security group. Port 80 is required for the ACME HTTP-01 challenge and the HTTP→HTTPS redirect. |
| **Container runtime** | Docker Engine >= 28 with the Compose v2 plugin (`docker compose version`). |
| **Build host** | A machine with the .NET 10 SDK and the Aspire CLI to build images and run `aspire publish`/`aspire deploy` (can be the VPS or your laptop). |

## 2. Generate the deployment artifacts

From the repo root on the build host:

```bash
cd src/Banguat.ExchangeRates.AppHost
aspire publish --non-interactive -o aspire-output
```

This generates (from the AppHost model — do not hand-edit these):

- `aspire-output/docker-compose.yaml` — services for cache, mcpserver, api, gateway.
- `aspire-output/.env` — placeholder image/port/password values.

Only the `gateway` service is host-published in the generated file; `api`,
`mcpserver`, and `cache` are internal to the compose network.

## 3. Build and resolve images

The recommended path is Aspire's own deploy pipeline, which builds the project
images, resolves the `.env` values, and runs compose:

```bash
cd src/Banguat.ExchangeRates.AppHost
aspire deploy --non-interactive
```

`aspire deploy` builds `api`, `gateway`, and `mcpserver` images and (if a
container registry is configured) pushes them. For a single self-contained VPS
with no registry, you can instead build images locally with the .NET SDK's
built-in container support and run compose directly — see
[Appendix A](#appendix-a-registry-free-local-images).

## 4. Add the Caddy TLS edge and bring the stack up

The Caddy edge is layered on top of the generated compose via the overlay in
`deploy/`. It terminates HTTPS, redirects HTTP→HTTPS, and reverse-proxies the
single gateway upstream.

Set the domain and ACME contact, then bring everything up from the repo root:

```bash
export DEMO_DOMAIN=demo.example.com
export ACME_EMAIL=you@example.com

docker compose \
  --project-directory . \
  -f src/Banguat.ExchangeRates.AppHost/aspire-output/docker-compose.yaml \
  -f deploy/docker-compose.caddy.yml \
  --env-file src/Banguat.ExchangeRates.AppHost/aspire-output/.env \
  up -d
```

Notes:
- `--project-directory .` makes the relative `deploy/Caddyfile` mount resolve
  correctly regardless of where the generated compose file lives.
- The overlay pins the gateway and Aspire dashboard host ports to `127.0.0.1`,
  so **only Caddy is reachable from the public internet**. Reach the dashboard
  over an SSH tunnel: `ssh -L 18888:127.0.0.1:18888 user@vps`, then open
  `http://localhost:18888`.
- Caddy persists issued certificates in the `caddy_data` volume, so restarts do
  not re-request certificates.

## 5. Verify

```bash
# From anywhere on the internet (trusted cert, no -k needed):
curl -s https://$DEMO_DOMAIN/api/v1/currencies | head -c 200
curl -s -X POST https://$DEMO_DOMAIN/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"verify","version":"1.0"}}}'
```

Both should return HTTP 200 over HTTPS. Point the CLI / your LLM client at
`https://$DEMO_DOMAIN`.

## 6. Pre-demo checklist (do NOT skip before a live talk)

- [ ] DNS `A` record resolves to the VPS (`dig +short $DEMO_DOMAIN`) — do this
      hours ahead; DNS can take time to propagate.
- [ ] Bring the stack up and **let Caddy issue the certificate before the talk**.
      First issuance hits Let's Encrypt live; never let the first issuance happen
      during your session. Confirm `https://$DEMO_DOMAIN/api/...` serves with a
      trusted cert (no browser warning).
- [ ] **Warm the cache**: call a couple of endpoints so rates are cached in
      Valkey. This shields the live demo from a slow Banguat SOAP upstream.
- [ ] Verify the MCP endpoint responds and your LLM client is configured for
      `https://$DEMO_DOMAIN/mcp`.
- [ ] Confirm Let's Encrypt rate limits are not a risk: if you redeploy
      repeatedly while testing, use the staging ACME endpoint (see Appendix B)
      to avoid burning the production certificate quota.
- [ ] Leave the stack running; do not `down` it right before the talk.

## 7. Teardown

```bash
docker compose \
  --project-directory . \
  -f src/Banguat.ExchangeRates.AppHost/aspire-output/docker-compose.yaml \
  -f deploy/docker-compose.caddy.yml \
  down
# add -v to also remove the cache and Caddy certificate volumes
```

---

## Appendix A: registry-free local images

For a single VPS with no container registry, build the three project images
locally with the .NET SDK container support and supply a filled-in env file:

```bash
# from the repo root
for proj in Api Gateway McpServer; do
  name=$(echo "$proj" | tr '[:upper:]' '[:lower:]')
  dotnet publish "src/Banguat.ExchangeRates.$proj/Banguat.ExchangeRates.$proj.csproj" \
    -c Release /t:PublishContainer \
    -p:ContainerRepository="banguat-$name" -p:ContainerImageTag=local
done
```

Create `src/Banguat.ExchangeRates.AppHost/aspire-output/.env.vps`:

```bash
API_IMAGE=banguat-api:local
API_PORT=8080
GATEWAY_IMAGE=banguat-gateway:local
GATEWAY_PORT=8080
MCPSERVER_IMAGE=banguat-mcpserver:local
MCPSERVER_PORT=8080
CACHE_PASSWORD=<choose-a-strong-password>
```

Then run the `up` command from section 4 with
`--env-file src/Banguat.ExchangeRates.AppHost/aspire-output/.env.vps`.
(When building on your laptop, `docker save`/`docker load` or a registry is
needed to move the images to the VPS; building directly on the VPS avoids that.)

## Appendix B: Let's Encrypt staging while testing

To avoid production rate limits while iterating, point Caddy at the ACME staging
CA by adding to the global block in `deploy/Caddyfile`:

```caddyfile
{
	email {$ACME_EMAIL}
	acme_ca https://acme-staging-v02.api.letsencrypt.org/directory
}
```

Staging certificates are **not** trusted by browsers (expect a warning); remove
this line for the real demo and clear the `caddy_data` volume so a trusted
certificate is requested.
