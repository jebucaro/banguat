using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.OperationalInsights;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// The resource group and registry belong to azure-org, we only point at them. Names come from user
// secrets so nothing about the tenant lands in git, and `aspire run` never looks at any of it.
IResourceBuilder<ParameterResource> acrName = builder.AddParameter("acrName");
IResourceBuilder<ParameterResource> acrResourceGroup = builder.AddParameter("acrResourceGroup");

IResourceBuilder<AzureContainerRegistryResource> acr = builder.AddAzureContainerRegistry("acr")
    .PublishAsExisting(acrName, acrResourceGroup);

builder.AddAzureContainerAppEnvironment("aca")
    .WithAzureContainerRegistry(acr)
    .ConfigureInfrastructure(infra =>
    {
        // An uncapped workspace is the easiest way to blow the budget, so same cap as the org one.
        OperationalInsightsWorkspace logs = infra.GetProvisionableResources().OfType<OperationalInsightsWorkspace>()
            .Single();
        logs.RetentionInDays = 30;
        logs.WorkspaceCapping = new OperationalInsightsWorkspaceCapping { DailyQuotaInGB = 0.1 };
    });

// TCP ingress can't wake Valkey from zero, so it's pinned to one, and a second replica would just be
// a second empty cache.
IResourceBuilder<ValkeyResource> cache = builder.AddValkey("cache")
    .PublishAsAzureContainerApp((_, app) => Size(app, 1, 1));

// These sleep when nobody's calling, so hit them once before the demo or the first request is slow.
IResourceBuilder<ProjectResource> mcpserver = builder.AddProject<Projects.Banguat_ExchangeRates_McpServer>("mcpserver")
    .WithReference(cache)
    .PublishAsAzureContainerApp((_, app) => Size(app, 0));

IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.Banguat_ExchangeRates_Api>("api")
    .WithReference(cache)
    .PublishAsAzureContainerApp((_, app) => Size(app, 0));

builder.AddProject<Projects.Banguat_ExchangeRates_Gateway>("gateway")
    .WithReference(api)
    .WithReference(mcpserver)
    .WithExternalHttpEndpoints()
    .PublishAsAzureContainerApp((_, app) => Size(app, 0));

await builder.Build().RunAsync();

// Aspire's defaults come out around $40 a month here, this keeps it close to the free grant.
static void Size(ContainerApp app, int minReplicas, int maxReplicas = 2)
{
    app.Template.Scale.MinReplicas = minReplicas;
    app.Template.Scale.MaxReplicas = maxReplicas;

    ContainerAppContainer container = app.Template.Containers.Single().Value!;
    container.Resources.Cpu = 0.25;
    container.Resources.Memory = "0.5Gi";
}