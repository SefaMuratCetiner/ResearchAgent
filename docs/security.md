# Security: OWASP Top 10 for agentic applications

This page maps the research agent to the OWASP Top 10 for Agentic Applications (ASI01–ASI10). The LLM Top 10 (2025)
IDs are given where they overlap. It lists what the code does, and what it leaves to other layers.

The threat that matters most here is **indirect prompt injection**: the agent reads pages written by strangers and
writes a report a person will trust. Every control below assumes that any page, search result, title or model answer
may be hostile.

## How untrusted text moves

```text
user question ──► Jobs ──► Planning (LLM) ──► Search (LLM + web search) ──► Fetching (web) ──► Knowledge ──► Synthesis (LLM) ──► report
   cleaned          capped      fenced in,          fenced in,                hidden text        stored as      fenced in,
                                checked out         titles cleaned            dropped, signals   cleaned text   sanitized out
```

The shared helpers live in `Research.SharedKernel.Security`:

| Helper | What it does |
| --- | --- |
| `UntrustedText.Clean` | Removes characters a person cannot see but a model reads: Unicode tag characters ("ASCII smuggling"), variation selectors, bidi overrides, zero-width and control characters |
| `UntrustedText.SingleLine` | Clean, one line, capped. Used for titles, queries and sub-questions, so they cannot open a new prompt section |
| `UntrustedText.IsWebUrl` | Absolute http(s) URL, at most 2048 characters |
| `PromptFence` | Wraps untrusted text in `<untrusted-{random nonce} kind=…>` elements. The text cannot close the element, because it cannot know the nonce |
| `PromptInjectionSignals` | Heuristics: "ignore previous instructions", chat role markers, text addressed to an AI, Markdown images that carry query data |

## The ten risks

| Risk | What the code does | Left to other layers |
| --- | --- | --- |
| **ASI01 Agent goal hijack** (LLM01) | The question, sub-questions, queries and excerpts go into prompts only inside a `PromptFence`. Every system prompt has a Security section that says fenced text is data, not instructions. Hidden page text is dropped before indexing (see ASI06). | No filter can stop every injection. The output controls under ASI05 and ASI09 limit what a successful injection can do. |
| **ASI02 Tool misuse** | The agent has two tools, and neither can change anything. Web search is capped at `AnthropicWebSearch:MaxUses` per sub-question and filtered by domain lists. Fetching only issues GET requests, behind the SSRF guard, robots.txt, size, time and redirect limits, and `Fetching:BlockedDomains` (checked on the requested and the final URL). Jobs starts fetch steps only for valid http(s) URLs, and at most `MaxSources` per job. | |
| **ASI03 Identity and privilege abuse** | Each service has its own database. The model holds no credentials: the API key is attached by the SDK, never put in a prompt, and never logged. | Callers are authenticated and authorized by the gateway, which is outside this repository. |
| **ASI04 Agentic supply chain** (LLM03) | `nuget.config` allows one source (nuget.org) and maps every package to it, which blocks dependency confusion. Package versions are pinned centrally, restores are locked, and NuGet audit runs on every restore. Dependabot proposes updates. Model IDs are pinned in configuration. | GitHub Actions are pinned by tag, not by commit SHA. Pin them to SHAs once the repository is on GitHub. |
| **ASI05 Unexpected code execution** (LLM05) | The agent runs no code and has no shell or interpreter tool. Before a report is stored, `ReportSanitizer` removes HTML tags and comments, images, links, bare URLs and script schemes, so rendering the Markdown cannot run script or load a URL. | Clients should still render the Markdown with raw HTML turned off. |
| **ASI06 Memory and context poisoning** (LLM04, LLM08) | The extractor drops elements hidden by attribute or inline style, plus invisible Unicode. Knowledge search is always scoped to one job's documents. Cached plans and search results are parsed and cleaned again when read. | Text hidden by an external stylesheet is not detected. |
| **ASI07 Insecure inter-agent communication** | Consumers treat other services' messages as input. Jobs cleans and caps sub-questions, accepts only valid fetch URLs, and caps the report length and the number of citations. A reply counts only for a step that is running, through the reply gate and inbox. | Broker authentication, TLS and message signing belong to the deployment; this repository runs locally only (D22). |
| **ASI08 Cascading failures** (LLM10) | Each job has a deadline, a token budget, a cap on sub-questions and sources, and a cap on attempts per step. Retries back off through delay queues and end in a DLQ. LLM calls are capped per replica. `Jobs:MaxActiveJobs` refuses new jobs with `503 jobs.capacity_reached` when the system is full. | A per-caller rate limit belongs to the gateway. |
| **ASI09 Human–agent trust exploitation** (LLM09) | The report may cite only sources it was given: unknown `[n]` markers are removed, and a report that cites nothing fails with `synthesis.uncited_report`. The prompt forbids telling the reader to act on a source's behalf, and links planted by a page are removed. The API returns the model name and the citations with every report. | Clients should show that the report is AI-generated. |
| **ASI10 Rogue agents** | Every prompt, response, page text and report is logged under a content category (credentials never are). Injection signals are logged (event 5003) and counted (`research.fetching.injection_signals`). Output that broke its rules is logged (event 7002). Any job can be cancelled through the API. Setting `Fetching:RejectSuspectedInjection` stops flagged pages from being indexed. | Alerts on those signals belong to the monitoring stack. |

## Operating notes

- **Signals versus blocking.** Injection signals are heuristics. A page *about* prompt injection matches them. That is
  why `RejectSuspectedInjection` is off by default. Turn it on when false negatives cost more than lost sources.
- **What a sanitized report means.** When event 7002 fires, the model wrote something its rules forbid: an image, a
  link, HTML, or a citation that does not exist. Usually a source tried to steer it. The job's content logs show the
  page text and the raw response.
- **Testing.** The controls are covered in `tests/Research.UnitTests/Security/AgentSecurityTests.cs` and
  `Synthesis/CitationBuilderTests.cs`.
