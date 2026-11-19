// The local topology (PLAN §7.1). Resource names are the connection names the services read:
// jobs / fetching / knowledge (Postgres databases), bus (RabbitMQ), cache (Valkey), blobs (Azurite), and the
// service names used by service discovery (knowledge is called by synthesis). Resource names must be unique, so the
// databases are named <service>-db and handed to each service under its connection name.

var builder = DistributedApplication.CreateBuilder(args);

// Secrets: set with `dotnet user-secrets set Parameters:anthropic-api-key <key>` in this project, or the
// Parameters__anthropic-api-key environment variable. Aspire prompts for it in the dashboard when missing.
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);

// Optional: needed only when Planning or Synthesis sets Llm:Provider to OpenAI. Set it with
// `dotnet user-secrets set Parameters:openai-api-key <key>`; while it is unset, nothing asks for it.
var openAIApiKey = string.IsNullOrEmpty(builder.Configuration["Parameters:openai-api-key"])
    ? null
    : builder.AddParameter("openai-api-key", secret: true);

// Infrastructure ----------------------------------------------------------------------------------------------------

// Aspire starts each container on its own; Podman Desktop and Docker Desktop group containers by this compose label.
const string ContainerGroup = "com.docker.compose.project=research";

var pg = builder.AddPostgres("pg")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume()
    .WithContainerRuntimeArgs("--label", ContainerGroup);

var jobsDb = pg.AddDatabase("jobs-db", "research_jobs");
var fetchingDb = pg.AddDatabase("fetching-db", "research_fetching");
var knowledgeDb = pg.AddDatabase("knowledge-db", "research_knowledge");

var bus = builder.AddRabbitMQ("bus")
    .WithManagementPlugin()
    .WithDataVolume()
    .WithContainerRuntimeArgs("--label", ContainerGroup);

var cache = builder.AddValkey("cache")
    .WithDataVolume()
    .WithContainerRuntimeArgs("--label", ContainerGroup);

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(static emulator => emulator
        .WithDataVolume()
        .WithContainerRuntimeArgs("--label", ContainerGroup))
    .AddBlobs("blobs");

// Schema scripts: run once, to completion, before any service that owns a database starts --------------------------

var dbScripts = builder.AddProject<Projects.Research_DbScripts>("db-scripts")
    .WithReference(jobsDb, connectionName: "jobs").WaitFor(jobsDb)
    .WithReference(fetchingDb, connectionName: "fetching").WaitFor(fetchingDb)
    .WithReference(knowledgeDb, connectionName: "knowledge").WaitFor(knowledgeDb);

// Services ----------------------------------------------------------------------------------------------------------

builder.AddProject<Projects.Research_Jobs_Host>("jobs")
    .WithExternalHttpEndpoints()
    .WithReference(jobsDb, connectionName: "jobs").WaitFor(jobsDb)
    .WithReference(bus).WaitFor(bus)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

var planning = builder.AddProject<Projects.Research_Planning_Host>("planning")
    .WithReference(bus).WaitFor(bus)
    .WithReference(cache)
    .WithEnvironment("Anthropic__ApiKey", anthropicApiKey)
    .WithReplicas(2);

builder.AddProject<Projects.Research_Search_Host>("search")
    .WithReference(bus).WaitFor(bus)
    .WithReference(cache)
    .WithEnvironment("AnthropicWebSearch__ApiKey", anthropicApiKey)
    .WithReplicas(3);

builder.AddProject<Projects.Research_Fetching_Host>("fetching")
    .WithReference(fetchingDb, connectionName: "fetching").WaitFor(fetchingDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(3);

var knowledge = builder.AddProject<Projects.Research_Knowledge_Host>("knowledge")
    .WithReference(knowledgeDb, connectionName: "knowledge").WaitFor(knowledgeDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

var synthesis = builder.AddProject<Projects.Research_Synthesis_Host>("synthesis")
    .WithReference(bus).WaitFor(bus)
    .WithReference(knowledge)
    .WithEnvironment("Anthropic__ApiKey", anthropicApiKey)
    .WithReplicas(2);

if (openAIApiKey is not null)
{
    planning.WithEnvironment("OpenAI__ApiKey", openAIApiKey);
    synthesis.WithEnvironment("OpenAI__ApiKey", openAIApiKey);
}

await builder.Build().RunAsync();
