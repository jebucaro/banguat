# Deploying the Banguat.ExchangeRates demo to Azure Container Apps

The AppHost publishes to Azure Container Apps. The gateway (YARP) is the only public
service; the API, MCP server and Valkey cache stay internal to the environment. TLS is
handled by Container Apps ingress, on the gateway's `*.azurecontainerapps.io` host.

The CLI is distributed and run separately (it talks to the deployed API); it is not
part of the deployment.

```
  Internet --443 TLS--> [ gateway ingress (ACA, managed cert) ]
                           |
                       [ gateway (YARP) ] --/api--> [ api ]       --+--> [ cache ]
                                          --/mcp--> [ mcpserver ] --/
                       (api, mcpserver and cache have internal ingress only)
```

## Who owns what

The Azure side is split on purpose. This repo never creates or deletes the resource
group or the registry, and never holds Azure names or IDs.

| Owner | What |
|---|---|
| **Azure org setup** (OpenTofu, separate repo) | the resource group, its budget and access, the shared container registry, provider registration |
| **This repo** (`aspire deploy`) | everything *inside* the resource group: Container Apps environment, Log Analytics, managed identity, the four container apps, plus AcrPull for that identity on the shared registry |
| **Your machine** (user secrets) | subscription, region, resource group and registry names |

## 1. Prerequisites

| Requirement | Detail |
|-------------|--------|
| **Resource group + registry** | Created by the Azure org setup. Ask its owner (or run `tofu output` there) for the resource group name and the `container_registry` name / resource group. |
| **Azure CLI (`az`)** | Used to sign in; `aspire deploy` uses your `az login`. You need Owner on the resource group and AcrPush on the registry. |
| **Aspire CLI** | 13.6+ (`aspire --version`). No `azd` needed. |
| **Bicep** | Run `az bicep install` once before the first deploy. Otherwise `az` downloads it mid-deploy while two steps run in parallel, and one of them dies with `Text file busy`. |
| **.NET 10 SDK + Docker** | To build the three project images. |

> `az` is the general Azure CLI (sign in, inspect, delete resources). `azd` (Azure
> Developer CLI) is a separate app-deployment tool; this repo doesn't use it, because
> `aspire deploy` does the same job and `azd` would re-declare the resource group.

## 2. One-time setup (values stay on your machine)

```bash
az login            # pick the account that has access to the resource group
cd src/Banguat.ExchangeRates.AppHost

dotnet user-secrets set "Azure:SubscriptionId" "<subscription id>"
dotnet user-secrets set "Azure:Location" "<region, e.g. eastus2>"
dotnet user-secrets set "Azure:ResourceGroup" "<resource group>"
dotnet user-secrets set "Azure:AllowResourceGroupCreation" "false"   # never create a stray group
dotnet user-secrets set "Parameters:acrName" "<shared registry name>"
dotnet user-secrets set "Parameters:acrResourceGroup" "<registry's resource group>"
```

User secrets live under `~/.microsoft/usersecrets/`, outside the repo. If any value is
missing, `aspire deploy` asks for it interactively.

## 3. Check the generated artifacts (optional)

```bash
aspire publish -o aspire-output      # Bicep, generated from AppHost.cs; don't hand-edit
```

- `acr/acr.bicep` references the registry as `existing` (no registry is created).
- Only `gateway/gateway.bicep` has `external: true` ingress.
- `api`, `mcpserver`, `gateway`: `minReplicas: 0`; `cache`: exactly 1 replica.

## 4. Deploy

```bash
aspire deploy
```

This builds and pushes the api, gateway and mcpserver images to the shared registry and
deploys everything into the resource group. At the end it prints the gateway URL
(`https://gateway.<env>.<region>.azurecontainerapps.io`).

Afterwards, a `tofu plan` in the Azure org repo should still say **No changes** (the
deploy must not change the resource group or its tags).

## 5. Verify

```bash
export DEMO_HOST=<gateway host from the deploy output>

curl -s https://$DEMO_HOST/api/v1/currencies | head -c 200
curl -s -X POST https://$DEMO_HOST/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"verify","version":"1.0"}}}'
```

Both should return HTTP 200 over HTTPS with no certificate warning. Point the CLI / your
LLM client at `https://$DEMO_HOST`.

## 6. Cost and the pre-demo checklist

Sizes are set in `AppHost.cs` (0.25 vCPU / 0.5 GiB each). The HTTP apps scale to zero,
so they cost almost nothing when idle. The cache can't (TCP ingress doesn't wake from
zero), so it runs one replica all the time: roughly **$4/month** while deployed. Deploy
shortly before the demo and tear down right after.

- [ ] Deploy at least a day ahead and run the checks in section 5.
- [ ] A minute before the talk, call `/api/...` and `/mcp` once each so no surface is
      cold-starting live, and so rates are cached in Valkey (shields the demo from a
      slow Banguat SOAP upstream).
- [ ] Optional: zero cold-start risk by setting `minReplicas: 1` on the HTTP apps in
      `AppHost.cs` and redeploying (small idle cost while it runs).

## 7. Tear-down (keeps the resource group)

> **Don't run `aspire destroy` or `azd down`.** Both delete the whole resource group,
> which belongs to the Azure org setup (its budget and access go with it).

Delete what's *inside* the group instead:

```bash
RG=<resource group>
ACR_ID=$(az acr show -n <shared registry name> --query id -o tsv)

# 1. Remove this deployment's pull access on the shared registry
for p in $(az identity list -g $RG --query "[].principalId" -o tsv); do
  az role assignment delete --assignee "$p" --scope "$ACR_ID"
done

# 2. Container apps first, then everything else (environment, logs, identity)
az containerapp list -g $RG --query "[].id" -o tsv | xargs -r az containerapp delete --yes --ids
az resource list -g $RG --query "[].id" -o tsv | xargs -r az resource delete --ids

# 3. Must print nothing
az resource list -g $RG -o table
```

Optionally delete the images too: `az acr repository list -n <shared registry name>`,
then `az acr repository delete -n <shared registry name> --repository <name> --yes` for
this project's repositories (api, gateway, mcpserver).

Retiring the project for good (deleting the resource group, budget and access) is done
from the Azure org setup, after the group is empty.
