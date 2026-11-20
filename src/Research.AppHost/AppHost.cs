// The local topology (PLAN §7.1). Resource names are the connection names the services read:
// jobs / fetching / knowledge (Postgres databases), bus (RabbitMQ), cache (Valkey), blobs (Azurite), elasticsearch
// (logs), and the service names used by service discovery (knowledge is called by synthesis). Resource names must be
// unique, so the databases are named <service>-db and handed to each service under its connection name.

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

// Logs: every service also sends its logs to Elasticsearch, and Kibana shows them (Discover, data view `logs-*`). Sign
// in to Kibana as `elastic` with the elasticsearch-password parameter (the dashboard shows it). Both passwords are
// generated once and kept in user secrets, without special characters, so they are safe in URLs, JSON and shells.
var elasticPassword = builder.AddParameter(
    "elasticsearch-password", new GenerateParameterDefault { MinLength = 22, Special = false }, secret: true, persist: true);
var kibanaPassword = builder.AddParameter(
    "kibana-password", new GenerateParameterDefault { MinLength = 22, Special = false }, secret: true, persist: true);

var elasticsearch = builder.AddElasticsearch("elasticsearch", elasticPassword)
    .WithDataVolume()
    .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithContainerRuntimeArgs("--label", ContainerGroup);

// Kibana may not sign in to Elasticsearch as the `elastic` superuser, so this one-shot container first gives the
// built-in kibana_system user its password.
var kibanaSetup = builder.AddContainer("kibana-setup", "docker.io/curlimages/curl", "8.17.0")
    .WithEntrypoint("sh")
    .WithArgs("-c", """
        curl -fsS --retry 10 --retry-all-errors --retry-delay 3 -u "elastic:$ELASTIC_PASSWORD" \
          -H "Content-Type: application/json" -X POST "$ES_URL/_security/user/kibana_system/_password" \
          -d "{\"password\":\"$KIBANA_PASSWORD\"}"
        """)
    .WithEnvironment("ES_URL", elasticsearch.GetEndpoint("http"))
    .WithEnvironment("ELASTIC_PASSWORD", elasticPassword)
    .WithEnvironment("KIBANA_PASSWORD", kibanaPassword)
    .WaitFor(elasticsearch)
    .WithContainerRuntimeArgs("--label", ContainerGroup);

builder.AddContainer("kibana", "docker.io/library/kibana", "8.17.3")
    .WithHttpEndpoint(targetPort: 5601, name: "http")
    .WithEnvironment("ELASTICSEARCH_HOSTS", elasticsearch.GetEndpoint("http"))
    .WithEnvironment("ELASTICSEARCH_USERNAME", "kibana_system")
    .WithEnvironment("ELASTICSEARCH_PASSWORD", kibanaPassword)
    .WaitForCompletion(kibanaSetup)
    .WithContainerRuntimeArgs("--label", ContainerGroup);

// Schema scripts: run once, to completion, before any service that owns a database starts --------------------------

var dbScripts = builder.AddProject<Projects.Research_DbScripts>("db-scripts")
    .WithReference(jobsDb, connectionName: "jobs").WaitFor(jobsDb)
    .WithReference(fetchingDb, connectionName: "fetching").WaitFor(fetchingDb)
    .WithReference(knowledgeDb, connectionName: "knowledge").WaitFor(knowledgeDb);

// Services ----------------------------------------------------------------------------------------------------------

builder.AddProject<Projects.Research_Jobs_Host>("jobs")
    .WithReference(elasticsearch)
    .WithExternalHttpEndpoints()
    .WithReference(jobsDb, connectionName: "jobs").WaitFor(jobsDb)
    .WithReference(bus).WaitFor(bus)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

var planning = builder.AddProject<Projects.Research_Planning_Host>("planning")
    .WithReference(elasticsearch)
    .WithReference(bus).WaitFor(bus)
    .WithReference(cache)
    .WithEnvironment("Anthropic__ApiKey", anthropicApiKey)
    .WithReplicas(2);

builder.AddProject<Projects.Research_Search_Host>("search")
    .WithReference(elasticsearch)
    .WithReference(bus).WaitFor(bus)
    .WithReference(cache)
    .WithEnvironment("AnthropicWebSearch__ApiKey", anthropicApiKey)
    .WithReplicas(3);

builder.AddProject<Projects.Research_Fetching_Host>("fetching")
    .WithReference(elasticsearch)
    .WithReference(fetchingDb, connectionName: "fetching").WaitFor(fetchingDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(3);

var knowledge = builder.AddProject<Projects.Research_Knowledge_Host>("knowledge")
    .WithReference(elasticsearch)
    .WithReference(knowledgeDb, connectionName: "knowledge").WaitFor(knowledgeDb)
    .WithReference(bus).WaitFor(bus)
    .WithReference(blobs).WaitFor(blobs)
    .WaitForCompletion(dbScripts)
    .WithReplicas(2);

var synthesis = builder.AddProject<Projects.Research_Synthesis_Host>("synthesis")
    .WithReference(elasticsearch)
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
