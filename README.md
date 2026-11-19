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

The commands below are for Linux (bash). They work the same on macOS and in WSL; on Windows, use the same
`dotnet` commands in any shell.

### Prerequisites

- **The .NET 10 SDK.** Install it from your distribution's packages or with Microsoft's install script; see
  [Install .NET on Linux](https://learn.microsoft.com/dotnet/core/install/linux). Aspire 13 needs no workload; the
  `aspire` CLI is optional. `global.json` accepts any 10.0.1xx SDK or later feature band.
- **A container runtime.** Aspire runs Postgres (`pgvector/pgvector:pg17`), RabbitMQ, Valkey and Azurite in
  containers.
  - **Docker:** your user must be able to reach the daemon without `sudo` (in the `docker` group, or rootless
    Docker). Check with `docker info`.
  - **Podman:** tell Aspire to use it, and expose the Podman socket for the Testcontainers-based tests:

    ```bash
    export ASPIRE_CONTAINER_RUNTIME=podman
    systemctl --user enable --now podman.socket
    export DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock
    export TESTCONTAINERS_RYUK_DISABLED=true
    ```

- **An Anthropic API key.**
- **`curl`**, and **`jq`** for the examples below.

### Raise the file-watcher limit (once per machine)

Every service watches its `appsettings*.json` so configuration reloads without a restart. Seven processes plus the
dashboard can exceed Linux's default of 128 inotify instances per user, which fails startup with
*"The configured user limit (128) on the number of inotify instances has been reached"*. Raise the limit once:

```bash
echo fs.inotify.max_user_instances=512 | sudo tee /etc/sysctl.d/90-inotify.conf && sudo sysctl --system
```

If you cannot change system settings, set `DOTNET_USE_POLLING_FILE_WATCHER=1` instead; it polls rather than
watching, at a small CPU cost.

### Store the API key

Once, in the AppHost's user secrets. They are stored outside the repository, in
`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`:

```bash
dotnet user-secrets set "Parameters:anthropic-api-key" "<key>" --project src/Research.AppHost
```

### Start everything

```bash
dotnet run --project src/Research.AppHost --launch-profile http
```

The `http` profile avoids the HTTPS development certificate, which Linux trusts only partly:
`dotnet dev-certs https --trust` covers some distributions and tools, but browsers and `curl` often still reject it.
To use the default `https` profile, run `dotnet dev-certs https --trust`, and if your browser still warns, import the
certificate into it by hand, or pass `-k` to `curl`.

The console prints the dashboard's login link. The Aspire dashboard shows every resource, log, trace and metric. The
`db-scripts` resource applies the schema scripts and exits. Services that own a database start only after it has
finished.

### Try it

Set `JOBS` to the `jobs` endpoint shown in the dashboard, then create a job and keep its id:

```bash
JOBS=http://localhost:<port>
```

```bash
ID=$(curl -s -X POST "$JOBS/research" \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-1' \
  -d '{"question":"What are the trade-offs of HNSW versus IVFFlat indexes in pgvector?"}' | jq -r .id)
```

Follow its progress (server-sent events; stop with Ctrl+C), then read the report:

```bash
curl -N "$JOBS/research/$ID/events"
```

```bash
curl -s "$JOBS/research/$ID/report" | jq
```

### Stop and reset

Ctrl+C stops the AppHost and its containers. The Postgres, RabbitMQ, Valkey and Azurite data lives in named volumes
and survives restarts. To start from an empty state, remove those volumes. List them with
`docker volume ls` (or `podman volume ls`), then remove the ones this AppHost created with `docker volume rm <name>`.

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

The test runner is Microsoft.Testing.Platform (see `global.json`). Run one project at a time:

```bash
dotnet test --project tests/Research.UnitTests
```

```bash
dotnet test --project tests/Research.ArchitectureTests
```

```bash
dotnet test --project tests/Research.IntegrationTests
```

The end-to-end tests run the full Claude-backed flow only when the key is in the environment (otherwise that test is
skipped):

```bash
ANTHROPIC_API_KEY=<key> dotnet test --project tests/Research.EndToEndTests
```

The integration and end-to-end tests start containers; with Podman, set the variables shown under
[Prerequisites](#prerequisites) first.

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

## Line endings

Every text file uses LF, on every OS. `.editorconfig` sets `end_of_line = lf`, and `.gitattributes` makes git store
and check out LF everywhere (`* text=auto eol=lf`), so `dotnet format --verify-no-changes` gives the same answer on
Linux, Windows and CI. On Windows, an editor that writes CRLF is corrected by git on commit.

## Before the first build


1. Run `dotnet restore`. Check that every version in `Directory.Packages.props` exists, and fix any that does not.
   Commit the `packages.lock.json` files that the restore writes.
2. Run `dotnet build`, then fix what the compiler reports. The places most likely to need changes are listed under
   *Unverified APIs* in [docs/architecture.md](docs/architecture.md#unverified-apis).
3. Run `dotnet build -p:ContinuousIntegrationBuild=true`. This build treats every warning as an error, including
   analyzer warnings.
