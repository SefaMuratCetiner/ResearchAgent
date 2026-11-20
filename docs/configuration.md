# Configuration

Each section is bound once in the Host's `CompositionRoot/<Service>Settings.cs` through `SettingsBinder`, and
validated at startup by an `IValidateOptions<T>`. Invalid configuration stops the process. Environment variables use
`__` for `:`, for example `Fetching__MaxBytes`.

## Reloading

Every option reloads without a restart when its configuration source changes (for example `appsettings.json`
edited on disk). Scoped code (handlers, endpoints) reads `IOptionsSnapshot<T>`, so one request or one message
sees one consistent set of values; singletons and background services read `IOptionsMonitor<T>.CurrentValue`
where they use the value. When a change takes effect:

| Setting | Applies |
| --- | --- |
| Handler and endpoint settings (`Jobs`, `Planning`, `Search`, `Fetching`, `Knowledge`, `Synthesis` limits) | The next request or message |
| Concurrency limits (`Anthropic:MaxConcurrentRequests`, `OpenAI:MaxConcurrentRequests`, `Search:MaxConcurrentSearches`, `Fetching` per-domain limit) | The next call; calls already holding a slot finish under the old limit |
| `Search` cache time-to-live | The next cached search |
| API keys (`Anthropic:ApiKey`, `OpenAI:ApiKey`, `AnthropicWebSearch:ApiKey`) | The next call: the SDK client is rebuilt with the new key (key rotation) |
| `Fetching` HTTP handler settings, `Synthesis` Knowledge base address | When the HTTP client factory builds its next handler or client (handlers are recycled every few minutes) |
| `Jobs` worker intervals | After the current tick |
| `Messaging` outbox batch size, poll interval and retry settings | The next outbox pass |
| `Messaging:Prefetch`, `Messaging:RetryDelaysSeconds` | Each listener finishes its in-flight messages and subscribes again with the new values |
| `Messaging:MaxAttempts`, `Messaging:MessageTimeoutSeconds`, `RabbitMq:ShutdownDrainSeconds` | The next message, or the next drain |
| **`Messaging:ServiceName`, `RabbitMq:Exchange`** | **Restart only.** They name the queues and exchange where messages live; changing them live would strand messages in the old queue. A change logs a warning (event 8105) and the startup value stays. |
| **`Llm:Provider`** | **Restart only.** It decides which provider is registered at composition time. |

A reloaded value goes through the same validators as at startup. If it is invalid, reading the options throws
`OptionsValidationException` (and the affected calls fail) until the configuration is fixed; the process does not
fall back to the previous value. Check a change in a non-production environment first.

## Secrets

| Secret | Where it comes from | Read as |
| --- | --- | --- |
| Anthropic API key | AppHost parameter `anthropic-api-key` (user secrets `Parameters:anthropic-api-key`) | `Anthropic__ApiKey` (Planning, Synthesis), `AnthropicWebSearch__ApiKey` (Search) |
| OpenAI API key (optional) | AppHost parameter `openai-api-key` (user secrets `Parameters:openai-api-key`); passed on only while it is set | `OpenAI__ApiKey` (Planning, Synthesis) |
| Connection strings | Injected by Aspire (`ConnectionStrings__jobs`, `__bus`, `__cache`, `__blobs`, ...) | Aspire client integrations |

Secrets, connection strings and auth headers are **never logged**. Content (questions, prompts, model output, search
results, page text, reports, API bodies) **is** logged; see [observability.md](observability.md).

## Aspire connections

| Name | Resource | Used by |
| --- | --- | --- |
| `jobs`, `fetching`, `knowledge` | Postgres databases `research_*` | the owning service, `db-scripts` |
| `bus` | RabbitMQ | all six services |
| `cache` | Valkey | Planning, Search |
| `blobs` | Azure Blob Storage (Azurite) | Fetching (write), Knowledge (read) |
| `elasticsearch` | Elasticsearch (logs only; optional, the sink is off without it) | all six services |
| `knowledge` (service discovery) | the Knowledge service | Synthesis (`https+http://knowledge`) |

## Shared sections

### `Messaging` (every service)

| Key | Default | Meaning |
| --- | --- | --- |
| `ServiceName` | — (required) | Queue prefix and consumer name (`jobs`, `planning`, ...) |
| `MaxAttempts` | 4 | Deliveries per message before `research.step_exhausted` |
| `RetryDelaysSeconds` | [10, 60, 300] | Delay queue per retry; the last repeats |
| `Prefetch` | 8 | QoS and handler concurrency per replica |
| `OutboxBatchSize` | 100 | Rows per outbox claim |
| `OutboxPollIntervalMs` | 60000 | Poll interval when no NOTIFY arrives |
| `OutboxRetryDelayMs` | 1000 | First retry delay of a failed publish, and first pause of the outbox loop after an error; both double |
| `OutboxMaxAttempts` | 20 | Failed publishes of one outbox message before it is parked (about an hour) |
| `OutboxMaxRetryDelaySeconds` | 300 | Upper bound of the doubling retry delay |
| `MessageTimeoutSeconds` | 900 | Handler timeout |

### `RabbitMq`

| Key | Default |
| --- | --- |
| `Exchange` | `research.events` |
| `ShutdownDrainSeconds` | 30 |

### `ContentLogging`

| Key | Default | Meaning |
| --- | --- | --- |
| `MaxChars` | 64000 | Longer content is truncated in log events (with a marker) |

### `Llm` (Planning, Synthesis)

| Key | Default | Meaning |
| --- | --- | --- |
| `Provider` | `Anthropic` | `Anthropic` or `OpenAI`. Only the selected provider's section is validated, so the other one's key is not needed. The module's `Model` must name a model of this provider; `OpenAI` with a `claude-*` model stops startup. |

### `Anthropic` (Planning, Synthesis)

Required only when `Llm:Provider` is `Anthropic`.

| Key | Default |
| --- | --- |
| `ApiKey` | — (required, secret) |
| `MaxConcurrentRequests` | 4 per replica (concurrency limiter) |
| `RefusalFallbackModel` | `claude-sonnet-5` (one retry on a refusal) |

### `OpenAI` (Planning, Synthesis)

Required only when `Llm:Provider` is `OpenAI`. Calls go to the Chat Completions API; a JSON schema becomes a strict
structured output, and OpenAI caches the repeated system prompt by itself.

| Key | Default |
| --- | --- |
| `ApiKey` | — (required, secret) |
| `MaxConcurrentRequests` | 4 per replica in Planning, 2 in Synthesis (concurrency limiter) |
| `RefusalFallbackModel` | empty: no retry on a refusal |
| `UseReasoningEffort` | `true`: the module's `Effort` is sent as `reasoning_effort` (`Max` as `high`). Set `false` for models without reasoning, which reject it. With reasoning, `MaxTokens` also covers the reasoning tokens. |

## Per service

### `Jobs`

| Key | Default | Meaning |
| --- | --- | --- |
| `MaxDuration` | 00:05:00 | Deadline after creation (`Expired`) |
| `SynthesisReserve` | 00:01:30 | Synthesis cutoff = deadline − reserve |
| `MinSources` / `MaxSources` | 3 / 12 | Indexed sources needed / fetches started |
| `MaxSubQuestions` | 5 | Plan size |
| `ResultsPerSearch` | 6 | Hits requested per search |
| `TokenBudget` | 400000 | Gathering stops when spent |
| `StepLease` | 00:03:00 | Step answer expected within |
| `MaxStepAttempts` | 3 | Re-emissions of a stuck step |
| `SynthesisTopK` | 12 | Passages retrieved for the report |
| `QuestionMinLength` / `QuestionMaxLength` | 10 / 2000 | |
| `RecoveryInterval` / `DeadlineInterval` | 00:00:30 / 00:00:05 | Worker periods |
| `WorkerBatchSize` | 50 | Jobs per worker run |
| `EventsPollInterval` | 00:00:01 | SSE database poll |
| `MaxActiveJobs` | 50 | Active jobs across replicas; more are refused with 503 `jobs.capacity_reached`. 0 = no cap |
| `MaxSubQuestionLength` | 200 | Sub-questions from a plan are cut to this |
| `MaxReportChars` / `MaxCitations` | 200000 / 100 | Bounds on a stored report |

### `Planning`

| Key | Default |
| --- | --- |
| `Model` | `claude-sonnet-5-5` |
| `Effort` | `Medium` |
| `MaxTokens` | 4000 |
| `CacheTimeToLive` | 24:00:00 |

### `Search` / `AnthropicWebSearch`

| Key | Default |
| --- | --- |
| `Search:MaxResults` | 10 |
| `Search:CacheTimeToLive` | 06:00:00 |
| `Search:MaxConcurrentSearches` | 4 per replica |
| `AnthropicWebSearch:ApiKey` | — (required, secret) |
| `AnthropicWebSearch:Model` | `claude-sonnet-5-5` |
| `AnthropicWebSearch:MaxUses` | 3 |
| `AnthropicWebSearch:MaxTokens` | 4000 |
| `AnthropicWebSearch:AllowedDomains` / `BlockedDomains` | [] |

### `Fetching`

| Key | Default |
| --- | --- |
| `MaxBytes` | 5242880 |
| `MaxRedirects` | 5 |
| `Timeout` | 00:00:20 |
| `MinContentChars` / `MaxContentChars` | 500 / 200000 |
| `UserAgent` | `ResearchAgent/1.0 (+https://research.local/bot)` |
| `PerDomainConcurrency` | 2 per replica |
| `RespectRobotsTxt` | true |
| `RobotsCacheDuration` | 01:00:00 |
| `AllowedContentTypes` | `text/html`, `application/xhtml+xml`, `text/plain` |
| `BlockedDomains` | none (a domain also blocks its subdomains) |
| `RejectSuspectedInjection` | false (signals are always logged and counted) |

### `Knowledge`

| Key | Default |
| --- | --- |
| `ChunkChars` / `ChunkOverlapChars` | 1200 / 200 |
| `MaxPassagesPerDocument` | 200 |
| `EmbeddingBatchSize` | 64 |
| `MaxTopK` / `MaxQueries` / `MaxQueryLength` | 50 / 20 / 2000 |

### `Synthesis`

| Key | Default |
| --- | --- |
| `Model` | `claude-sonnet-5-5` |
| `Effort` | `Medium` |
| `MaxTokens` | 16000 |
| `MaxContextChars` | 120000 (passages beyond this are dropped, lowest score first) |
| `KnowledgeBaseAddress` | `https+http://knowledge` |

## Ports (when a Host runs alone)

| Jobs | Planning | Search | Fetching | Knowledge | Synthesis |
| --- | --- | --- | --- | --- | --- |
| 5101 | 5102 | 5103 | 5104 | 5105 | 5106 |

Under the AppHost, Aspire assigns ports and replicas.
