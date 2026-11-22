# Architecture

[PLAN.md](PLAN.md) records the original design decisions (D1–D25) and the reasons for them. This file describes the code as it is today.

## Reference rules

Enforced automatically by [ReferenceRuleTests.cs](../tests/Research.ArchitectureTests/ReferenceRuleTests.cs):

```text
AppHost ──► *.Bootstrap, *.Worker, tools/Research.DbScripts
*.Bootstrap / *.Worker ──► own *.Application, *.Infrastructure, (own *.Api), Research.ServiceDefaults,
                           Research.Shared.Core, Research.Shared.Bus, (Research.Shared.Web if HTTP), Providers.*
*.Api ──► own *.Application, Research.Shared.Web                                  (never ServiceDefaults, Domain, or Infrastructure)
*.Infrastructure ──► own *.Application, Research.Shared.Core, Research.Shared.Bus, Providers.*
*.Application ──► own *.Domain, Research.Shared.Core, Research.Shared.Bus,
                  Research.Shared.Contracts                                       (never ASP.NET Core, Aspire, or providers)
*.Domain ──► Research.Shared.Kernel                                               (zero NuGet packages)
Providers.* ──► Research.Shared.Core / Research.Shared.Bus / Research.Shared.Contracts
Research.Shared.Web ──► Research.Shared.Kernel, Research.Shared.Core              (HTTP hosts and *.Api only)
Research.ServiceDefaults ──► (zero project references)                            (entry points only)
Research.Shared.Bus ──► Research.Shared.Kernel, Research.Shared.Core, Research.Shared.Contracts
Research.Shared.Core ──► Research.Shared.Kernel
Research.Shared.Kernel / Research.Shared.Contracts ──► (no references at all)
```

Services never reference each other. Shared libraries are split by capability:
* `Research.Shared.Kernel`: pure DDD base types (`Entity`, `AggregateRoot`, `IDomainEvent`, `IHasDomainEvents`) and `Result`/`Error`/`Unit` primitives. Zero packages.
* `Research.Shared.Contracts`: additive-only integration message records (`IIntegrationMessage`, `MessageKind`, `MessageCatalog`). Zero packages.
* `Research.Shared.Core`: shared platform plumbing (`Hosting`, `Actors`, `Correlation`, `Errors`, `Logging`, `Security`, `Concurrency`, `Http`) and external capability ports (`Llm`, `Search`, `Documents`, `Embeddings`, `Caching`).
* `Research.Shared.Bus`: unified in-process and out-of-process bus (`Mediation`, `Transactions`, `Events`, `Messaging`), exposing public abstractions and DI registration (`BusServiceCollectionExtensions`, `BusFeature`) while keeping runtime implementations (`Mediator`, pipeline behaviors, `EnvelopeFactory`, `MessageDispatcher`, `OutboxPublisherWorker`, `DirectMessageSender`) `internal`.

Service-specific ports live in `Research.<Service>.Application/Common/Interfaces/`. Pure domain rules live in `Research.<Service>.Domain/Policies/` (`JobStateMachine`, `JobOutcomePolicy`, `TextChunker`, `UrlCanonicalizer`), while stateless LLM services (`Planning`, `Synthesis`) keep their prompt/citation policies in `*.Application`. `Research.Providers.*` and `Research.<Service>.Infrastructure` implement the ports. Only entry points (`*.Bootstrap`, `*.Worker`) wire concrete implementations; where a port has multiple providers (`Llm:Provider`), the host picks one from startup settings.

## Service defaults and shared web

* **`Research.ServiceDefaults`** is a standard zero-project-reference .NET Aspire shared project (`AddServiceDefaults` / `MapDefaultEndpoints`) covering structured logging (Serilog + OpenTelemetry + optional Elasticsearch), OpenTelemetry tracing and metrics over OTLP, health checks (`/health/live`, `/health/ready`), service discovery, and default HTTP client resilience.
* **`Research.Shared.Web`** holds HTTP-only presentation primitives (`IEndpointGroup`, `IEndpoint`, `EndpointGroupExtensions`, `IEndpointValidator<T>`, `EndpointValidationFilter<T>`, `ResultHttpExtensions`, `ProblemResults`, `ApiErrors`, `ErrorStatusCodeMapper`, `GlobalExceptionHandler`, `CorrelationIdMiddleware`, `HttpContentLoggingMiddleware`, `AddResearchProblemDetails`) and is referenced only by HTTP APIs (`Research.Jobs.Api`, `Research.Knowledge.Api`) and HTTP hosts (`Research.Jobs.Bootstrap`, `Research.Knowledge.Bootstrap`).

## Endpoints and two-stage validation

HTTP APIs follow the REPR pattern using native Minimal APIs: each `IEndpointGroup` owns the route prefix and OpenAPI tag and delegates to self-contained `IEndpoint` classes that co-locate their request/response models, route, OpenAPI metadata, and thin `ISender` delegation (`ToHttpResult` / `ToOkOrProblem`). Use cases return immutable DTOs (`ResearchJobDto`, `ResearchReportDto`, `PassageMatchDto`) so `*.Api` projects never depend on `*.Domain`, while `ResearchJobRepository` tracks persisted CAS snapshots inside its DI scope so `ResearchJob` stays free of persistence-tracking state.

Validation is intentionally split across two boundaries:

1. **Stage 1 — HTTP transport validation (`EndpointValidationFilter<T>` + `IEndpointValidator<T>`):** runs as a Minimal API `IEndpointFilter` **before** the endpoint method executes, checking syntactic/HTTP-only rules (missing body, required fields, `Idempotency-Key` header format) and short-circuiting with `400 research.validation_failed` (`ValidationProblemDetails`).
2. **Stage 2 — Application/policy validation (`ValidationBehavior<,>` + `IRequestValidator<T>`):** runs inside the mediation pipeline **before** the application handler executes, enforcing entry-point-agnostic policy rules (`JobsOptions`, `KnowledgeOptions`) and returning typed `Result<T>.Failure(...)` outcomes.

## Host shape (composition root)

```text
Research.<Service>.Bootstrap/ (or Research.Jobs.Worker/)
  Program.cs                              builder → root.ComposeServices() → Build → ComposePipeline → ComposeEndpoints → Run
  CompositionRoot/
    <Service>CompositionRoot.cs           AddServiceDefaults + Features list + MapDefaultEndpoints
    <Service>CompositionRoot.Pipeline.cs  middleware order (HTTP-facing hosts)
    <Service>Settings.cs                  every options section, bound and validated once via SettingsBinder
  Features/                               one IResearchFeature per capability (PostgresFeature, MessagingFeature, ...)
  Workers/                                timer loops (Research.Jobs.Worker only)
```

## Module shape and unified mediation pipeline

Each service's `Research.<Service>.Application` and `Research.<Service>.Api` projects organize use cases, domain event handlers, and HTTP endpoints under `FeatureSlices/<SliceName>/` (omitted from namespaces, one top-level type per file), with each API's root `<Service>Endpoints` (`IEndpointGroup`) at the project root.

Commands (`ICommand<TResponse>` / `ICommand`), queries (`IQuery<TResponse>`), integration messages (`IncomingMessageEnvelope<TMessage>` via `IMessageHandler<TMessage>`), and domain events (`IDomainEvent` via `IDomainEventHandler<TEvent>`) all flow through a custom, zero-third-party-package mediator (`Research.Shared.Bus.Mediation.Mediator`) implementing `IMediator` (`ISender` + `IPublisher`) and `IDomainEventDispatcher`:

```text
HTTP Endpoint / Worker Timer / RabbitMQ MessageDispatcher
  ──► ISender.SendAsync(...)
        ──► LoggingBehavior
          ──► TelemetryBehavior (Research.Mediation traces & metrics)
            ──► InboxBehavior (fast-path duplicate skip for IncomingMessageEnvelope<T> when IInbox is registered)
              ──► ValidationBehavior (IRequestValidator<T> strategies → typed Result failure)
                ──► TransactionBehavior (opt-in IUnitOfWork wrapper for ITransactionalCommand)
                  ──► IRequestHandler<TRequest, TResponse>

PostgresUnitOfWork.CommitAsync()
  ──► IPublisher.PublishAsync(domainEvent)
        ──► EventLoggingBehavior
          ──► EventTelemetryBehavior
            ──► 0..N IDomainEventHandler<TEvent>
```

Expected business outcomes are returned as `Result` / `Result<T>` values (`IOperationResult` / `IResultFactory<TSelf>`). Exceptions are reserved for transient or unexpected failures, which `MessageDispatcher<TMessage>` retries via RabbitMQ delay queues.

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
8. **Supervision.** The recovery worker dispatches `RecoverStuckJobsCommand` to re-emit expired steps. The deadline
   worker dispatches `EnforceJobDeadlinesCommand` to enforce the cutoff and expire overdue jobs. Cancellation is a
   compare-and-set update to `Cancelled`.

Every reply goes through `StepReplyProcessor`, one transaction that does the following in order:

1. records the reply in the inbox;
2. locks the job row;
3. checks whether the job is terminal (a late result is discarded);
4. finishes the step with a compare-and-set update (a duplicate is discarded);
5. runs the slice's `advance` step.

## Fault tolerance

| Concern | Mechanism |
| --- | --- |
| Lost or duplicated message | Outbox + inbox (database services, with `InboxBehavior` fast-path pre-check); confirmed publish + idempotent receiver in Jobs (stateless services); step leases + re-emission |
| Transient dependency failure | Delay-queue retries (10 s / 60 s / 300 s), then `research.step_exhausted` |
| Poison message | Straight to DLQ + `StepFailed(research.poison_message)` |
| Slow or hung job | Synthesis cutoff, hard deadline (`Expired`), handler timeout |
| Runaway cost | Token budget, `MaxSources`, concurrency limiters on LLM and search calls |
| Crash mid-handler | Nothing is committed until the single transaction commits; the message is redelivered |
| Scale-out | Every service is stateless apart from its database; workers use `SKIP LOCKED`; quorum queues; replicas set in the AppHost |
| Malicious URLs | `SsrfGuard` on every connection (also redirects), robots.txt, size/redirect/time limits, per-domain concurrency |
