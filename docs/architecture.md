# Architecture

[PLAN.md](PLAN.md) records the decisions (D1–D25) and the reasons for them. This file describes the code as it is.

## Reference rules

```text
Host ──► Module, Module.Postgres, Module.Api, Providers, Shared
Module.Api ──► Module, Problems
Module.Postgres ──► Module, Providers.Postgres
Module ──► SharedKernel, Contracts, Messaging          (never a provider, never ASP.NET Core)
Providers.* ──► SharedKernel (+ Messaging for RabbitMQ)
AppHost ──► Hosts, tools
```

Modules define ports: interfaces in `Domain/` or in SharedKernel (`ILlmClient`, `IDocumentStore`, `ICacheStore`,
`IEmbeddingGenerator`, `IMessagePublisher`). Providers and `*.Postgres` projects implement them. Only Hosts know the
concrete types. Where a port has more than one provider, the Host picks one from its startup settings: `Llm:Provider`
registers the Anthropic or the OpenAI `ILlmClient`. Code the providers share (`LlmMetrics`, `LlmTracing`, the
concurrency limiter) lives in SharedKernel, because a provider never references another provider.

## Host shape (composition root)

```text
Research.<Service>.Host/
  Program.cs                      builder → root.ComposeServices() → Build → ComposePipeline → ComposeEndpoints → Run
  CompositionRoot/
    <Service>CompositionRoot.cs   the Features list (grouped: observability, platform, infrastructure, module, API)
    <Service>CompositionRoot.Pipeline.cs   middleware order (HTTP-facing Hosts)
    <Service>Settings.cs          every options section, bound and validated once
  Features/                       one IResearchFeature per capability (PostgresFeature, MessagingFeature, ...)
  Workers/                        timer loops (Jobs only)
```

There is no ServiceDefaults project. Its parts are platform features in `Research.Hosting`: `LoggingFeature`,
`TelemetryFeature`, `HealthChecksFeature`, `HttpClientsFeature` (service discovery and standard resilience),
`ActorsFeature` and `ErrorsFeature`. Each composition root lists the ones it uses.

## Module shape

Each module is organized as vertical slices: one folder per use case, with a command or query, one handler, and the
message handler or endpoint that calls it. There is no mediator. Expected outcomes are `Result`/`Result<T>` values,
and exceptions are for transient or unexpected failures, which the message pipeline retries.

## The job, end to end

```text
POST /research ─► Planning ─► Gathering ─► Synthesizing ─► Completed | CompletedWithWarnings
                     │            │              │
                     └────────────┴──────────────┴─► Failed | Cancelled | Expired
```

1. **Create.** `CreateResearchJob` inserts the job in `Planning` with two clocks, `synthesis_cutoff_at` and
   `deadline_at`, emits the `plan` step (`PlanRequested`), and publishes `JobCreated`, all in one transaction.
2. **Plan.** On `PlanReady`, Jobs normalizes the sub-questions (an empty plan gives `jobs.plan_empty`), moves the job
   to `Gathering`, and emits `search:{i}` steps.
3. **Search.** On `ResultsFound`, Jobs emits a `fetch:{hash(url)}` step for each new URL, up to `MaxSources`, and stops
   once the budget is spent. The step key deduplicates URLs per job.
4. **Fetch and index.** Fetching downloads the page, extracts its text and stores it in a blob, then sends
   `DocumentFetched`. Knowledge chunks, embeds and stores the text, links it to the job, and sends `DocumentIndexed`,
   which finishes the fetch step.
5. **End of gathering.** Gathering ends when every search and fetch step has finished, at the cutoff, or when the
   budget runs out:
   - with 0 sources, the job fails with `jobs.no_sources_found`;
   - with fewer than `MinSources`, it fails with `jobs.insufficient_sources`;
   - otherwise it moves to `Synthesizing` and emits `SynthesisRequested` exactly once.
6. **Synthesize.** Synthesis retrieves the top passages over HTTP, trims them to the context limit, and writes a report
   that cites sources as `[n]`. On `ReportReady`, Jobs stores the report and completes the job. Warnings are
   `cutoff_reached`, `budget_exhausted`, `steps_failed` and `output_truncated`.
7. **Failures.** A failed plan or synthesis step fails the job with the worker's code. A failed search or fetch step
   only adds the `steps_failed` warning.
8. **Supervision.** The recovery worker re-emits expired steps. The deadline worker forces the cutoff and expires
   overdue jobs. Cancellation is a compare-and-set update to `Cancelled`.

Every reply goes through `JobReplyGate`, one transaction that does the following in order:

1. records the reply in the inbox;
2. locks the job row;
3. checks whether the job is terminal (a late result is discarded);
4. finishes the step with a compare-and-set update (a duplicate is discarded);
5. runs the slice's `advance` step.

## Fault tolerance

| Concern | Mechanism |
| --- | --- |
| Lost or duplicated message | Outbox + inbox (database services); confirmed publish + idempotent receiver in Jobs (stateless services); step leases + re-emission |
| Transient dependency failure | Delay-queue retries (10 s / 60 s / 300 s), then `research.step_exhausted` |
| Poison message | Straight to DLQ + `StepFailed(research.poison_message)` |
| Slow or hung job | Synthesis cutoff, hard deadline (`Expired`), handler timeout |
| Runaway cost | Token budget, `MaxSources`, concurrency limiters on LLM and search calls |
| Crash mid-handler | Nothing is committed until the single transaction commits; the message is redelivered |
| Scale-out | Every service is stateless apart from its database; workers use `SKIP LOCKED`; quorum queues; replicas set in the AppHost |
| Malicious URLs | `SsrfGuard` on every connection (also redirects), robots.txt, size/redirect/time limits, per-domain concurrency |

## Unverified APIs

None of this code has been compiled. These calls were written from documentation or memory, so check them first:

| Where | What to verify |
| --- | --- |
| `Directory.Packages.props` | Every version exists and is mutually compatible (Aspire 13.0.0, Anthropic 13.1.0, Npgsql 9.0.4, RabbitMQ.Client 7.1.2, ...) |
| `AnthropicLlmClient` | `client.Messages.Create(parameters, cancellationToken:)`; `OutputConfig` / `Effort` / `JsonOutputFormat` shapes; `StopReason` string comparison; `CacheControlEphemeral`; `AnthropicRateLimitException` namespace |
| `AnthropicWebSearchProvider` / `WebSearchResultReader` | `WebSearchTool20260209` property types; `JsonSerializer.Serialize(response)` producing the wire shape that the reader parses |
| `RabbitMqListener` / `RabbitMqMessagePublisher` | RabbitMQ.Client 7 `CreateChannelOptions` named arguments, `consumerDispatchConcurrency`, publisher confirm tracking |
| Fetching/Knowledge Hosts | `AddAzureBlobServiceClient(name, configureSettings)` name and the `DisableHealthChecks` setting |
| `ResearchJobEventsEndpoint` | `TypedResults.ServerSentEvents` and `SseItem<T>` |
| Jobs/Knowledge repositories | Dapper mapping of `text[]` and `DateTimeOffset` (handlers registered in `DapperTypeHandlers`); `unnest(... ::text[])` → `::vector` cast |
| `AppHost.cs` | `WithImage`/`WithImageTag` on Postgres, `WaitForCompletion`, `WithReplicas` with Aspire 13 |
| `ContentExtractor` | AngleSharp 1.x `HtmlParser.ParseDocument(Stream)` encoding detection |
