// The local topology (PLAN §7.1). Resource names are the connection names the services read:
// jobs / fetching / knowledge (Postgres databases), bus (RabbitMQ), cache (Valkey), blobs (Azurite), and the
// service names used by service discovery (knowledge is called by synthesis).

var builder = DistributedApplication.CreateBuilder(args);

// Secrets: set with `dotnet user-secrets set Parameters:anthropic-api-key <key>` in this project, or the
// Parameters__anthropic-api-key environment variable. Aspire prompts for it in the dashboard when missing.
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);

// Infrastructure ----------------------------------------------------------------------------------------------------

var pg = builder.AddPostgres("pg")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume();

var jobsDb = pg.AddDatabase("jobs", "research_jobs");
var fetchingDb = pg.AddDatabase("fetching", "research_fetching");
var knowledgeDb = pg.AddDatabase("knowledge", "research_knowledge");

var bus = builder.AddRabbitMQ("bus")
    .WithManagementPlugin()
    .WithDataVolume();

var cache = builder.AddValkey("cache")
    .WithDataVolume();

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(static emulator => emulator.WithDataVolume())
    .AddBlobs("blobs");

// Schema scripts: run once, to completion, before any service that owns a database starts --------------------------

var dbScripts = builder.AddProject<Projects.Research_DbScripts>("db-scripts")
    .WithReference(jobsDb).WaitFor(jobsDb)
    .WithReference(fetchingDb).WaitFor(fetchingDb)
    .WithReference(knowledgeDb).WaitFor(knowledgeDb);

// Services ----------------------------------------------------------------------------------------------------------

builder.AddProject<Projects.Research_Jobs_Host>("jobs")
    .WithExternalHttpEndpoints()
    .WithReference(jobsDb).WaitFor(jobsDb)
    .WithReference(bus).WaitFor(bus)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

builder.AddProject<Projects.Research_Planning_Host>("planning")
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
    .WithReference(fetchingDb).WaitFor(fetchingDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(3);

var knowledge = builder.AddProject<Projects.Research_Knowledge_Host>("knowledge")
    .WithReference(knowledgeDb).WaitFor(knowledgeDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

builder.AddProject<Projects.Research_Synthesis_Host>("synthesis")
    .WithReference(bus).WaitFor(bus)
    .WithReference(knowledge)
    .WithEnvironment("Anthropic__ApiKey", anthropicApiKey)
    .WithReplicas(2);

await builder.Build().RunAsync();
