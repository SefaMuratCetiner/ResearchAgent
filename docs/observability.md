# Observability

All three signals, traces, metrics and logs, go out over OTLP. Locally they go to the Aspire dashboard, which injects
`OTEL_EXPORTER_OTLP_ENDPOINT`.

## Logs

- Serilog, as a provider of `Microsoft.Extensions.Logging` (`LoggingFeature`).
- Output is compact JSON on stdout, plus the OpenTelemetry provider (`writeToProviders: true`).
- Messages come from source-generated `[LoggerMessage]` definitions with fixed event ids.
- Log scopes carry the correlation id (HTTP), the job id and the step key (messages).

### Content logging

The user's rule is to log content but never credentials.

| Category | What | Where |
| --- | --- | --- |
| `Research.Content.Http` | Request and response bodies (never headers); skips `text/event-stream` and `/health` | `HttpContentLoggingMiddleware` (Jobs, Knowledge) |
| `Research.Content.Llm` | System prompt, user prompt, model output; the raw web search response | `AnthropicLlmClient`, `AnthropicWebSearchProvider` |
| `Research.Content.Search` | Query and each hit | `RunSearchHandler` |
| `Research.Content.Documents` | Extracted page text | `FetchPageHandler` |
| `Research.Content.Reports` | The report | `WriteReportHandler` |

Content is cut at `ContentLogging:MaxChars`. To turn a category off, set its level in `Serilog:MinimumLevel:Override`.
API keys, connection strings and `Authorization` / `x-api-key` headers are never written.

### Event id ranges

| Range | Owner | Ids in use |
| --- | --- | --- |
| 1000–1099 | Research.Messaging | 1000–1005 dispatcher, 1010 outbox loop |
| 2000–2099 | Jobs | 2000–2002 lifecycle, 2010–2013 reply gate, 2020–2021 create (2021 capacity), 2030–2031 recovery, 2090–2091 workers |
| 3000–3099 | Planning | 3000–3001 |
| 4000–4199 | Search | 4000–4002 handler, 4100 Anthropic web search |
| 5000–5099 | Fetching | 5000 fetched, 5001 rejected, 5002 page text, 5003 prompt-injection signals |
| 6000–6099 | Knowledge | 6000 prepared, 6010–6011 indexed/failed, 6020 search |
| 7000–7099 | Synthesis | 7000–7001 report, 7002 output sanitized, 7003 uncited report |
| 8000–8099 | Anthropic provider | 8000–8002 |
| 8100–8199 | RabbitMQ provider | 8100–8103 listener |
| 8200–8299 | Postgres provider | 8200–8201 outbox store |
| 8400–8499 | Valkey provider | 8400 |
| 9100–9299 | HTTP | 9100–9101 unhandled exception, 9200 correlation, 9210–9211 content logging |

## Traces

| Source | Spans |
| --- | --- |
| ASP.NET Core, HttpClient | Instrumented by `TelemetryFeature`; health requests are filtered out |
| Npgsql, RabbitMQ, StackExchange.Redis, Azure Blob | Instrumented by the Aspire client integrations |
| `Research.Messaging` | One consume span per delivery, the **root of a new trace** with a link to the producer |
| `Research.Jobs` | Worker runs |
| `Research.Llm` | One span per model call |

Span tags: `research.job.id`, `research.step.key`, `research.error.code`. Each step also stores the producer's trace
id in `job_steps.trace_id`.

## Metrics

| Meter | Instruments |
| --- | --- |
| `Research.Messaging` | `research.messaging.handler.duration`, `research.messaging.dead_lettered`, `research.outbox.published`, `research.outbox.failures` |
| `Research.Jobs` | `research.jobs.created`, `research.jobs.finished`, `research.job.duration`, `research.job.sources`, `research.steps.finished`, `research.jobs.late_results`, `research.steps.reemitted` |
| `Research.Llm` | `research.llm.duration`, `research.llm.tokens`, `research.llm.throttled`, `research.llm.refusals` |
| `Research.Search` | `research.search.duration`, `research.search.hits` |
| `Research.Fetching` | `research.fetching.pages` (by outcome), `research.fetching.page.size`, `research.fetching.duration`, `research.fetching.injection_signals` (by signal) |
| `Research.Knowledge` | `research.knowledge.documents` (indexed/reused/error), `research.knowledge.passages`, `research.knowledge.search.duration` |

## Health

| Endpoint | Checks |
| --- | --- |
| `/health/live` | The process answers |
| `/health/ready` | Postgres, RabbitMQ (Aspire), plus the schema check that the service's tables exist |

The Valkey and blob checks report **Degraded**, not Unhealthy. Both stores are optional or retried per message, so a
replica stays in rotation while they are down.
