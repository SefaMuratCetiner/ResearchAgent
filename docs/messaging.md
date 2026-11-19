# Messaging

## Topology

- **Broker:** RabbitMQ, which Aspire exposes through the connection `bus`.
- **Exchange:** one topic exchange, `research.events`. The routing key is the message type (`planning.plan-requested`).
- **Work queue:** each consumer declares `{service}.{messageType}`, for example `jobs.planning.plan-ready`. It is a
  **quorum** queue bound to its routing key.
- **Delay queues:** each work queue has one per retry delay, named `{queue}.retry.{seconds}s`. A message waits there
  until its per-queue TTL expires, then dead-letters back to the work queue through the default exchange.
- **Dead-letter queue:** each work queue has a DLQ, `{queue}.dlq`. Nothing consumes it automatically, so inspect and
  replay its messages by hand.
- **Publishing:** publisher confirms are on for every publish, and messages are persistent.

## Contracts

All contracts live in `Research.Contracts`. Every message starts with `JobId` and `StepKey`, and implements
`IIntegrationMessage`.

| Type | Producer → consumer | Notes |
| --- | --- | --- |
| `jobs.job-created`, `jobs.job-completed`, `jobs.job-failed`, `jobs.job-cancelled`, `jobs.job-expired` | Jobs → (anyone) | Lifecycle events; no consumer today |
| `planning.plan-requested` / `planning.plan-ready` | Jobs ↔ Planning | |
| `search.search-requested` / `search.results-found` | Jobs ↔ Search | `ResultsFound` may be empty |
| `fetching.fetch-requested` | Jobs → Fetching | |
| `fetching.document-fetched` | Fetching → Knowledge | Claim check: carries `BlobKey`, never the text |
| `knowledge.document-indexed` | Knowledge → Jobs | Ends the fetch step (`StepKey` is the fetch step's) |
| `synthesis.synthesis-requested` / `synthesis.report-ready` | Jobs ↔ Synthesis | |
| `research.step-failed` | any worker → Jobs | Business failure or exhausted retries |

Headers: `research-job-id`, `research-step-key`, `research-message-type`, `research-occurred-at`,
`research-attempt`, `traceparent`, `tracestate`. The AMQP `message-id` is a UUID v7.

## Consumer pipeline (`MessageDispatcher<T>`)

| Outcome | Condition | Action |
| --- | --- | --- |
| Ack | Handler succeeded | `basic.ack` |
| Retry | Handler threw, attempts left | Publish a copy with `research-attempt + 1` to the next delay queue (confirmed), then ack |
| DeadLetter | Attempts exhausted | Publish `StepFailed(research.step_exhausted)`, then move the message to the DLQ |
| DeadLetter | Payload cannot be deserialized | Publish `StepFailed(research.poison_message)` if the headers name a job and step, then move the message to the DLQ |
| Requeue | Shutdown cancelled the handler | `basic.nack(requeue: true)` |

The handler runs under `Messaging:MessageTimeoutSeconds`. Each delivery starts a **new trace linked to the producer's
span**, so every hop is its own trace and the links connect them. Listeners retry their setup with backoff, and
`ShutdownDrainSeconds` lets in-flight messages finish before shutdown.

## Delivery guarantees

| Service | Incoming | Outgoing |
| --- | --- | --- |
| Jobs, Fetching, Knowledge | Inbox (`inbox_messages`, keyed by message id + consumer) in the same transaction as the state change | Transactional outbox (`outbox_messages`) published by `OutboxBackgroundService` (`LISTEN research_outbox` + poll) |
| Planning, Search, Synthesis | No database | Direct confirmed publish, then ack |

Delivery is at least once everywhere, and duplicates are absorbed where state changes:

- Jobs records each reply in its inbox and finishes the step with a compare-and-set update. A duplicate reply finds
  the step already finished and is discarded.
- Fetching and Knowledge check the inbox before doing work, and record it again in the reply transaction.
- Blob keys are content-addressed, so writing the same blob twice is harmless.
- Documents are unique per content hash and embedding model, and job links are upserted.

Outbox order is **best effort**. Jobs does not depend on reply order: each reply checks the job's current state.

## Lost messages

Jobs stores each step's request in `job_steps` with a lease (`Jobs:StepLease`). `StuckJobRecoveryWorker`
re-emits any step whose lease has expired, up to `Jobs:MaxStepAttempts` times. After that it fails the step with
`research.step_exhausted`. This covers a lost message, a crashed consumer, and a broker that lost its queues.
