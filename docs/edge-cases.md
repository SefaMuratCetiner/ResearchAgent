# Edge Cases

## Job lifecycle

- **Duplicate POST (client retry):** The client sends an `Idempotency-Key` header, unique per tenant, and the original job is returned.
- **Cancel during fan-out:** Status becomes `Cancelling`, and in-flight results are ignored on arrival. Workers check the job status before expensive LLM calls.
- **Cancel after completion:** No-op; returns `409 research.job_already_completed`.
- **Deadline passes mid-run:** `JobExpiryWorker` synthesizes with what is indexed if at least `MinSources` exist; otherwise, the job fails with `research.insufficient_sources`.
- **Token or cost budget exhausted:** Stop the fan-out and synthesize with partial results.
- **Planner returns 0 sub-questions:** Fall back to searching the original question as-is.
- **Planner returns too many:** Truncate to `MaxSubQuestions`.
- **Every search returns 0 hits:** The job fails with `research.no_sources_found` rather than hanging.
- **Fewer than `MinSources` succeed:** The job ends as `CompletedWithWarnings` or `Failed`, depending on policy.
- **Report requested before completion:** Returns `409 research.report_not_ready`.

## Messaging

- **Duplicate delivery:** Stateful services use the inbox; for stateless steps, Jobs ignores a result for a step already done.
- **Out-of-order events (e.g. `DocumentIndexed` before `ResultsFound` is recorded):** The state machine accepts any order, and step rows are upserted.
- **Late result for a cancelled or expired job:** Acknowledge and discard.
- **Poison or unknown contract version:** Send straight to the DLQ with an alert.
- **Broker down at publish:** The outbox keeps the message and retries with a growing delay; nothing is lost. After
  about an hour of failures the message is parked for an operator (see [messaging](messaging.md#outbox-retries)).
- **Event with no consumer:** Events are not published mandatory, so the broker drops them; only a command nobody
  can receive is an error.
- **Broker down at consume:** Listeners reconnect with backoff, and readiness reports unready.
- **Message larger than the broker limit:** Use claim check (send a blob reference); never inline documents.
- **Retries exhausted:** Send to the DLQ and emit `StepFailed`; the job degrades instead of waiting forever.
- **Lost message (step never reported back):** `StuckJobRecoveryWorker` re-emits once the lease expires.
- **Re-emitted step plus a delayed original:** Both arrive, and the step key makes the second one a no-op.

## Concurrency

- **Two fan-in handlers finishing at once:** The optimistic version check means exactly one emits `SynthesisRequested`.
- **Lost update on the job row:** Version conflict → retry the handler from a fresh read.
- **Several replicas running background workers:** `SKIP LOCKED` stops two of them claiming the same rows.
- **Same URL found by several sub-questions:** Deduplicate by URL hash per job before fetching.
- **Same document across jobs:** Knowledge deduplicates by content hash and links it to the new job through `job_documents`.

## External dependencies

- **LLM 429 or 529:** Honor `retry-after`, use a delay queue, and open the circuit breaker on sustained failures.
- **LLM output cut off at max tokens:** Detect the stop reason and continue or retry with a summarized context.
- **LLM output malformed (plan isn't valid JSON):** Retry once with a repair prompt, then emit `StepFailed(research.invalid_plan)`.
- **LLM refuses the question:** Fail with `research.llm_refused`; don't retry.
- **Context too large for synthesis:** Cap the top-K passages, then fall back to map-reduce summarization.
- **Search provider down:** Use hedging or fall back to the second provider, or serve from cache.
- **Search quota exhausted:** Open the circuit and let the job degrade or fail with a clear code.
- **Embedding model changed:** Store the model and dimension with each vector, and re-embed in the background; never mix models in one search.

## Fetching

- **SSRF (private IPs, a redirect to one, a DNS rebind):** Check the resolved IP on every hop.
- **Huge or endless response:** Apply a byte cap, plus a timeout on the streamed read.
- **Unsupported content (binary, video):** Treat as a business failure; don't retry.
- **Paywalls, logins, JS-only pages:** Extracted text below the minimum length means a business failure.
- **Wrong or missing charset:** Detect it, and fall back to UTF-8 with replacement characters.
- **Robots disallow:** Skip, with `research.fetch_disallowed`.
- **Redirect loops:** Enforce a maximum redirect count.
- **Slow or hostile domains:** Apply a per-domain concurrency limit and timeout.
- **Duplicate content at different URLs:** Deduplicate by content hash.
- **Prompt injection in fetched pages:** Wrap source text as data in the prompt, keep instructions separate, and never act on page content.

## Database and storage

- **Primary failover mid-transaction:** The transaction fails as transient, and the message is redelivered.
- **Connection pool exhausted under scale-out:** Use PgBouncer, with per-replica pool limits.
- **Schema behind the code during rollout:** Use expand-then-contract scripts; readiness fails if tables are missing.
- **Blob written but metadata commit failed:** The blob is orphaned; the retention worker deletes blobs that have no row.
- **Metadata row exists but the blob is missing:** Knowledge fails the step with `research.document_missing`.
- **Outbox or inbox growth:** Use monthly partitions, with retention dropping old ones.
- **Read replica lag:** Retrieval might miss just-indexed passages, so Jobs triggers synthesis only after `DocumentIndexed`. Read from the primary if lag exceeds a threshold.

## Scaling and operations

- **Scale-in kills a busy pod:** Drain gracefully; anything unfinished is redelivered.
- **Rolling deploy with mixed versions:** Change contracts additively only, and consumers ignore unknown fields.
- **LLM quota shared across replicas:** Set the per-replica limit to quota ÷ max replicas, or use a Redis limiter.
- **Burst of jobs:** Queues absorb it, KEDA scales out, and the gateway rate-limits the edge.
- **Clock skew between pods:** Compare deadlines and leases using database time (`now()`), not pod time.
- **Configuration reload with an invalid value:** The validator rejects it, and the last good value stays.
- **Secrets rotated:** Options reload, and HTTP clients pick up the new key without a restart.

## Security and tenancy

- **Missing or invalid JWT (gateway misconfigured):** Jobs still validates it → `401`.
- **Tenant A reads tenant B's job:** Every query filters on tenant explicitly → `404`, not `403`, so the job's existence isn't revealed.
- **PII in questions or documents:** Logs redact it, and retention deletes it.
- **Error responses leaking internals:** `UnhandledExceptionMiddleware` returns only a code and a `traceId`.
