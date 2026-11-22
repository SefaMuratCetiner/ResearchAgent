# Error codes

Each code is owned by exactly one service and starts with that service's prefix. The `research.` prefix is shared
platform infrastructure. Each library exposes its codes as an `ErrorSet` (`<Service>Errors.All`), and at startup
`ErrorCatalog` refuses duplicate codes and codes with a foreign prefix.

Errors appear in two places:

- **HTTP:** an RFC 9457 problem document (`application/problem+json`). It carries the error in `code`, adds `traceId`,
  and includes `errors` for validation failures. The `type` is the code's URI.
- **Messages:** `StepFailed.ErrorCode`. For a failed job, it is also `JobFailed.ErrorCode` and the job's `errorCode`.

Kinds map to HTTP status codes: Invalid → 400, NotFound → 404, Conflict → 409, Forbidden → 403, Rule → 422,
Unavailable → 503,
Unexpected → 500.

## Platform (`research.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `research.unexpected` | Unexpected | Unhandled exception in an HTTP request |
| `research.validation_failed` | Invalid | Request validation failed (`errors` lists fields) |
| `research.step_exhausted` | Unexpected | A message handler threw on every attempt (`Messaging:MaxAttempts`), or Jobs re-emitted a stuck step `Jobs:MaxStepAttempts` times |
| `research.poison_message` | Unexpected | A message could not be deserialized; it goes straight to the DLQ |

## Jobs (`jobs.`)

| Code | Kind | Where |
| --- | --- | --- |
| `jobs.job_not_found` | NotFound | Any `/research/{id}` route |
| `jobs.job_already_completed` | Conflict | Cancel of a completed job |
| `jobs.job_not_cancellable` | Conflict | Cancel of a failed/expired/cancelled job |
| `jobs.report_not_ready` | Conflict | `/report` while the job is running |
| `jobs.report_not_available` | NotFound | `/report` of a job that ended without one |
| `jobs.idempotency_conflict` | Conflict | Same `Idempotency-Key`, different question |
| `jobs.question_invalid` | Invalid | Question empty or outside `QuestionMinLength..QuestionMaxLength` |
| `jobs.capacity_reached` | Unavailable | `POST /research` while `MaxActiveJobs` jobs are active (503; retry later) |
| `jobs.plan_empty` | Rule | Job failure: the plan had no usable sub-question |
| `jobs.no_sources_found` | Rule | Job failure: gathering ended with 0 indexed sources |
| `jobs.insufficient_sources` | Rule | Job failure: fewer than `MinSources` indexed |
| `jobs.deadline_exceeded` | Rule | The job's state is `Expired` (deadline passed) |
| `jobs.budget_exhausted` | Rule | **Warning**: gathering stopped because the token budget ran out |
| `jobs.cutoff_reached` | Rule | **Warning**: gathering stopped at the synthesis cutoff |
| `jobs.steps_failed` | Rule | **Warning**: some search/fetch steps failed |

The report-level warning `synthesis.output_truncated` is also listed in a job's `warnings`.

## Planning (`planning.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `planning.invalid_plan` | Rule | The structured output was missing or unparsable |
| `planning.llm_refused` | Rule | The model refused, also on the fallback model |

## Search (`search.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `search.invalid_query` | Invalid | The provider rejected the query (`invalid_input`, `query_too_long`) |
| `search.quota_exhausted` | Rule | The provider's usage limit was reached (`max_uses_exceeded`) |
| `search.provider_refused` | Rule | The provider refused for another permanent reason |

An empty result is not an error. Search replies with `ResultsFound` and no hits.

## Fetching (`fetching.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `fetching.invalid_url` | Invalid | Not an absolute http(s) URL |
| `fetching.fetch_disallowed` | Forbidden | robots.txt disallows the path |
| `fetching.blocked_address` | Forbidden | The host resolves only to private/reserved addresses (SSRF guard) |
| `fetching.http_error` | Rule | The site answered 4xx (except 429) |
| `fetching.redirect_limit` | Rule | More than `MaxRedirects` redirects, or a redirect the client will not follow |
| `fetching.unsupported_content` | Rule | Content type not in `AllowedContentTypes` |
| `fetching.too_large` | Rule | Body larger than `MaxBytes` |
| `fetching.content_too_short` | Rule | Extracted text shorter than `MinContentChars` |
| `fetching.timeout` | Rule | No complete answer within `Timeout` |
| `fetching.blocked_domain` | Forbidden | The requested or final (after redirects) host is in `BlockedDomains` |
| `fetching.suspected_injection` | Rule | The text shows prompt-injection signals and `RejectSuspectedInjection` is on |

Fetching does not use a code for transient failures: 5xx, 429 and network errors throw, so the message is retried.
If every retry fails, the step ends with `research.step_exhausted`.

## Knowledge (`knowledge.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `knowledge.document_missing` | Rule | The claim-check blob is not in the document store |
| `knowledge.no_passages` | Rule | Chunking produced nothing |
| `knowledge.embedding_dimension_mismatch` | Unexpected | The embedding model returned vectors of the wrong size |
| `knowledge.invalid_search` | Invalid | Passage search request invalid (handler-level check) |

## Synthesis (`synthesis.`)

| Code | Kind | Raised when |
| --- | --- | --- |
| `synthesis.no_passages` | Rule | Retrieval returned nothing for the job |
| `synthesis.llm_refused` | Rule | The model refused, also on the fallback model |
| `synthesis.empty_report` | Rule | The model returned no text |
| `synthesis.output_truncated` | Rule | Warning only: the report hit `MaxTokens` (the report is kept, marked `truncated`) |
| `synthesis.uncited_report` | Rule | The report, after sanitizing, cites none of the sources (it is not stored) |
