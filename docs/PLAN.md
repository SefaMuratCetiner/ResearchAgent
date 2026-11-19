# Research Agent V3 — Architecture and Implementation Plan

A .NET research agent built as microservices on .NET Aspire, following the `dotnet-coding-standards`
composition-root standard (with the departures recorded in §2) and the `dotnet-observability` skill
(without redaction). Goals: horizontally scalable, highly available, fault tolerant.

Status: **implemented, never compiled.** All services, the AppHost, the schema tool, the tests (unit,
architecture, integration, end-to-end) and the CI/CD workflows are written. None of it has been restored, built or
run yet. §23 lists where the code departs from this plan.

---

## Contents

1. [Scope and goals](#1-scope-and-goals)
2. [Decisions and departures from the standard](#2-decisions-and-departures-from-the-standard)
3. [Service topology](#3-service-topology)
4. [Folder structure](#4-folder-structure)
5. [Project reference rules](#5-project-reference-rules)
6. [Host shape and composition root](#6-host-shape-and-composition-root)
7. [Aspire: AppHost and service defaults](#7-aspire-apphost-and-service-defaults)
8. [Design patterns](#8-design-patterns)
9. [Microservices patterns](#9-microservices-patterns)
10. [Job flow](#10-job-flow)
11. [Messaging](#11-messaging)
12. [Database architecture](#12-database-architecture)
13. [Worker design](#13-worker-design)
14. [Errors and error codes](#14-errors-and-error-codes)
15. [Observability](#15-observability)
16. [Scalability, availability, fault tolerance](#16-scalability-availability-fault-tolerance)
17. [Edge cases](#17-edge-cases)
18. [Testing](#18-testing)
19. [Deployment](#19-deployment)
20. [Packages](#20-packages)
21. [Implementation phases](#21-implementation-phases)
22. [Open questions](#22-open-questions)
23. [Implementation deviations](#23-implementation-deviations)

---

## 1. Scope and goals

**In scope:** the research pipeline services, their shared libraries, the Aspire AppHost, databases, messaging,
observability, tests and deployment manifests.

**Out of scope:** the API gateway. One exists in front of the system and is owned elsewhere; we neither build
nor configure it, and the services make **no assumptions about it**: no authentication, no authorization, no
tenancy and no forwarded-header handling in our services (D20). The only things we read from incoming requests
are the standard W3C trace headers (`traceparent`, `tracestate`, `baggage`, handled by OpenTelemetry) and an
optional `X-Correlation-ID`.

**Current runtime scope:** local orchestration with .NET Aspire only. Production hosting (§19) is kept as the
target design but is not built now.

**Non-functional targets**

| Target | Value |
| --- | --- |
| Jobs API availability | 99.9% / 30 days |
| Jobs API latency | p95 < 300 ms for `POST` and `GET` |
| Job success | 99% of jobs complete (business failures excluded) |
| Job duration | p95 under `Jobs:MaxDuration` (default 5 min) |
| Minimum replicas | 2 per service, spread across zones |
| Data loss | none for committed job state under the failures listed in §16.1; anything outside that list is stated there with its recovery point |

---

## 2. Decisions and departures from the standard

Each departure below must also be recorded in `docs/architecture.md` and reflected in the convention tests.

| # | Decision | Standard says | Reason |
| --- | --- | --- | --- |
| D1 | `Research.SharedKernel` + `Research.Contracts` instead of `Research.Abstractions` | One `<App>.Abstractions` | Wire contracts (versioned, must not break running consumers) change under different rules than in-process shared code. Both have **no package references**. |
| D2 | Projects nested by service (`src/Jobs/…`), plus `Shared/`, `Providers/`, `.slnf` filters | Flat `src/` | Keeps each service together; filters let a developer open one service. One `.slnx` still holds everything. |
| D3 | One `*.Api` project per HTTP service, plus shared `Research.Http` | One `<App>.Api` | Services deploy independently. |
| D4 | `Research.Problems` as its own project | Problem writer inside `<App>.Api` | Shared by every HTTP service, kept apart from other HTTP plumbing. |
| D5 | Platform Feature classes are `public` in `Research.Hosting` | Features are `internal` in `Host/Features/` | Seven Hosts would otherwise copy the same platform features. Capability features stay `internal` in each Host. |
| D6 | **No Aspire `ServiceDefaults` project.** Its content (OpenTelemetry, health endpoints, service discovery, standard HTTP resilience) is expressed as Feature classes in `Research.Hosting` and listed in every composition root. | Libraries have no `AddX` extensions | Keeps one wiring style; every registration is findable in a feature list. See §7.2. |
| D7 | **Aspire client integrations are used as-is**, including their built-in health checks, tracing, metrics and retries (`AddNpgsqlDataSource`, `AddRabbitMQClient`, `AddRedisClient`, `AddAzureBlobClient`). | Explicit over magic | Accepted trade-off: less wiring code, consistent with Aspire. Our Features do not duplicate what the integrations already add. |
| D8 | Single-service providers live in the service folder (`Research.Search.AnthropicWebSearch`) | `<App>.Providers.<Name>` | Keeps them next to the only consumer. Shared providers stay in `src/Providers/`. |
| D9 | `Research.Messaging` shared library (consumer pipeline, inbox, outbox dispatcher) | Not covered | One reliable consumer pipeline instead of one per service. |
| D10 | `Research.Providers.Postgres` for generic Postgres plumbing (connection factory helpers, outbox/inbox SQL) | DB code in `<Module>.Postgres` | Module SQL still lives in each module's adapter; only the generic parts are shared. |
| D11 | Hosts have a `Workers/` folder for thin timer loops that only call module handlers | Host = `Program.cs`, `CompositionRoot/`, `Features/` | Keeps hosting concerns out of modules; workers hold no business logic. |
| D12 | New suffixes `*MessageHandler` and `*Tracing` | Not in the suffix list | Message handlers are a new kind of entry point; `*Tracing` comes from the observability skill. |
| D13 | Test projects per service + `ArchitectureTests`, `EndToEndTests`, `TestSupport` | One `UnitTests` + one `IntegrationTests` | Matches the per-service layout; Aspire end-to-end and chaos tests are new kinds of test. |
| D14 | One OpenAPI document per HTTP service (`jobs.v1.json`, `knowledge.v1.json`) | `docs/openapi/v1.json` | One contract per HTTP service. |
| D15 | **Error codes prefixed with the owning service** (`jobs.…`, `fetching.…`); `research.…` only for codes owned by shared libraries | Prefix with the service | Fixes earlier drift where every code used `research.`. See §14. |
| D16 | **No log redaction** (no `Microsoft.Extensions.Compliance.Redaction`, no data-classification attributes, no collector scrub processor) | Observability skill: redact in the pipeline | Decided by the product owner. Research content (question, prompts, LLM output, page text, request/response bodies) **is logged** in full detail through dedicated content log categories; only credentials stay out of logs (see §15.2). |
| D17 | **Valkey** instead of Redis for caching | — | Valkey is BSD-3-licensed and wire-compatible with Redis; recent Redis server releases are not under a permissive license. Aspire hosts it with `AddValkey`; clients use StackExchange.Redis (MIT). |
| D18 | **Mock embeddings** (`Research.Providers.MockEmbeddings`) for now | — | No embeddings provider chosen yet (Anthropic has no embeddings API). The port stays, so a real provider is a new adapter + one feature-list change. See §8.2. |
| D19 | **Anthropic web search** is the only search provider, behind a strategy port | — | Extensible to other providers later without touching handlers. See §8.1. |
| D20 | **The gateway is a black box: no auth, no tenancy, no forwarded headers, no audit log** | — | Security and identity are the gateway's concern. Our services have no authentication/authorization, no tenant column or filters, no `ForwardedHeadersMiddleware`, and no audit log (it would have no actor to record). HTTP requests run as the fixed actor `api`; workers as `system:<service>`. |
| D21 | **No data retention or cleanup** for now | — | Product decision. Tables, blobs and queues grow without automatic cleanup; revisit before production. |
| D22 | **Local Aspire only** — no deployment target built yet | — | Hosting design (§19) kept for later. |
| D23 | **SSE only** for client notifications | — | No webhooks for now. |
| D24 | **Claim check over Azure Blob Storage, Azurite locally** for fetched documents | — | Keeps large text off RabbitMQ quorum queues. Azurite is MIT-licensed and first-class in Aspire. The `IDocumentStore` port keeps an S3-compatible store possible later. |
| D25 | **Minimal API endpoint classes instead of FastEndpoints** | FastEndpoints | One static class per endpoint (`Pattern`, `Name`, `HandleAsync`), mapped in `<Service>Api.Map`. No extra framework; built-in OpenAPI and `TypedResults.ServerSentEvents`. |

---

## 3. Service topology

```text
                    External API gateway (not ours)
                                 │ HTTP, traceparent
                  ┌──────────────▼──────────────┐
                  │ Jobs  (HTTP API + coordinator)│  owns the job state machine
                  └──────────────┬──────────────┘
                                 │ transactional outbox → RabbitMQ
   ┌──────────────┬──────────────┼───────────────┬────────────────────┐
   ▼              ▼              ▼               ▼                    ▼
Planning       Search         Fetching        Knowledge            Synthesis
LLM: split     web search     download +      chunk, embed,        LLM: cited
question into  providers      extract text,   store in pgvector;   report from
sub-questions  + cache        claim check     retrieval API ◄───── top passages (HTTP)
```

| Service | Kind | Owns | Public | Scales on |
| --- | --- | --- | --- | --- |
| Jobs | HTTP API + consumers + workers | `research_jobs` DB | yes (behind gateway) | requests, queue depth |
| Planning | worker | nothing (Valkey plan cache) | no | queue depth |
| Search | worker | nothing (Valkey result cache); Anthropic web search | no | queue depth |
| Fetching | worker | `research_fetching` DB + blob container | no | queue depth |
| Knowledge | worker + internal HTTP API | `research_knowledge` DB (pgvector) | no | queue depth, requests |
| Synthesis | worker | nothing | no | queue depth |

All Hosts use the `web` template (even workers), so every pod exposes `/health/live` and `/health/ready` for
probes. Worker Hosts map no other endpoints.

**Coordinator:** Jobs is the orchestrator. It can later be split into `Research.Coordinator.Host` (consumers,
outbox, workers) and `Research.Jobs.Host` (API only) by composing the same `Research.Jobs` module in two Hosts —
no module code changes.

---

## 4. Folder structure

```text
ResearchAgentV3/
├── Research.slnx                          # "Solution Items" folder lists root files + docs/
├── filters/                               # Jobs.slnf, Planning.slnf, Search.slnf, ...
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── README.md
│
├── docs/
│   ├── PLAN.md                            # this file
│   ├── edge-cases.md
│   ├── architecture.md                    # reference rules, Host shape, decisions D1–D24, recipes
│   ├── configuration.md                   # every section/key/env var, secrets, per environment
│   ├── errors.md                          # every error code per service
│   ├── messaging.md                       # exchanges, queues, contracts, retry/DLQ policy
│   ├── database.md                        # databases, tables, indexes, retention, ownership
│   ├── observability.md                   # event-ID ranges, metric catalog, SLOs
│   ├── runbooks/                          # one per alert
│   ├── postmortems/
│   └── openapi/
│       ├── jobs.v1.json
│       └── knowledge.v1.json
│
├── src/
│   ├── Research.AppHost/
│   │
│   ├── Shared/
│   │   ├── Research.SharedKernel/         # no package references
│   │   │   ├── Entities/                  # Entity, AuditableEntity
│   │   │   ├── Results/                   # Result, Result<T>, Error, ErrorKind
│   │   │   ├── Errors/                    # IErrorCatalogContributor
│   │   │   ├── Actors/                    # ICurrentActor
│   │   │   ├── Messaging/                 # IMessagePublisher, IMessageHandler<T>, MessageEnvelope
│   │   │   ├── Llm/                       # ILlmClient
│   │   │   ├── Embeddings/                # IEmbeddingGenerator
│   │   │   ├── Documents/                 # IDocumentStore (claim check)
│   │   │   └── Caching/                   # ICacheStore
│   │   ├── Research.Contracts/            # message records, no package references
│   │   │   ├── Jobs/
│   │   │   ├── Planning/
│   │   │   ├── Search/
│   │   │   ├── Fetching/
│   │   │   ├── Knowledge/
│   │   │   └── Synthesis/
│   │   ├── Research.Hosting/              # platform Feature classes (replaces ServiceDefaults)
│   │   │   ├── Features/                  # IResearchFeature, ResearchBuilder
│   │   │   ├── Logging/
│   │   │   ├── Telemetry/
│   │   │   ├── Health/
│   │   │   ├── ServiceDiscovery/
│   │   │   ├── Resilience/
│   │   │   └── Actors/                    # SystemActor, ApiActor (fixed "api")
│   │   ├── Research.Messaging/
│   │   │   ├── Consuming/                 # consumer pipeline, failure classification
│   │   │   ├── Inbox/
│   │   │   ├── Outbox/                    # OutboxBackgroundService
│   │   │   ├── Tracing/                   # TraceContextHeaders, consumer activities
│   │   │   └── Metrics/                   # MessagingMetrics
│   │   ├── Research.Problems/             # RFC 9457 writer, ErrorKind → status, catalog
│   │   └── Research.Http/
│   │       ├── Middleware/                # correlation, unhandled exception
│   │       └── Features/                  # CorrelationIdFeature
│   │
│   ├── Providers/
│   │   ├── Research.Providers.Anthropic/  # ILlmClient + RateLimitedLlmClient decorator
│   │   ├── Research.Providers.MockEmbeddings/  # deterministic IEmbeddingGenerator (§8.2)
│   │   ├── Research.Providers.RabbitMq/   # publisher, consumer listener, retry topology
│   │   ├── Research.Providers.Valkey/     # ICacheStore over StackExchange.Redis
│   │   ├── Research.Providers.Postgres/   # connection helpers, outbox/inbox SQL, LISTEN/NOTIFY
│   │   └── Research.Providers.BlobStorage/
│   │
│   ├── Jobs/
│   │   ├── Research.Jobs/
│   │   │   ├── CreateResearchJob/
│   │   │   ├── GetResearchJob/
│   │   │   ├── GetResearchReport/
│   │   │   ├── CancelResearchJob/
│   │   │   ├── AdvanceOnPlanReady/
│   │   │   ├── AdvanceOnResultsFound/
│   │   │   ├── AdvanceOnDocumentIndexed/
│   │   │   ├── CompleteOnReportReady/
│   │   │   ├── HandleStepFailure/
│   │   │   ├── RecoverStuckJobs/
│   │   │   ├── EnforceJobDeadlines/      # synthesis cutoff + hard deadline (§10.1)
│   │   │   └── Domain/
│   │   ├── Research.Jobs.Postgres/
│   │   │   ├── Jobs/
│   │   │   ├── Steps/
│   │   │   ├── Reports/
│   │   │   ├── Health/
│   │   │   └── db/
│   │   ├── Research.Jobs.Api/
│   │   │   └── Jobs/
│   │   │       ├── CreateResearchJob/
│   │   │       ├── GetResearchJob/
│   │   │       ├── GetResearchReport/
│   │   │       ├── CancelResearchJob/
│   │   │       └── StreamResearchJob/     # SSE progress
│   │   └── Research.Jobs.Host/
│   │       ├── CompositionRoot/
│   │       ├── Features/
│   │       └── Workers/                   # stuck-job recovery, job deadlines
│   │
│   ├── Planning/
│   │   ├── Research.Planning/
│   │   │   ├── PlanResearch/
│   │   │   └── Domain/
│   │   └── Research.Planning.Host/
│   │       ├── CompositionRoot/
│   │       └── Features/
│   │
│   ├── Search/
│   │   ├── Research.Search/
│   │   │   ├── RunSearch/
│   │   │   └── Domain/                    # ISearchProvider port, SearchHit, UrlCanonicalizer
│   │   ├── Research.Search.AnthropicWebSearch/   # ISearchProvider adapter (§8.1)
│   │   └── Research.Search.Host/
│   │       ├── CompositionRoot/
│   │       └── Features/
│   │
│   ├── Fetching/
│   │   ├── Research.Fetching/
│   │   │   ├── FetchDocument/
│   │   │   └── Domain/
│   │   ├── Research.Fetching.Postgres/
│   │   │   └── db/
│   │   └── Research.Fetching.Host/
│   │       ├── CompositionRoot/
│   │       └── Features/
│   │
│   ├── Knowledge/
│   │   ├── Research.Knowledge/
│   │   │   ├── IngestDocument/
│   │   │   ├── RetrievePassages/
│   │   │   └── Domain/
│   │   ├── Research.Knowledge.Postgres/
│   │   │   ├── Documents/
│   │   │   ├── Passages/
│   │   │   ├── Health/
│   │   │   └── db/
│   │   ├── Research.Knowledge.Api/
│   │   │   └── Knowledge/
│   │   │       └── RetrievePassages/
│   │   └── Research.Knowledge.Host/
│   │       ├── CompositionRoot/
│   │       └── Features/
│   │
│   └── Synthesis/
│       ├── Research.Synthesis/
│       │   ├── WriteReport/
│       │   └── Domain/
│       ├── Research.Synthesis.HttpKnowledge/
│       └── Research.Synthesis.Host/
│           ├── CompositionRoot/
│           └── Features/
│
├── tests/
│   ├── .editorconfig
│   ├── Research.ArchitectureTests/
│   ├── Research.TestSupport/              # hand-made fakes: FakeLlmClient, InMemoryPublisher, ...
│   ├── Shared/
│   │   ├── Research.Messaging.Tests/
│   │   └── Research.Problems.Tests/
│   ├── Jobs/
│   │   ├── Research.Jobs.UnitTests/
│   │   └── Research.Jobs.IntegrationTests/
│   ├── Planning/    Research.Planning.UnitTests/
│   ├── Search/      Research.Search.UnitTests/
│   ├── Fetching/    Research.Fetching.UnitTests/    Research.Fetching.IntegrationTests/
│   ├── Knowledge/   Research.Knowledge.UnitTests/   Research.Knowledge.IntegrationTests/
│   ├── Synthesis/   Research.Synthesis.UnitTests/
│   └── Research.EndToEndTests/            # Aspire.Hosting.Testing, chaos tests
│
└── deploy/                                # only db/ is used now; the rest is future (D22)
    ├── db/                                # script runner applying each service's db/
    ├── k8s/                               # generated by `aspire publish`, then reviewed
    ├── keda/                              # queue-depth scalers per worker
    ├── otel-collector/                    # loadbalancing tier + tail-sampling tier
    ├── prometheus/                        # recording + alert rules per service
    ├── alertmanager/
    ├── grafana/                           # one dashboard per service + job dashboard
    └── dotnet-monitor/                    # sidecar configuration
```

Every module root holds only slice folders and `Domain/`. No `Services/`, `Repositories/`, `Models/`,
`Helpers/`, `Infrastructure/` or `Controllers/` folders anywhere.

---

## 5. Project reference rules

Enforced by `Research.ArchitectureTests`.

| Project | May reference |
| --- | --- |
| `Research.SharedKernel` | nothing (no packages) |
| `Research.Contracts` | nothing (no packages) |
| `Research.Hosting` | `SharedKernel`; OpenTelemetry, Serilog, health-check, service-discovery packages |
| `Research.Messaging` | `SharedKernel`, `Contracts` |
| `Research.Problems` | `SharedKernel`; ASP.NET Core framework |
| `Research.Http` | `SharedKernel`; ASP.NET Core framework |
| `Research.<Module>` | `SharedKernel`, `Contracts`, `Messaging` (for `IMessageHandler<T>` only) |
| `Research.<Module>.Postgres` | `SharedKernel`, its own module, `Providers.Postgres` |
| `Research.Providers.*`, `Research.<Service>.<Provider>` | `SharedKernel` (and `Contracts` for RabbitMq) |
| `Research.<Service>.Api` | `SharedKernel`, `Problems`, `Http`, its own module |
| `Research.<Service>.Host` | anything it composes |
| `Research.AppHost` | Host projects only |

Also enforced:
- Modules reference no ASP.NET Core, FastEndpoints, RabbitMQ, Npgsql, StackExchange.Redis or provider SDK
  packages (including the `Anthropic` SDK).
- No module references another module. Services talk only through `Research.Contracts` (messages) or a port
  with an HTTP adapter (`Synthesis → Knowledge`).

---

## 6. Host shape and composition root

Every Host:

```text
Research.<Service>.Host/
├── Program.cs                      # bootstrap logger → root.ComposeServices() → Build →
│                                   # root.ComposePipeline(app) → root.ComposeEndpoints(app)
├── CompositionRoot/
│   ├── <Service>Settings.cs        # binds + validates every options section; bad config stops startup
│   ├── <Service>CompositionRoot.cs # the feature list, grouped by concern, each feature once
│   └── <Service>CompositionRoot.Pipeline.cs   # middleware order, a comment per step
├── Features/                       # one internal sealed *Feature per capability
├── Workers/                        # optional: thin timer loops calling module handlers
└── appsettings.json / appsettings.Development.json
```

Example feature list (Search):

```text
// Platform (Research.Hosting)
LoggingFeature, TelemetryFeature, HealthChecksFeature, ServiceDiscoveryFeature, HttpResilienceFeature
// Infrastructure (Aspire client integrations)
RabbitMqFeature, ValkeyFeature
// Capability
SearchFeature, SearchProviderFeature (selects the strategy, §8.1), SearchMessagingFeature
// Decorators (after the feature they decorate)
RateLimitedSearchProviderFeature, CachedSearchProviderFeature
```

Rules: features never order anything; a disabled feature registers nothing; decorate, don't replace; every
handler, repository, validator and adapter has its own explicit registration line (FastEndpoints endpoint
discovery is the only scanning).

Jobs pipeline order:

```text
1. UnhandledExceptionMiddleware   first, so everything below becomes a problem document
2. CorrelationMiddleware          before anything logs; reads an optional X-Correlation-ID
3. JobsApi.Map(app)               FastEndpoints, OpenAPI (non-Production), health endpoints

No authentication, authorization or forwarded-header steps (D20). Knowledge (internal API) uses the same
pipeline; it is called only by Synthesis.
```

---

## 7. Aspire: AppHost and service defaults

### 7.1 AppHost resources

| Resource | Aspire hosting API | Used by |
| --- | --- | --- |
| `pg` (image `pgvector/pgvector`) with databases `jobs`, `fetching`, `knowledge`, data volume | `AddPostgres` / `AddDatabase` | Jobs, Fetching, Knowledge |
| `bus` RabbitMQ with management plugin, data volume | `AddRabbitMQ` | all services |
| `cache` Valkey, data volume | `AddValkey` | Planning, Search |
| `storage` blobs (Azurite emulator locally, data volume) | `AddAzureStorage(...).RunAsEmulator(e => e.WithDataVolume()).AddBlobs` | Fetching (write), Knowledge (read) |
| `anthropic-api-key` | `AddParameter(secret: true)` | Planning, Search, Synthesis |
| `db-scripts` | executable/container resource that applies `db/` scripts before services start | all DB owners |

Projects: `jobs` (`WithExternalHttpEndpoints`, the gateway's target), `planning`, `search`, `fetching`,
`knowledge`, `synthesis`; each with `WithReference` to what it uses, `WaitFor` on hard dependencies, and
`WithReplicas(2)` (Search and Fetching 3).

### 7.2 Service defaults without a ServiceDefaults project (D6)

The Aspire template's `ServiceDefaults` project is **not created**. Its responsibilities move into
`Research.Hosting` as Feature classes that every composition root lists explicitly:

| ServiceDefaults responsibility | Replaced by |
| --- | --- |
| `ConfigureOpenTelemetry()` | `TelemetryFeature` — resource attributes, ASP.NET Core / HttpClient / runtime instrumentation, every `*Tracing` source and `*Metrics` meter by name, OTLP exporter from the standard `OTEL_EXPORTER_OTLP_ENDPOINT` that Aspire injects |
| `AddDefaultHealthChecks()` | `HealthChecksFeature` — `self` check tagged `live`; integration checks tagged `ready` |
| `MapDefaultEndpoints()` | `HealthEndpoints.Map(app)` — called from `<Service>Api.Map` (HTTP Hosts) or the composition root (worker Hosts); `/health/live`, `/health/ready`, status only |
| `AddServiceDiscovery()` | `ServiceDiscoveryFeature` |
| `ConfigureHttpClientDefaults(AddStandardResilienceHandler)` | `HttpResilienceFeature` — standard resilience handler (timeout, retry with jitter, circuit breaker) on every HttpClient; per-client overrides in each provider feature |
| (not in ServiceDefaults) | `LoggingFeature` — Serilog as a logging provider, JSON to stdout + OTLP |

Aspire client integrations (`AddNpgsqlDataSource`, `AddRabbitMQClient`, `AddRedisClient` — used for Valkey,
which speaks the same protocol — and `AddAzureBlobClient`) are called from infrastructure features
(`PostgresFeature`, `RabbitMqFeature`, `ValkeyFeature`, ...) with their defaults on (D7). `TelemetryFeature`
therefore does **not** add Npgsql/RabbitMQ/Valkey instrumentation again.

---

## 8. Design patterns

| Pattern | Where | Purpose |
| --- | --- | --- |
| Composition root | `*.Host/CompositionRoot/` | Only Hosts know concrete types; all wiring and order in one place |
| Feature modules | `*.Host/Features/`, `Research.Hosting` | One capability per file, one line to add or remove |
| Vertical slices | module folders | A use case's code lives together |
| CQRS-lite | `*Command` / `*Query` + one handler | Clear intent, independent tuning; no mediator |
| Result pattern | `SharedKernel/Results` | Expected outcomes as values, exceptions for the unexpected |
| Ports and adapters | ports in `SharedKernel` / `Domain/`, adapters in providers and `*.Postgres` | Swap LLM, search, broker, storage without touching handlers |
| Specific repositories + read queries | `*.Postgres` | Purpose-named SQL methods; reads return response shapes |
| State machine | `Jobs/Domain` | Legal job transitions, deadlines, budgets |
| Decorator | `CachedSearchProvider`, `RateLimitedSearchProvider`, `RateLimitedLlmClient` | Behavior added via `Decorate<T>` |
| Strategy | search providers (§8.1), embedding generators (§8.2), content extractors per MIME type | Pluggable behavior chosen by configuration |
| Adapter | `AnthropicWebSearchProvider`, `MockEmbeddingGenerator` | Translate an external API into the port's shape |
| Policy objects | `JobBudgetPolicy`, `RobotsPolicy`, `SsrfGuard` | Small, testable rules |
| Options pattern + validation | `*Options` + source-generated validators | Invalid config stops startup; reload without restart |
| Error catalog contributor | `*Errors` per library | Stable codes, no central list |
| Typed request features | `Research.Http/Features` | Per-request state without `HttpContext.Items` |

### 8.1 Search provider design (Strategy + Adapter + Decorators)

Today the only provider is **Anthropic's server-side web search tool**; the design lets other providers
(Brave, Bing, SearXNG, ...) be added later without touching the module.

```text
RunSearchCommandHandler ──► ISearchProvider                         (port, Research.Search/Domain)
                               ▲
          CachedSearchProvider │  decorator: Valkey cache-aside, key = provider + normalized query
     RateLimitedSearchProvider │  decorator: shares Anthropic quota with Planning/Synthesis
    AnthropicWebSearchProvider │  adapter (strategy), Research.Search.AnthropicWebSearch
                               │
   later: FallbackSearchProvider (composite / chain of responsibility over an ordered list)
```

- **Port:** `ISearchProvider.SearchAsync(SearchQuery, CancellationToken) → Result<IReadOnlyList<SearchHit>>`.
  `SearchHit` (url, title, snippet, published/page age, provider) is the module's own shape — no provider
  types leak into the module.
- **Strategy selection in the composition root:** `SearchProviderFeature` reads `Search:Provider`
  (`AnthropicWebSearch` today) and registers exactly one adapter with an explicit line. Adding a provider =
  new adapter project + one `case` in that feature + its options section. No runtime factory, no scanning.
- **Adapter (`AnthropicWebSearchProvider`):** calls the Messages API through the official `Anthropic` C# SDK
  with the `web_search_20260209` server tool (`max_uses`, optional `allowed_domains` / `blocked_domains`,
  `user_location`), then reads only the `web_search_tool_result` blocks and maps each `web_search_result` to a
  `SearchHit`. The model's prose is ignored.
  - Server-tool errors arrive as HTTP 200 with an error object instead of a result list → mapped to
    `search.provider_unavailable` / `search.quota_exhausted` / `search.no_results`.
  - Options: `AnthropicWebSearch:Model`, `:MaxUses`, `:Effort`, `:AllowedDomains`, `:BlockedDomains`.
- **Normalization in the module:** URL canonicalization and per-job dedupe live in `Domain/`, so every provider
  benefits.
- **Later extensions:** `FallbackSearchProvider` (ordered providers, next on `provider_unavailable`), hedging,
  and an Anthropic `web_fetch` strategy for the Fetching module behind an `IDocumentFetcher` port.

### 8.2 Embeddings (mock for now)

- Port `IEmbeddingGenerator` in SharedKernel; adapter `MockEmbeddingGenerator` in
  `Research.Providers.MockEmbeddings`, registered by `EmbeddingsFeature` in the Knowledge Host.
- Deterministic **feature hashing**: tokenize → hash each token into one of `n` buckets (signed) → L2-normalize.
  No network, no cost, repeatable in tests, and texts sharing words still score as similar, so retrieval
  behaves plausibly end to end.
- Options: `Embeddings:Provider = Mock`, `Embeddings:Dimensions = 384`, model id `mock-hashing-v1`.
- Every passage stores `embedding_model`; retrieval only compares vectors of the same model. Switching to a
  real provider = new adapter, a new `vector(n)` column or table if the dimension changes, and a re-embed
  job — handlers stay unchanged.

### 8.3 LLM usage (Planning, Synthesis, Search)

- Official `Anthropic` C# SDK in `Research.Providers.Anthropic` (and `Research.Search.AnthropicWebSearch`).
- Model **`claude-sonnet-5-5`** with effort **`medium`** for planning, synthesis and web search (product-owner
  decision). Configurable per use: `Anthropic:PlanningModel` / `:PlanningEffort`, `Anthropic:SynthesisModel` /
  `:SynthesisEffort`, `AnthropicWebSearch:Model` / `:Effort`.
- Effort is set **explicitly** in every request: Sonnet 5.5 defaults to `high`, so omitting it would not give
  `medium`. Thinking stays adaptive (`thinking: {type: "disabled"}` is rejected on this model).
- Sonnet 5.5 supports the `web_search_20260209` tool used by Search (§8.1).
- **Server-side refusal fallbacks on by default** (`fallbacks: "default"` with the
  `server-side-fallback-2026-07-01` beta); every response checks `stop_reason` (`refusal` →
  `planning.llm_refused` / `synthesis.llm_refused`, `max_tokens` → `synthesis.output_truncated`).
- Planning output via structured outputs (`output_config.format`), not prefill or forced tool choice.
- Synthesis streams (long outputs) and uses prompt caching for the stable system prompt.
- One Anthropic organization quota is shared by Planning, Search and Synthesis; each service's rate-limiter
  share is configured so the sum stays under the quota.

---

## 9. Microservices patterns

| Category | Pattern | Applied as |
| --- | --- | --- |
| Decomposition | Database per service | Jobs, Fetching, Knowledge own their DBs; no cross-service table access |
| Communication | Async messaging | RabbitMQ; one sync call (Synthesis → Knowledge retrieval) |
| | Orchestration saga / process manager | Jobs drives every step and handles failure |
| | Scatter-gather | N searches → M fetches → M indexings, counted back in `job_steps` |
| | Async request-reply | `202 Accepted` + job id; poll `GET` or SSE |
| | Claim check | Documents in blob storage; messages carry a reference |
| | Service discovery | Aspire / Kubernetes DNS |
| | API gateway | External (§1) |
| Reliability | Transactional outbox | State change + message in one transaction |
| | Idempotent consumer (inbox) | Message id recorded where state is written |
| | Competing consumers | N replicas per queue |
| | Queue-based load leveling | Queues absorb bursts; workers drain at LLM-sustainable rate |
| | Retry + dead-letter channel | Delay queues, DLQ, `StepFailed` |
| | Circuit breaker, timeout, hedging, fallback | Standard resilience handler; hedging + fallback provider for search |
| | Bulkhead | Prefetch per consumer; LLM concurrency limiter |
| | Scheduler-agent-supervisor | `StuckJobRecoveryWorker` re-emits expired steps |
| | Graceful degradation | Synthesis with partial sources (`MinSources`) |
| | Cache-aside | Search results and plans in Valkey |
| Operations | Health check API | `/health/live`, `/health/ready` |
| | Externalized configuration | Options + Aspire parameters + secret store |
| | Distributed tracing + correlation | OpenTelemetry, trace context in message headers |
| | Log aggregation, application metrics | Serilog → OTLP; `*Metrics` classes |

---

## 10. Job flow

1. `POST /research` (via gateway) → Jobs validates, inserts `research_jobs` + outbox `PlanRequested` in one
   transaction → `202` with job id. Duplicate `Idempotency-Key` returns the existing job.
2. Planning consumes `PlanRequested`, calls the LLM, publishes `PlanReady(subQuestions)`.
3. Jobs records the plan and creates one `job_steps` row per sub-question; publishes `SearchRequested` ×N.
4. Search publishes `ResultsFound(urls)`. Jobs dedupes URLs per job, creates fetch steps, publishes
   `FetchRequested` ×M (capped by `Jobs:MaxSources`).
5. Fetching downloads + extracts, stores content in blob storage, publishes `DocumentFetched(blobRef)`.
6. Knowledge reads the blob, chunks, embeds, upserts passages, links them to the job, publishes
   `DocumentIndexed`.
7. Jobs counts completed/failed steps. When gathering ends (§10.1: all fetch steps terminal, synthesis
   cutoff reached, or budget exhausted) it evaluates the indexed sources and either publishes
   `SynthesisRequested` exactly once or fails the job.
8. Synthesis retrieves top-K passages from the Knowledge API, writes a cited report, publishes `ReportReady`.
9. Jobs stores the report, marks the job `Completed` or `CompletedWithWarnings`; SSE subscribers are notified.

### 10.1 Job termination semantics

**States**

| State | Kind | Meaning |
| --- | --- | --- |
| `Pending` | active | Created; `PlanRequested` in the outbox |
| `Planning` | active | Planning step in flight |
| `Gathering` | active | Search, fetch and index steps in flight (replaces the separate Searching/Fetching states; per-step progress lives in `job_steps`) |
| `Synthesizing` | active | `SynthesisRequested` published; waiting for `ReportReady` |
| `Completed` | terminal | Report stored; every planned step succeeded and gathering ended naturally |
| `CompletedWithWarnings` | terminal | Report stored, but gathering was cut short or incomplete — see warnings below |
| `Failed` | terminal | No report; a defined failure condition (below), with `error_code` set |
| `Cancelled` | terminal | No report; cancelled by the client before reaching another terminal state |
| `Expired` | terminal | No report; the **hard deadline** passed while the job was still active |

Terminal states are absorbing: once a job is terminal, no transition leaves it. There is no `Cancelling` state —
cancellation is immediate (see races).

**Two clocks per job** (both stored on the row, computed from database `now()` at creation)

| Column | Value | Effect when reached |
| --- | --- | --- |
| `synthesis_cutoff_at` | `created_at + Jobs:MaxDuration − Jobs:SynthesisReserve` (5 min − 90 s) | Ends gathering early; the job goes to synthesis evaluation |
| `deadline_at` | `created_at + Jobs:MaxDuration` (5 min) | Any job still active becomes `Expired` |

The **cutoff** triggers synthesis; the **deadline** expires. The reserve exists so a job that reaches the cutoff
normally still has time to synthesize before the deadline.

**Ending gathering → synthesis evaluation.** Gathering ends at the first of: (a) every search/fetch/index step
is terminal, (b) `synthesis_cutoff_at` reached, (c) token budget exhausted. Then, with `indexed` = documents
indexed for the job:

| Condition | Result | Code / warnings |
| --- | --- | --- |
| `indexed = 0` | `Failed` | `jobs.no_sources_found` |
| `0 < indexed < Jobs:MinSources` (3) | `Failed` | `jobs.insufficient_sources` |
| `indexed ≥ MinSources` | `Synthesizing` | warnings recorded for the final state (below) |

**Final state after `ReportReady`**

- `Completed` if gathering ended by (a) and no step failed.
- `CompletedWithWarnings` otherwise; `warnings` (jsonb) lists every reason that applies:
  `jobs.cutoff_reached` (ended by b), `jobs.budget_exhausted` (ended by c), `jobs.steps_failed` (with count),
  `synthesis.output_truncated` (report hit `max_tokens`).

**Other paths to `Failed`:** planning fails after retries (`planning.*`), synthesis fails after retries or is
refused (`synthesis.*`), or `research.step_exhausted` on the planning or synthesis step. A failed single
search/fetch/index step never fails the job by itself; it only counts toward `jobs.steps_failed`.

**`Expired` vs `CompletedWithWarnings`:** a job that reaches the cutoff with enough sources is *not* expired —
it synthesizes and ends `CompletedWithWarnings (jobs.cutoff_reached)`. `Expired` (`jobs.deadline_exceeded`)
means the hard deadline passed with no report: planning or synthesis was too slow, or a stuck step was never
recovered in time.

**Transitions are compare-and-set.** Every state change is one statement:

```sql
UPDATE research_jobs
SET status = @to, version = version + 1, ...
WHERE id = @id AND version = @version AND status = ANY(@allowedFrom)
```

in the same transaction as its outbox rows. Zero rows updated → re-read the job; if it is now terminal the
incoming event is discarded (acked, logged at `Information`, counted as `research.jobs.late_results`); otherwise
re-evaluate and retry.

**Races, resolved by whichever CAS commits first**

| Race | Cancel/expiry wins | The other wins |
| --- | --- | --- |
| Cancel vs. gathering end | `Cancelled`; no `SynthesisRequested` is ever published (same CAS + outbox tx) | `Synthesizing`; cancel is still allowed from there (next row) |
| Cancel vs. `ReportReady` | `Cancelled`; the arriving report is discarded and **not stored** | `Completed*`; cancel returns `409 jobs.job_already_completed` |
| Deadline vs. `ReportReady` | `Expired`; the late report is discarded | `Completed*` |
| Deadline vs. cancel | `Expired`; cancel returns `409 jobs.job_not_cancellable` | `Cancelled` |
| Cutoff vs. last step finishing | Both lead to synthesis evaluation; the CAS lets exactly one publish `SynthesisRequested`; the warning `jobs.cutoff_reached` is set only if the cutoff transition won |

Cancellation is best effort for work already in flight: workers check job status before expensive calls only
where they can read it cheaply (Jobs does; stateless workers do not), so an in-flight LLM call may complete and
be billed, and its result is discarded.

**Who drives the clocks:** `JobDeadlineWorker` (timer, every replica, `SKIP LOCKED`) moves `Gathering` jobs past
`synthesis_cutoff_at` into synthesis evaluation and moves any active job past `deadline_at` to `Expired`. Both are
ordinary CAS transitions, so the worker races safely with message handlers.

Every terminal transition publishes `JobCompleted` / `JobCancelled` (plus `JobFailed`, `JobExpired`) and
notifies SSE subscribers.

---

## 11. Messaging

**Topology**
- One topic exchange `research.events`; routing key = contract name (`fetching.fetch-requested`).
- One durable **quorum queue per consuming service and contract** (`fetching.fetch-requested`); services never
  share a queue.
- Per queue: dead-letter exchange, delay queues (10 s, 1 min, 5 min) via TTL, and a DLQ.
- Publisher confirms on; persistent messages; consumer manual ack; per-consumer prefetch.

**Contracts** (`Research.Contracts`)

| Kind | Contracts |
| --- | --- |
| Commands (one consumer service) | `PlanRequested`, `SearchRequested`, `FetchRequested`, `IngestRequested`*, `SynthesisRequested` |
| Events (zero or more consumers) | `JobCreated`, `PlanReady`, `ResultsFound`, `DocumentFetched`, `DocumentIndexed`, `ReportReady`, `StepFailed`, `JobCompleted`, `JobCancelled`, `JobFailed`, `JobExpired` |

\* Knowledge consumes `DocumentFetched` directly; `IngestRequested` exists only for re-indexing.

**Envelope:** `messageId` (UUID v7), `type`, `version`, `jobId`, `stepKey`, `occurredAt`, `traceparent`,
`tracestate`, `attempt`, `payload`.

**Versioning:** additive changes only; consumers ignore unknown fields; a breaking change is a new contract
type (`FetchRequestedV2`) consumed alongside the old one until producers are migrated.

---

## 12. Database architecture

### 12.1 Topology

```text
Postgres cluster (HA: primary + synchronous standby in another zone, PITR backups)
├── research_jobs        owner: Jobs        role: jobs_svc
├── research_fetching    owner: Fetching    role: fetching_svc
└── research_knowledge   owner: Knowledge   role: knowledge_svc   + read replica, pgvector

Blob storage (zone-redundant)   container documents/   Fetching writes, Knowledge reads
Valkey (replicated)             plan cache (Planning), search cache (Search), key prefix per service
```

Planning, Search and Synthesis are stateless; Jobs persists their results.

### 12.2 Tables

| Database | Table | Key columns |
| --- | --- | --- |
| research_jobs | `research_jobs` | id, question, status, plan (jsonb), idempotency_key, synthesis_cutoff_at, deadline_at, token_budget, tokens_used, error_code, warnings (jsonb), completed_at, version, audit columns |
| | `job_steps` | id, job_id, kind, step_key (unique per job), status, attempts, lease_until, last_error_code, trace_id, audit columns |
| | `job_sources` | job_id, url_hash, url, status |
| | `research_reports` | job_id, content, citations (jsonb), model, audit columns |
| | `outbox_messages` | id, type, payload, headers, created_at, published_at |
| | `inbox_messages` | message_id, consumer, processed_at |
| research_fetching | `fetched_documents` | id, url, url_hash, content_hash, mime_type, size_bytes, blob_key, status, audit columns |
| | `outbox_messages`, `inbox_messages` | as above |
| research_knowledge | `documents` | id, content_hash (unique), source_url, blob_key, embedding_model, audit columns |
| | `passages` | id, document_id, chunk_index, text, embedding `vector(n)` (HNSW index), embedding_model |
| | `job_documents` | job_id, document_id |
| | `outbox_messages`, `inbox_messages` | as above |

### 12.3 Rules

- UUID v7 keys; audit columns written explicitly by handlers and SQL.
- No tenancy (D20): no tenant column or filters. `idempotency_key` is unique across all jobs.
- Fan-in: `UPDATE job_steps … RETURNING` + `UPDATE research_jobs … WHERE id = @id AND version = @version`;
  exactly one handler observes the final count and enqueues `SynthesisRequested`.
- Work claiming: `SELECT … FOR UPDATE SKIP LOCKED` for outbox, stuck-job recovery and expiry — safe on
  every replica, no leader election.
- Outbox wake-up via `LISTEN/NOTIFY`, polling as fallback.
- Schema: versioned SQL scripts in each `db/` folder, applied by the `deploy/db` runner before rollout; no
  migrations at startup. Readiness checks verify required tables. Changes are expand-then-contract.
- One database role per service, no access to other services' databases.
- Connections: Npgsql pooling + PgBouncer (transaction mode).
- Reads: Knowledge retrieval uses the read replica; falls back to primary when replica lag exceeds a threshold.
- Growth: **no retention or cleanup for now (D21)**. Indexes are designed so the hot queries (unpublished
  outbox rows, expired leases, a job's steps) stay fast as tables grow: partial indexes on
  `outbox_messages (created_at) WHERE published_at IS NULL` and `job_steps (lease_until) WHERE status = 'Running'`.
  Partitioning and retention are deferred.
- Failover: multi-host connection strings with `Target Session Attributes=primary`; failed transactions retry
  via redelivery.

---

## 13. Worker design

### 13.1 Consumer pipeline (every message)

```text
RabbitMQ quorum queue  (prefetch = per-replica concurrency)
  → RabbitMqConsumerListener          Providers.RabbitMq
      deserialize envelope (failure → DLQ, poison)
      extract trace context, start consumer activity
      new DI scope, SystemActor("system:<service>")
      per-message timeout linked to shutdown token
  → ConsumerPipeline                  Research.Messaging
      inbox: already processed → ack
  → I<Contract>MessageHandler          module slice
      call the slice's CommandHandler → Result
      BEGIN; state change + inbox row + outbox rows; COMMIT
  → ack (only after commit)
```

### 13.2 Failure classification

| Outcome | Action |
| --- | --- |
| Success | commit, ack |
| Business failure (`Result` failure) | publish `StepFailed(code)`, ack, no retry |
| Transient exception (timeout, 5xx, failover, open circuit) | nack to next delay queue (10 s → 1 min → 5 min) |
| Retries exhausted | DLQ + `StepFailed(research.step_exhausted)` |
| Poison (bad payload, unknown version) | DLQ immediately, alert |
| Crash mid-message | redelivered; inbox prevents double effects |
| Lost message | `StuckJobRecoveryWorker` re-emits after lease expiry |

### 13.3 Workers

| Worker | Host | Trigger | Runs on |
| --- | --- | --- | --- |
| Message consumers (`*Listener`) | every service | queue | every replica, KEDA-scaled |
| `OutboxBackgroundService` | Jobs, Fetching, Knowledge | NOTIFY + polling | every replica (`SKIP LOCKED`) |
| `StuckJobRecoveryWorker` | Jobs | timer | every replica |
| `JobDeadlineWorker` | Jobs | timer: synthesis cutoff + hard deadline (§10.1) | every replica |

No retention workers (D21).

### 13.4 Rules

- Every listener and `BackgroundService` loop is wrapped: an unhandled exception would stop the host. Log,
  then continue or stop deliberately.
- LLM and web search: `RateLimitedLlmClient` / `RateLimitedSearchProvider` decorators (token bucket +
  concurrency cap, honor `retry-after`); per-replica limit = the service's share of the Anthropic quota ÷ max
  replicas; Valkey-backed distributed limiter if that proves too conservative.
- Long LLM calls: RabbitMQ consumer timeout above the longest expected call (15 min); step lease slightly
  longer.
- Fetching: SSRF guard on every resolved IP and redirect hop, max bytes, max redirects, robots policy,
  per-domain concurrency limit, dedicated egress network policy.
- Graceful shutdown: stop consuming, drain in-flight within `ShutdownTimeout`, close channel.
- Workers check job status before expensive work and skip cancelled or expired jobs.

---

## 14. Errors and error codes

**Shape:** RFC 9457 problem document, fields in order `type, title, status, detail, instance, code, traceId`.
Modules return `ErrorKind`; `Research.Problems` maps it: `Invalid → 400`, `NotFound → 404`,
`Conflict → 409`, `Forbidden → 403`, `Rule → 422`, `Unexpected → 500`. No exception text, type names, stack
traces or hosts in any response.

**Prefix rule (D15):** a code is prefixed with the service that owns it. Codes from shared libraries use
`research.`. Codes are never renamed or reused. Worker failures travel as `StepFailed` with the same codes.

| Owner | Codes |
| --- | --- |
| Shared (`Research.Problems`, `Research.Messaging`) | `research.unexpected`, `research.validation_failed`, `research.step_exhausted`, `research.poison_message` |
| Jobs | `jobs.job_not_found`, `jobs.job_already_completed`, `jobs.job_not_cancellable`, `jobs.report_not_ready`, `jobs.idempotency_conflict`, `jobs.no_sources_found`, `jobs.insufficient_sources`, `jobs.deadline_exceeded`, `jobs.budget_exhausted`, `jobs.cutoff_reached`, `jobs.steps_failed` (the last three also as warnings on `CompletedWithWarnings`) |
| Planning | `planning.invalid_plan`, `planning.llm_refused`, `planning.llm_unavailable` |
| Search | `search.no_results`, `search.provider_unavailable`, `search.quota_exhausted` |
| Fetching | `fetching.fetch_disallowed`, `fetching.blocked_address`, `fetching.unsupported_content`, `fetching.too_large`, `fetching.content_too_short`, `fetching.redirect_limit`, `fetching.timeout` |
| Knowledge | `knowledge.document_missing`, `knowledge.embedding_unavailable`, `knowledge.embedding_model_mismatch` |
| Synthesis | `synthesis.llm_refused`, `synthesis.llm_unavailable`, `synthesis.context_too_large`, `synthesis.output_truncated` |

Each library declares its codes in a `*Errors` catalog contributing through `IErrorCatalogContributor`;
`docs/errors.md` documents every code. An architecture test checks that every code starts with its owner's
prefix.

---

## 15. Observability

### 15.1 Signals and pipeline

| Signal | In code | Leaves as |
| --- | --- | --- |
| Logs | `ILogger<T>` + `[LoggerMessage]`, Serilog provider | JSON on stdout + OTLP |
| Traces | `ActivitySource` per library (`*Tracing`), built-in + Aspire integration instrumentation | OTLP |
| Metrics | `*Metrics` classes via `IMeterFactory`, built-in meters | OTLP |

- Local: the Aspire dashboard is the OTLP backend.
- Production: OpenTelemetry Collector (`otelcol-contrib`) → Tempo (traces), Loki (logs), Prometheus (metrics),
  Grafana (dashboards), Alertmanager (alerts). Two collector tiers: `loadbalancing` exporter by trace id →
  tail-sampling tier.
- Resource attributes identical in all signals: `service.name`, `service.version`, `service.instance.id`,
  `deployment.environment.name`.

### 15.2 Logging (no redaction — D16)

- Source-generated `[LoggerMessage]` only, named placeholders, consistent names (`{JobId}`, `{StepKey}`,
  `{ErrorCode}`).
- Event-ID ranges: Messaging 1000–1099, Jobs 2000–2099, Planning 3000–3099, Search 4000–4099,
  Fetching 5000–5099, Knowledge 6000–6099, Synthesis 7000–7099, Providers 8000–8999, Hosting 9000–9099.
- **Content is logged (product-owner decision):** the research question, the plan, prompts sent to the LLM,
  LLM responses (including web search results), fetched page text, extracted text, the synthesized report,
  and HTTP request/response bodies of the Jobs and Knowledge APIs.
- **Content goes through dedicated log categories**, separate from operational logs, so its volume can be
  controlled without touching the rest:

  | Category | Logs | Default level |
  | --- | --- | --- |
  | `Research.Content.Http` | Jobs/Knowledge request and response bodies (`HttpLogging` with body logging) | `Information` |
  | `Research.Content.Llm` | Prompt (system + messages), response text, stop reason, token usage, model, effort | `Information` |
  | `Research.Content.Search` | Query and every result (url, title, snippet) | `Information` |
  | `Research.Content.Documents` | Fetched raw text, extracted text, chunk texts | `Information` |
  | `Research.Content.Reports` | Final report and citations | `Information` |

  Each category's level is changeable at runtime through configuration reload, e.g. set
  `Research.Content.Documents` to `Warning` to stop logging page text without a redeploy.
- **Size cap per entry:** `Logging:Content:MaxChars` (default 64 000). Longer values are cut and the entry
  records `{OriginalLength}` and `{Truncated}`, so a huge page cannot break the log pipeline (OTLP and Loki have
  per-record size limits). Set to `0` for no cap.
- Every content entry carries `{JobId}`, `{StepKey}` and the trace id, so content can be read next to the trace
  of the step that produced it.
- **Credentials are still never logged:** API keys, connection strings and auth headers. `HttpLogging` and the
  Anthropic adapter exclude the `x-api-key` / `Authorization` headers; options classes holding secrets are
  never logged whole. Enforced by an architecture test on `[LoggerMessage]` parameter names (`apiKey`,
  `connectionString`, `password`, `secret`).
- No per-item logs in hot loops for **operational** logs; content categories log one entry per item on purpose.
- Levels: `Information` for the app and content categories, `Warning` for `Microsoft`/`System`; all changed at
  runtime through config reload.
- Consequence to be aware of: logs now contain user questions and third-party page text, so the log backend
  (Aspire dashboard locally, Loki later) holds that data too — access to it should match access to the
  research data itself.

### 15.3 Tracing long-running jobs

- **One trace per hop**: each message handler starts a consumer activity from the extracted parent and ends at
  commit. Fan-out steps carry an `ActivityLink` to the job-creation trace instead of being its children — this
  keeps traces small and compatible with tail sampling (`decision_wait: 10s`).
- Every span carries `research.job.id`; `job_steps.trace_id` stores the trace per step.
- Background workers start a new root activity per run.
- Business failures tag `research.error.code`; only exceptions and failed dependencies set `Error` status.
- SDK parent-based sampling at 1.0; collector keeps all errors, all slow traces, 10% of the rest.
- Health endpoints excluded from tracing and access logs.
- Incoming `traceparent` is honored when present (OpenTelemetry does this); `X-Correlation-ID`, when present,
  is accepted as a validated business reference, added to the log scope and echoed back.

### 15.4 Metric catalog

| Instrument | Type | Tags |
| --- | --- | --- |
| `research.jobs.created` / `.completed` / `.failed` | counter | outcome, error code |
| `research.job.duration` | histogram (s) | outcome |
| `research.job.sources` | histogram | — |
| `research.steps.completed` / `.failed` | counter | step kind, error code |
| `research.llm.duration` | histogram (s) | model, operation |
| `research.llm.tokens` | counter | model, direction |
| `research.llm.throttled` | counter | model |
| `research.search.duration` / `.hits` | histogram | provider |
| `research.fetch.duration` / `.bytes` | histogram | outcome, content class |
| `research.knowledge.passages.indexed` | counter | — |
| `research.messaging.handler.duration` | histogram (s) | queue, outcome |
| `research.messaging.dead_lettered` | counter | queue |
| `research.outbox.oldest_age` | observable gauge (s) | service |
| `research.jobs.stuck` | observable gauge | — |
| `research.jobs.late_results` | counter | event type (results discarded because the job was already terminal) |

Never tag with job id, URL, domain or question text.

### 15.5 Health

- `/health/live`: no checks. `/health/ready`: Postgres + RabbitMQ (hard). Valkey, Anthropic (LLM and web
  search), blob storage: `Degraded`. Mock embeddings has no check.
- Public endpoints return status only; details on an internal port.

### 15.6 SLOs, alerts, dashboards

- SLOs: API availability, API latency, job success, job duration (§1).
- Alerts (owner, severity, `runbook_url` each): fast/slow error-budget burn per SLO; DLQ growth;
  outbox oldest age > 60 s; queue lag growing at max replicas; stuck jobs > 0 for 15 min; LLM throttling rate.
- Dashboards: one per service (SLO, traffic, dependencies, runtime, business, messaging) + a job dashboard
  filtered by `research.job.id` with links to traces and logs.
- A runbook per alert; blameless postmortems feed back into alerts, dashboards and runbooks.

### 15.7 Audit

None (D20). Job state changes are still traceable through the audit columns (`CreatedBy` = `api` or
`system:<service>`), logs and traces.

### 15.8 Diagnostics

`dotnet-monitor` sidecar; crash-dump environment variables; `DOTNET_GCHeapHardLimitPercent` below the container
limit; Postgres `log_min_duration_statement`. Dumps contain secrets and prompts — restricted storage, deleted
after the investigation.

---

## 16. Scalability, availability, fault tolerance

| Concern | Mechanism |
| --- | --- |
| Horizontal scale | Stateless services; competing consumers; KEDA on queue length; HPA on CPU/requests for Jobs and Knowledge API |
| No lost work | Outbox, inbox, quorum queues, publisher confirms, ack after commit |
| Crash recovery | Checkpointed state machine; leases; stuck-job recovery |
| Dependency failure | Standard resilience handler; hedging + fallback for search; circuit breakers |
| LLM limits | Rate limiter decorator, `retry-after`, token budget per job, prompt caching |
| Partial failure | Degrade to partial results (`MinSources`), never hang |
| Availability | ≥ 2 replicas per service (locally via Aspire `WithReplicas`); in production: zones, Postgres HA, 3-node RabbitMQ with quorum queues, replicated Valkey, zone-redundant blobs |
| Rolling deploys | Additive contracts; expand-then-contract schema; graceful drain |
| Connection pressure | PgBouncer; per-replica pool limits |

### 16.1 Durability failure model

**System of record:** Postgres. RabbitMQ is transport only, and Valkey is a cache. Every message that matters
can be rebuilt from Postgres: the outbox holds unpublished messages, and `job_steps` holds every step that is
still owed a result, which `StuckJobRecoveryWorker` re-emits after its lease expires.

**What "no data loss" promises**

1. When `POST /research` returns `202`, the job row and its `PlanRequested` outbox row are committed.
2. When a handler's transaction commits, its state change, inbox row and outgoing outbox rows are committed
   together, or none of them are.
3. A message leaves a queue only after the consumer's transaction committed (manual ack after commit).
4. An outbox row is marked published only after the broker's publisher confirm.

Delivery is **at least once**; state effects are **effectively once** (inbox + compare-and-set transitions).
Message order is not guaranteed and not relied on.

**Failures covered — no committed job state is lost**

| Failure | Why nothing is lost | Recovery |
| --- | --- | --- |
| Any service replica crashes or is killed, at any point | Uncommitted work rolls back; unacked messages are redelivered; unpublished outbox rows stay | Automatic: redelivery, outbox retry |
| Broker unreachable from a service | Outbox keeps messages; consumers reconnect | Automatic when the broker returns |
| One RabbitMQ node lost (3-node cluster) | Quorum queues keep a majority copy | Automatic |
| RabbitMQ majority or all nodes lost (queues gone) | Job state is in Postgres; owed steps are re-emitted | `StuckJobRecoveryWorker` after lease expiry; jobs may hit their cutoff or deadline meanwhile |
| Postgres primary fails over to a synchronous standby | Commits are acknowledged only after the standby has them (`synchronous_commit = on` with `synchronous_standby_names`) | Automatic failover; in-flight transactions fail and are redelivered |
| Valkey lost | Cache only | Cold cache; results recomputed |
| Duplicate delivery or redelivery | Inbox and CAS turn it into a no-op | Automatic |

**Not covered, or covered with a stated loss**

| Situation | What can be lost | Recovery point |
| --- | --- | --- |
| Postgres primary and standby both lost | Commits after the last archived WAL segment | RPO = WAL archive interval (target ≤ 5 min, production only) |
| Local Aspire setup (single Postgres, single RabbitMQ, data volumes) | Nothing across process or container restarts; everything if the data volumes are deleted | No replication locally |
| In-flight external work when a worker dies (LLM call, search, fetch) | Nothing persisted, but the work is redone and **billed again**; a regenerated plan or report may differ | Redelivery |
| Results arriving for a terminal job (cancelled, expired, already completed) | Discarded **on purpose** (§10.1); not counted as loss | — |
| Telemetry (logs, traces, metrics) | Dropped under exporter back-pressure or collector outage | Best effort, not part of the guarantee |
| Fetched document blob written but the metadata commit fails | Nothing: the blob is orphaned (kept, no cleanup, D21); the redelivered message writes a new blob | Redelivery |
| Blob storage lost (Azurite data volume deleted locally; storage account lost in production) | Document content; indexed passages in Postgres survive | Re-fetch from the source URL |

**Configuration the guarantee depends on** (verified by integration tests or startup checks):
Postgres `synchronous_commit = on` with a synchronous standby (production); RabbitMQ quorum queues, persistent
messages, publisher confirms, manual ack; outbox marks rows published only after confirm; consumers commit
before ack; Fetching writes the blob **before** committing the metadata row and outbox message, so a
`DocumentFetched` reference never points at a missing blob; Aspire data volumes for Postgres, RabbitMQ and
Azurite locally.

**How it is tested:** chaos tests (§18) kill a worker mid-message, restart RabbitMQ with its queues purged,
and restart Postgres mid-job; every job must still reach a terminal state, with no duplicated step effects.

---

## 17. Edge cases

**Job lifecycle:** duplicate `POST` (idempotency key) · cancel during fan-out · cancel after completion · deadline
passes mid-run · budget exhausted · planner returns 0 or too many sub-questions · all searches return nothing ·
fewer than `MinSources` succeed · report requested before completion.

**Messaging:** duplicate delivery · out-of-order events · late results for cancelled/expired jobs · poison
messages · broker down at publish or consume · message over broker size (claim check) · retries exhausted · lost
message · re-emitted step racing a delayed original.

**Concurrency:** simultaneous fan-in completions (optimistic version) · lost updates · background workers on many
replicas (`SKIP LOCKED`) · same URL from several sub-questions · same document across jobs (content hash).

**External:** LLM 429/529 · output truncated at max tokens · malformed plan JSON (one repair retry) · LLM refusal ·
synthesis context too large (cap top-K, map-reduce fallback) · search provider down or out of quota · embedding
model change (store model per vector, re-embed, never mix).

**Fetching:** SSRF incl. redirects and DNS rebinding · huge or endless bodies · binary/unsupported content ·
paywalls and JS-only pages · wrong charset · robots disallow · redirect loops · slow/hostile domains · duplicate
content at different URLs · prompt injection in fetched pages (treat source text as data, never instructions).

**Data:** failover mid-transaction · pool exhaustion · schema behind code during rollout · orphaned blob (logged;
cleanup deferred, D21) · missing blob · unbounded table growth (no retention, D21) · read-replica lag.

**Search (Anthropic web search):** server-tool error returned as HTTP 200 · `max_uses` reached · results with
no usable URL · refusal on the search request · quota shared with Planning/Synthesis exhausted.

**Operations:** scale-in during work · mixed versions during rollout · shared LLM quota · traffic bursts · clock
skew (use DB `now()` for leases and deadlines) · invalid config on reload · secret rotation.

**Security:** sensitive data in logs (§15.2) · internals in error responses · SSRF from fetched URLs (above).
Authentication and tenancy are the gateway's concern (D20).

---

## 18. Testing

| Suite | Tooling | Covers |
| --- | --- | --- |
| Unit (per service) | xUnit v3, Shouldly, hand-made fakes in `Research.TestSupport`, `FakeTimeProvider` | Handlers, state machine, policies, chunker, SSRF guard, failure classification |
| Integration (Jobs, Fetching, Knowledge) | `WebApplicationFactory<Program>`, Testcontainers (Postgres+pgvector, RabbitMQ) | Real composition root, SQL, outbox/inbox, OpenAPI contract tests |
| Architecture | reflection + project-file inspection | §5 reference rules, suffixes, slice layout, features registered once and never ordering, `AuditableEntity`, error-code prefixes, no credential parameters in `[LoggerMessage]`, content logged only through `Research.Content.*` categories |
| End-to-end | `Aspire.Hosting.Testing` on the real AppHost with fake LLM/search providers | Full job flow, cancellation, deadlines, partial results |
| Chaos | end-to-end + container kill/pause | The §16.1 scenarios: kill a worker mid-message, purge and restart RabbitMQ, restart Postgres mid-job → every job reaches a terminal state, no duplicated effects; plus the §10.1 races (cancel vs. report, deadline vs. report) |
| Load | k6 | Throughput, KEDA scaling, LLM limiter behavior |

No mocking library. Test names are sentences (`Duplicate_delivery_is_processed_once`).

---

## 19. Deployment

**Now (D22):** local only — `dotnet run` on `Research.AppHost` starts every service, Postgres, RabbitMQ,
Valkey and Azurite; the `db-scripts` resource applies the SQL scripts first. Everything below is the target
design for later.

- Target **.NET 10 (LTS)** and the current Aspire release.
- `aspire publish` generates Kubernetes manifests; reviewed and committed under `deploy/k8s/`
  (Azure Container Apps via `azd` is the alternative).
- Order per release: `deploy/db` script runner → rollout (maxUnavailable 0) → readiness gates traffic.
- KEDA `rabbitmq` scalers per worker queue; HPA min 2 for every service; pod anti-affinity across zones;
  PodDisruptionBudgets.
- Secrets from the platform secret store (Key Vault / Kubernetes secrets); never in `appsettings*.json`.
- CI: `dotnet build -p:ContinuousIntegrationBuild=true`, all tests, `dotnet format --verify-no-changes`,
  OpenAPI contract check, locked restore.

---

## 20. Packages

All versions in `Directory.Packages.props`, grouped and labeled. Check the license of each exact version before
adding. Excluded (commercial): FluentAssertions 8+, MediatR 13+, AutoMapper 15+, MassTransit 9+,
JsonSchema.Net 8+.

| Group | Packages |
| --- | --- |
| Aspire hosting | `Aspire.Hosting.AppHost`, `.PostgreSQL`, `.RabbitMQ`, `.Valkey`, `.Azure.Storage` |
| Aspire clients | `Aspire.Npgsql`, `Aspire.RabbitMQ.Client`, `Aspire.StackExchange.Redis` (for Valkey), `Aspire.Azure.Storage.Blobs` |
| Web | `FastEndpoints`, `FastEndpoints.OpenApi` |
| Data | `Dapper`, `Npgsql`, `Pgvector` |
| Resilience / discovery | `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery` |
| Content | `AngleSharp` |
| Config | `Microsoft.FeatureManagement` |
| Observability | `OpenTelemetry.Extensions.Hosting`, `.Exporter.OpenTelemetryProtocol`, `.Instrumentation.AspNetCore`, `.Instrumentation.Http`; `Serilog.Extensions.Logging`, `.Settings.Configuration`, `.Formatting.Compact`, `.Sinks.Console`, `.Sinks.OpenTelemetry` |
| Analyzers | `Meziantou.Analyzer`, `Roslynator.Analyzers` |
| Tests | `xunit.v3`, `Shouldly`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq`, `Aspire.Hosting.Testing` |
| LLM and web search | `Anthropic` (official C# SDK) |
| Embeddings | none — mock adapter (D18) |

Not used: redaction packages (D16), any ORM with migrations, mediator, auto-mapper, Swagger UI.

---

## 21. Implementation phases

| Phase | Deliverable | Done when |
| --- | --- | --- |
| 0. Foundation | Solution, build files, `global.json`, `.editorconfig`, `Directory.Packages.props`, `docs/` skeleton, SharedKernel, Contracts, Hosting features, empty AppHost with Postgres/RabbitMQ/Valkey/Azurite | `dotnet build` (CI mode) green; architecture tests for §5 pass |
| 1. Messaging backbone | `Research.Messaging`, `Providers.RabbitMq`, `Providers.Postgres` (outbox/inbox), retry/DLQ topology | Integration test: publish → consume → duplicate ignored → transient retried → poison dead-lettered |
| 2. Jobs | Module, Postgres adapter + `db/` scripts, Api, Host, state machine, workers, OpenAPI contract | Create/get/cancel via API; state machine unit tests; contract test |
| 3. Planning + Synthesis | Modules, Hosts, `Providers.Anthropic` with rate limiter | End-to-end with fake search: plan → synthesize |
| 4. Search, Fetching, Knowledge | Modules, `AnthropicWebSearch` strategy + decorators, mock embeddings, blob claim check, pgvector, retrieval API | Full end-to-end job with fake LLM and fake search; one manual run against real Anthropic |
| 5. Observability | `*Tracing`/`*Metrics` everywhere, verified in the Aspire dashboard | One job is followable across services in the Aspire dashboard |
| 6. Hardening | Chaos tests, security review, edge cases §17 | Chaos suite green locally |
| Later | Production hosting (§19), collector/Grafana/alerts, KEDA, PDBs, load tests, retention, real embeddings, more search providers | — |

---

## 22. Open questions

**Resolved**

| Question | Decision |
| --- | --- |
| Cache server | Valkey (D17) |
| Embeddings provider | Mock adapter for now (D18, §8.2) |
| Search provider | Anthropic web search behind a strategy port (D19, §8.1) |
| Gateway | Black box: no auth, tenancy, forwarded headers or audit in our services (D20) |
| Hosting target | Local Aspire only for now; §19 kept as the later design (D22) |
| Retention | None for now (D21) |
| Client notifications | SSE only (D23) |
| LLM models | `claude-sonnet-5-5` at effort `medium` for planning, synthesis and web search (§8.3) |
| Document transport | Claim check over blob storage, Azurite locally (D24) |

**Still open:** none.

---

## 23. Implementation deviations

The code is the source of truth. Where it differs from the sections above, this table records the change and why.

| Area | Plan said | Code does | Why |
| --- | --- | --- | --- |
| Endpoints | FastEndpoints | Minimal API endpoint classes (D25) | Fewer dependencies |
| Job states | `Pending` first | Jobs start in `Planning` | The plan step is emitted in the create transaction, so `Pending` never existed long enough to observe |
| `job_sources` table | Separate source list | Dropped; `job_steps` (unique `(job_id, step_key)` with `fetch:{hash(url)}`) deduplicates URLs | One table fewer, same guarantee |
| Content logging section | `Logging:Content` | `ContentLogging:MaxChars` | A separate section avoids clashing with `Logging` provider config |
| Search errors | `search.no_results` | Removed (an empty `ResultsFound` is a valid answer); codes are `invalid_query`, `quota_exhausted`, `provider_refused` | Empty results are not failures |
| LLM errors | `planning.llm_unavailable`, `synthesis.llm_unavailable`, `synthesis.context_too_large` | Transient failures retry and end as `research.step_exhausted`; context is trimmed to `MaxContextChars`; added `synthesis.no_passages`, `synthesis.empty_report` | One code per real outcome |
| Jobs errors | — | Added `jobs.question_invalid`, `jobs.plan_empty`, `jobs.report_not_available` | Cases found while implementing |
| Fetching errors | — | Added `fetching.invalid_url`, `fetching.http_error`; robots code is `fetching.fetch_disallowed` | Distinguish permanent 4xx from transient 5xx |
| Knowledge errors | `knowledge.embedding_model_mismatch` | `knowledge.embedding_dimension_mismatch`, `knowledge.no_passages`, `knowledge.invalid_search` | Documents are keyed by model, so a model mismatch cannot happen; a size mismatch can |
| Rate limiting | Token-bucket rate limiter | Concurrency limiter only (`MaxConcurrentRequests`, `MaxConcurrentSearches`, `PerDomainConcurrency`) | Simpler; throttling responses are retried by the pipeline |
| Synthesis | Streaming | Non-streaming, `MaxTokens` 16000; truncation is a warning | Simpler and enough for the report size |
| Refusals | Server-side fallback | Client-side: one retry on `RefusalFallbackModel` | Works the same with any SDK version |
| Valkey/blob health | Aspire checks | Custom checks reporting **Degraded** | Aspire's checks report Unhealthy and would pull replicas for an optional dependency |
| SSE | Push from events | Polls the job row every `EventsPollInterval` | No extra infrastructure; any replica can serve any stream |
| Clocks | Database `now()` | `TimeProvider` in .NET | Testable with a fake clock |
| Stateless services | Outbox everywhere | Planning, Search, Synthesis publish directly with confirms, then ack | No database to hold an outbox; Jobs' inbox + step CAS absorb duplicates |
| Gauges | Outbox age, stuck jobs | Not implemented | Need a database poll per scrape; recovery worker logs and `research.steps.reemitted` cover it |
| Provider registration | `RabbitMqFeature` | `RabbitMqServiceCollectionExtensions.AddPublisher/AddConsumer` helpers called from each Host's `MessagingFeature` | The provider must not reference `Research.Hosting` |
| Options reload | Reload without restart | Every option reloads (`IOptionsSnapshot` in scoped code, `IOptionsMonitor` in singletons); `Messaging:ServiceName` and `RabbitMq:Exchange` stay at their startup value and warn on change | They name where messages live; changing them live strands queued messages. See [configuration.md](configuration.md#reloading) |
| Web search results | Typed SDK blocks | Response serialized to JSON and read by `WebSearchResultReader` | Avoids depending on SDK union types for server tool results |
| Fetching HTTP client | Standard resilience handler | No resilience handler; `SsrfGuard` as `ConnectCallback`; retries via the message pipeline | Retrying arbitrary sites inside a handler would stack with message retries |
| Schema tool | Container resource | `tools/Research.DbScripts` project resource with `WaitForCompletion` | Same language, same build |
| Agent security | Not covered | OWASP agentic Top 10 controls: prompt fences, untrusted-text cleaning, hidden-text extraction, report sanitizing, uncited reports rejected (`synthesis.uncited_report`), message validation in Jobs, `Jobs:MaxActiveJobs` (`jobs.capacity_reached`, new `ErrorKind.Unavailable` → 503), `Fetching:BlockedDomains` / `RejectSuspectedInjection`, `nuget.config` source mapping. See docs/security.md | The agent reads hostile web content and its reports are trusted by people |
