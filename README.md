# Research Agent

A research agent built as .NET 10 microservices on .NET Aspire 13. You send a question; the system plans
sub-questions, searches the web, fetches and indexes pages, and writes a cited report.

> **Status:** the code is written but has **never been compiled or run**. Package versions in
> `Directory.Packages.props` and several third-party API shapes are unverified. See
> [Before the first build](#before-the-first-build).

## Services

| Service | Role | State | HTTP |
| --- | --- | --- | --- |
| `jobs` | Public API and orchestrator: job state machine, steps, deadlines, recovery | Postgres `research_jobs` | `/research/*` (the gateway's target), SSE |
| `planning` | Splits the question into sub-questions (Claude, structured output) | Stateless; Valkey cache | health only |
| `search` | Web search through `ISearchProvider` (Anthropic web search today) | Stateless; Valkey cache | health only |
| `fetching` | Downloads pages safely, extracts text, stores it in blob storage | Postgres `research_fetching`, blobs | health only |
| `knowledge` | Chunks, embeds (mock model) and indexes pages; passage search | Postgres `research_knowledge` (pgvector) | `/knowledge/passages/search` (internal) |
| `synthesis` | Writes the cited report from retrieved passages (Claude) | Stateless | health only |

They communicate through RabbitMQ (topic exchange `research.events`). Jobs sends a request to a worker service, and
the worker replies. Synthesis also calls the Knowledge API over HTTP through Aspire service discovery.

```text
client ─► gateway ─► jobs ──JobCreated/PlanRequested──► planning ──PlanReady──► jobs
                       │──SearchRequested──► search ──ResultsFound──► jobs
                       │──FetchRequested──► fetching ──DocumentFetched──► knowledge ──DocumentIndexed──► jobs
                       └──SynthesisRequested──► synthesis ──(HTTP) knowledge── ReportReady ──► jobs
```

## Run locally

Prerequisites:

- the .NET 10 SDK;
- Docker or Podman, which Aspire uses to run Postgres (`pgvector/pgvector:pg17`), RabbitMQ, Valkey and Azurite;
- an Anthropic API key.

Store the key once, in the AppHost's user secrets:

```bash
dotnet user-secrets set "Parameters:anthropic-api-key" "<key>" --project src/Research.AppHost
```

Then start everything:

```bash
dotnet run --project src/Research.AppHost
```

The Aspire dashboard shows every resource, log, trace and metric. The `db-scripts` resource applies the schema
scripts and exits. Services that own a database start only after it has finished.

Try it, replacing `<jobs>` with the `jobs` endpoint shown in the dashboard:

```bash
curl -X POST <jobs>/research -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" -d "{\"question\":\"What are the trade-offs of HNSW versus IVFFlat indexes in pgvector?\"}"
```

```bash
curl -N <jobs>/research/<id>/events
```

```bash
curl <jobs>/research/<id>/report
```

## Repository layout

```text
Research.slnx                 everything; filters/*.slnf open one service with its dependencies
src/Research.AppHost          local topology (Aspire)
src/Shared                    SharedKernel, Contracts, Messaging, Hosting (platform features), Problems, Http
src/Providers                 adapters: Anthropic, RabbitMQ, Postgres, Valkey, BlobStorage, MockEmbeddings
src/<Service>                 <Service> module · <Service>.Postgres · <Service>.Api · <Service>.Host
tools/Research.DbScripts      applies db/*.sql per database
docs/                         architecture, configuration, errors, messaging, database, observability, security
```

Every Host has the same shape:

- `Program.cs` calls `ComposeServices`, `ComposePipeline` and `ComposeEndpoints`;
- `CompositionRoot/` holds the root and the settings binder;
- `Features/` holds one file per capability.

See [docs/architecture.md](docs/architecture.md).

## Security

The agent reads pages written by strangers, so every page, search result and model answer is treated as hostile:

- untrusted text enters prompts only inside fenced elements with a random name;
- text hidden from readers is dropped from fetched pages;
- reports are sanitized (no HTML, images, links or invented citations), and a report that cites nothing is rejected;
- other services' messages are validated, and the number of active jobs is capped.

[docs/security.md](docs/security.md) maps each control to the OWASP Top 10 for agentic applications and lists what is
left to the gateway and the deployment.

## Tests

| Project | What it covers | Needs |
| --- | --- | --- |
| `tests/Research.UnitTests` | State machine and outcome policy, step keys, SSRF guard, robots rules, extraction, `FetchPageHandler` (fake site), chunking, mock embeddings, passage search merge, URL canonicalization, web search result parsing, citations, error catalog, message dispatcher outcomes; the OWASP controls (text cleaning, prompt fences, injection signals, hidden-text extraction, report sanitizing) | nothing |
| `tests/Research.ArchitectureTests` | Reference rules (§5), Host shape, no secrets in appsettings | nothing |
| `tests/Research.IntegrationTests` | Outbox/inbox, fetched documents, pgvector search on the real schema scripts; RabbitMQ retry through delay queues, exhaustion → `StepFailed` + DLQ | Docker |
| `tests/Research.EndToEndTests` | The real AppHost: validation, 404, idempotency, cancel, SSE; the full Claude-backed flow when `ANTHROPIC_API_KEY` is set | Docker (+ key) |

Run one project with `dotnet test --project tests/Research.UnitTests`. The test runner is Microsoft.Testing.Platform
(see `global.json`).

## CI/CD

The workflows live in `.github/workflows`:

- **`ci.yml`** runs on every push and pull request:
  1. a locked restore;
  2. a build that treats warnings as errors;
  3. `dotnet format --verify-no-changes`;
  4. the unit and architecture tests, then the integration tests and the end-to-end API tests.

  The full flow against Claude runs only when the workflow is started by hand with `run_full_flow`, and it uses the
  `ANTHROPIC_API_KEY` repository secret.
- **`release.yml`** runs on a `v*.*.*` tag. It verifies the build, then publishes a container image for each service
  and for the schema tool to `ghcr.io/<owner>/research-<service>`, using the SDK's container support. There is no
  deploy step yet, because there is no hosting target (D22).

Dependabot proposes NuGet and Actions updates weekly.

## Before the first build

Nothing here has been restored or compiled yet. Work through these steps in order on the first build:

1. Run `dotnet restore`. Check that every version in `Directory.Packages.props` exists, and fix any that does not.
   Commit the `packages.lock.json` files that the restore writes.
2. Run `dotnet build`, then fix what the compiler reports. The places most likely to need changes are listed under
   *Unverified APIs* in [docs/architecture.md](docs/architecture.md#unverified-apis).
3. Run `dotnet build -p:ContinuousIntegrationBuild=true`. This build treats every warning as an error, including
   analyzer warnings.
