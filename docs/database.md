# Databases

There is one Postgres server (locally, the `pgvector/pgvector:pg17` image) with **one database per owning
service**. No service reads another service's database.

| Database | Aspire connection | Owner | Scripts |
| --- | --- | --- | --- |
| `research_jobs` | `jobs` | Jobs | `0001_outbox_inbox.sql`, `0002_jobs.sql` |
| `research_fetching` | `fetching` | Fetching | `0001_outbox_inbox.sql`, `0003_fetching.sql` |
| `research_knowledge` | `knowledge` | Knowledge | `0001_outbox_inbox.sql`, `0004_knowledge.sql` |

Planning, Search and Synthesis have no database.

## Schema scripts, not migrations

Scripts live next to the adapter that uses them, in `src/**/Research.*.Postgres/db/*.sql`, and are numbered globally.
`tools/Research.DbScripts` applies them in name order to the database each belongs to, records each one in
`schema_scripts` (name and SHA-256), and refuses a script that changed after it was applied. Each script runs in its
own transaction with its record, and a session advisory lock serializes concurrent runners. Scripts are idempotent
(`IF NOT EXISTS`). To change the schema, add a new script.

The AppHost runs the tool as `db-scripts`. Jobs, Fetching and Knowledge wait for it to complete, and each one's
readiness check (`/health/ready`) verifies that its tables exist.

## Tables

### Every outbox owner (`0001_outbox_inbox.sql`)

| Table | Purpose | Key points |
| --- | --- | --- |
| `outbox_messages` | Messages to publish | Partial index on unpublished rows; claimed with `FOR UPDATE SKIP LOCKED`; `pg_notify('research_outbox')` on insert |
| `inbox_messages` | Processed message ids | PK `(message_id, consumer)` |

### `research_jobs`

| Table | Purpose | Key points |
| --- | --- | --- |
| `research_jobs` | One row per job | `status`, `version` (compare-and-set), `plan text[]`, `warnings text[]`, tokens, `synthesis_cutoff_at`, `deadline_at`; unique partial index on `idempotency_key`; indexes for the deadline and cutoff workers |
| `job_steps` | One row per plan/search/fetch/synthesize step | Unique `(job_id, step_key)`, which deduplicates URLs per job; stores the request type and payload for re-emission; `lease_until` index for recovery |
| `research_reports` | The final report | PK `job_id`; `citations jsonb` |

### `research_fetching`

| Table | Purpose | Key points |
| --- | --- | --- |
| `fetched_documents` | Pages fetched per job | Unique `(job_id, url_hash)`; `blob_key` (`pages/{sha256}.txt`), `content_hash` |

### `research_knowledge`

| Table | Purpose | Key points |
| --- | --- | --- |
| `documents` | Indexed text, once per content | Unique `(content_hash, embedding_model)` |
| `passages` | Chunks and embeddings | `embedding vector(384)`; HNSW index with `vector_cosine_ops`; unique `(document_id, ordinal)` |
| `job_documents` | Which job uses which document, under which URL/title | PK `(job_id, document_id)` |

Passage search filters by the job (through `job_documents`) and by the embedding model, and orders by cosine distance
(`<=>`). Embeddings are sent as pgvector text literals and cast with `::vector`.

## Concurrency rules

- A handler that changes a job locks the job row (`FOR UPDATE`) **first**, then touches steps, which keeps the lock
  order the same everywhere. Workers lock jobs one at a time with `SKIP LOCKED`.
- Job saves are compare-and-set updates on `version` and the allowed source states. A stale copy changes nothing.
- Clocks (cutoff, deadline, leases) are computed in .NET with `TimeProvider`, not with database `now()`, so tests can
  control time.

## Retention

There is no retention: rows and blobs are kept. Because blobs are content-addressed, the same text is stored once.
