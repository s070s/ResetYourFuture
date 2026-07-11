# Plan: Local LLM Agent — Zero-Friction Ollama Bootstrap + Tool-Calling Upgrade

| | |
|---|---|
| Status | Draft |
| Created | 2026-07-11 |
| Depends on | none (12-plan-five-features' `recommend_courses` synergy is optional) |
| Related audits | Observability (38), Configuration (39), Operational Readiness (42) |

## 1. Context & Goals

- The assistant is **already Ollama-backed RAG** (streaming, grounded, bilingual) — but it is `Enabled=false` by default, requires manual `ollama pull`, and is a single grounded Q&A turn, not an agent.
- Goal A — **zero-friction on a fresh PC**: clone → install Ollama → run the app → the assistant works. No manual model pulls, no config edits, no restart once Ollama appears.
- Goal B — **agent upgrade**: the assistant can call server-side tools over the signed-in user's own data (enrollments, progress, results, subscription) and search/recommend courses, while keeping RAG grounding for site content.
- Resource budget: chat model **`qwen3:1.7b`** (~1.4 GB, multilingual incl. Greek, native tool-calling) + existing **`bge-m3`** embeddings (~1.2 GB) ≈ 2.6 GB total, CPU-friendly.

## 2. Current State

| Piece | File |
|---|---|
| Chat orchestration, streaming, system prompt, status ping | `src/ResetYourFuture.Application/ApiServices/AssistantService.cs` (`StreamChatAsync` 27-92, `GetStatusAsync` 94-114, `BuildSystemPrompt` 129-160) |
| RAG retrieval (cosine over in-memory cache, MinScore 0.4) | `src/ResetYourFuture.Application/ApiServices/AssistantRetrievalService.cs` |
| Indexing pipeline (chunk → embed → hash-diff) | `src/ResetYourFuture.Application/ApiServices/AssistantIndexingService.cs`, entity `AssistantContentChunk` |
| Options POCO | `src/ResetYourFuture.Application/Common/AssistantOptions.cs` (defaults `gemma3:4b`, `Enabled=false`) |
| Single DI block (the only place OllamaSharp is referenced) | `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:85-113` — when disabled, `DisabledAssistantService` is registered **at startup** and only a restart can enable the real pipeline |
| Background indexer (5s after startup, then 6h; tolerates Ollama down) | `src/ResetYourFuture.Web/Services/AssistantIndexer.cs` |
| SSE endpoint + per-user rate limit ("assistant" policy) | `src/ResetYourFuture.Web/Controllers/AssistantController.cs`, `ServiceRegistrationExtensions.cs:155-165` |
| Widget (client-held transcript, 20-message cap, 100 ms render throttle) | `src/ResetYourFuture.Web/Shared/Components/Assistant/AssistantWidget.razor(.cs)` |
| Config | `src/ResetYourFuture.Web/appsettings.json:63-72` |

Packages already present: `OllamaSharp` 5.4.25, `Microsoft.Extensions.AI` 10.7.0 (`Directory.Packages.props`) — `UseFunctionInvocation()` and `AIFunctionFactory` need no new dependencies.

## 3. Design Decisions

| # | Decision | Alternatives rejected | Rationale |
|---|----------|-----------------------|-----------|
| 1 | Chat model default → `qwen3:1.7b`; keep `bge-m3` embeddings | `gemma3:1b` (weak Greek, no reliable tool-calling); keep `gemma3:4b` (2.4× disk/RAM) | Smallest model that is both multilingual (Greek) and tool-calling capable; embeddings must stay multilingual and existing chunks stay valid |
| 2 | `Assistant:Enabled` defaults to `true`; a new **runtime availability state** replaces the DI-time service swap | Keep DI-time swap | With DI-time swap, "enabled but Ollama missing" needs a restart after installing Ollama — kills the fresh-PC story. Runtime state degrades gracefully and recovers automatically |
| 3 | App **auto-pulls missing models** via OllamaSharp on startup (background, with progress logs) | Docker compose sidecar; setup script only | No Docker in this repo by design; auto-pull needs zero user knowledge. `winget install Ollama.Ollama` remains the only manual step |
| 4 | RAG stays **pre-injected grounding** (retrieval before the model runs); tools are only for **personal/live data** | Expose retrieval as a `search_content` tool | A 1.7B model choosing when to search is unreliable; pre-injection preserves today's quality and keeps tool count small |
| 5 | Tools resolve the user **server-side from the authenticated context** — no `userId` parameter is visible to the model; anonymous users get **zero tools** | Passing userId as a tool arg | Prompt injection must never let the model read another user's data |
| 6 | No server-side chat persistence (transcript stays client-held, existing 20-message cap) | New ChatHistory tables | Out of scope; existing DTO cap + trimming is sufficient for v1 |
| 7 | Tests keep the assistant **disabled in CI**; live-model coverage is an env-gated smoke test | Ollama in GitHub Actions | CI runner cost/flakiness; unit tests fake `IChatClient` |

## 4. Work Items

### Workstream A — zero-friction bootstrap

### WI-A1: Config + options defaults
- **Files:** `src/ResetYourFuture.Web/appsettings.json:63-72`, `src/ResetYourFuture.Application/Common/AssistantOptions.cs`
- **Change:** `Enabled: true`, `ChatModel: "qwen3:1.7b"` in both file defaults and POCO initializers. Add `AutoPullModels: true` (new key) so restricted environments can opt out. Keep test hosts disabled (`CustomWebAppFactory` already sets it).
- **Acceptance criteria:** fresh clone with no `.env` boots with assistant enabled; tests still run with it disabled.

### WI-A2: Runtime availability state replaces DI-time swap
- **Files:** `src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:96-113`, new `src/ResetYourFuture.Application/Common/AssistantRuntimeState.cs`, `DisabledAssistantService.cs` (retire or keep only for `Enabled=false`), `AssistantController.cs`
- **Change:** always register the real pipeline when `Enabled=true`; add singleton `AssistantRuntimeState` with `Status ∈ {Disabled, OllamaUnreachable, DownloadingModels, Ready}` + progress text. `AssistantService.GetStatusAsync` and the chat endpoint consult it (chat returns a friendly "warming up" event instead of erroring while not Ready).
- **Acceptance criteria:** installing/starting Ollama **after** the app is running transitions state → Ready without an app restart (state re-probed on status calls / bootstrap retries).

### WI-A3: `OllamaBootstrapService` (BackgroundService)
- **Files:** new `src/ResetYourFuture.Web/Services/OllamaBootstrapService.cs`; register in the assistant DI block before `AssistantIndexer`; `AssistantIndexer.cs` waits for `Ready` instead of a blind 5 s delay
- **Change:** loop with backoff (5 s → 60 s cap): ping `BaseUrl` → if unreachable set `OllamaUnreachable`, retry; list local models (OllamaSharp `ListLocalModelsAsync`) → `PullModelAsync` any missing chat/embedding model, streaming progress to logs and `AssistantRuntimeState` (e.g. "downloading qwen3:1.7b — 43%"); then set `Ready`. Never throws out of `ExecuteAsync`; honors `AutoPullModels=false` by skipping pulls (state `OllamaUnreachable` names the missing model).
- **Acceptance criteria:** on a machine with Ollama installed but no models, first `dotnet run` ends with both models pulled, index built, widget answering — zero manual pulls; logs show progress lines.

### WI-A4: Widget surfaces bootstrap state
- **Files:** `AssistantWidget.razor(.cs)`, `AssistantStatusDto` (extend with state + progress), `AssistantRes.resx` + `AssistantRes.el.resx` (+ hand-edited `Designer.cs` per repo convention)
- **Change:** widget stays visible in all states; instead of hiding when unavailable it shows localized state text: "Assistant is downloading its model (43%)…", "Assistant unavailable — is Ollama running?". Status polling already exists via `GET /api/assistant/status`.
- **Acceptance criteria:** EN + EL state messages render during a cold bootstrap; widget becomes interactive the moment state hits Ready.

### WI-A5: README + optional pre-pull script
- **Files:** `README.md` (AI Assistant section, lines ~449-479), new `scripts/setup-ollama.ps1` (optional convenience: install check + pre-pull)
- **Change:** setup reduces to: 1) `winget install Ollama.Ollama` 2) `dotnet run` — everything else is automatic. Update model names, sizes (~2.6 GB total), hardware note, troubleshooting rows, and the config table (new `AutoPullModels` key).
- **Acceptance criteria:** following only the README on a clean Windows machine yields a working assistant.

### WI-A6: Env-gated live smoke test
- **Files:** new test class in `tests/ResetYourFuture.Web.Tests/` (e.g. `AssistantLiveSmokeTests.cs`)
- **Change:** `[SkippableFact]`-style guard on env var `RYF_OLLAMA_LIVE=1`: boots the app factory with assistant enabled against local Ollama, sends one chat turn, asserts a non-empty token stream and `done` event. Skipped in CI.
- **Acceptance criteria:** green locally with Ollama running; auto-skipped in GitHub Actions.

### Workstream B — tool-calling agent

### WI-B1: Function-invocation pipeline
- **Files:** `ServiceRegistrationExtensions.cs` (chat client registration), `AssistantService.cs`
- **Change:** wrap the chat client: `new OllamaApiClient(...).AsIChatClient()` → `new ChatClientBuilder(...).UseFunctionInvocation().Build()`. `StreamChatAsync` keeps its manual-enumerator error handling; pass tools via `ChatOptions.Tools` (only when authenticated). SSE event shape unchanged (`token`/`sources`/`done`/`error`); optionally emit a new `tool` event (`"Checking your enrollments…"`) for UX.
- **Acceptance criteria:** with tools present, plain questions still stream tokens exactly as today; tool-triggering questions produce a final grounded answer.

### WI-B2: `AssistantTools` — the tool surface
- **Files:** new `src/ResetYourFuture.Application/ApiServices/AssistantTools.cs` (+ `IAssistantTools`), unit tests
- **Change:** scoped class holding the **authenticated userId** (injected server-side per request — Decision 5), exposing `AIFunctionFactory.Create(...)`-wrapped methods, each returning compact JSON-able records (size-capped, ≤ ~1 KB):
  - `get_my_enrollments()` — course titles + enrolled date (localized title per request language)
  - `get_my_progress()` — per enrolled course: completed/total lessons
  - `get_my_assessment_results()` — recent submissions: title + date (no raw answers)
  - `get_subscription_status()` — tier, renewal/expiry
  - `search_courses(query, maxResults≤5)` — title/description match over **published** courses; reuses existing course query patterns
  - `recommend_courses()` — published courses in the user's categories not yet enrolled (upgrades to ratings once feature 2 of plan 12 exists)
  All queries `AsNoTracking`, published-only, tenant = current user.
- **Acceptance criteria:** each tool unit-tested against in-memory `IApplicationDbContext`; no tool accepts or leaks a user identifier; anonymous request ⇒ `ChatOptions.Tools` empty.

### WI-B3: System prompt + guardrails
- **Files:** `AssistantService.cs` (`BuildSystemPrompt`), `AssistantOptions.cs`
- **Change:** prompt gains a short tool-usage section ("use tools for questions about the user's own courses, progress, results, or subscription; answer site questions from CONTEXT"). Guardrails: `MaximumIterationsPerRequest = 3` on the function-invocation pipeline (new option `MaxToolRounds`, default 3), keep `RequestsPerMinute` + `MaxOutputTokens`, tool results truncated at source (WI-B2), existing injection guard sentence extended to cover tool outputs.
- **Acceptance criteria:** a question requiring two tool calls completes; a pathological loop stops at 3 rounds and still yields a text answer; Greek UI produces Greek answers with tools involved.

### WI-B4: Pipeline + guardrail tests
- **Files:** `tests/ResetYourFuture.Application.Tests/AssistantServiceTests.cs` (extend), new fake `IChatClient` in `tests/ResetYourFuture.TestSupport/`
- **Change:** fake `IChatClient` that scripts function-call responses: assert tools are advertised only when authenticated, invocation results round-trip into the conversation, round cap enforced, SSE event sequence preserved, errors mid-tool-call surface as `error` events.
- **Acceptance criteria:** `dotnet test` green with assistant disabled (no Ollama needed).

## 5. Implementation Order & Dependencies

1. **A1 → A2 → A3** (bootstrap core; A2 before A3 since the bootstrap service writes the state).
2. **A4** (widget states) — after A2/A3 provide real states.
3. **B1 → B2 (one tool first — `get_my_enrollments` — validated live against qwen3:1.7b) → remaining tools → B3 → B4.**
4. **A6** smoke test once B-stream stabilizes; **A5** README last (documents final behavior).
- A-stream and B-stream are independent after A1; two people (or sessions) can run them in parallel.

## 6. Verification

- `dotnet build` / `dotnet test` green (assistant disabled in CI — unchanged).
- **Fresh-PC drill (the core acceptance):** clean Windows VM → clone → `winget install Ollama.Ollama` → `dotnet run --project src/ResetYourFuture.Web` → watch logs pull both models → widget answers "What courses do you offer?" (RAG) and, signed in as seeded student, "How far am I in my courses?" (tools) — in **both EN and EL**.
- `RYF_OLLAMA_LIVE=1 dotnet test --filter AssistantLiveSmoke` green locally.
- Regression: assistant disabled (`Assistant__Enabled=false`) still yields the graceful disabled widget; rate-limit 429 path unchanged.

## 7. Out of Scope

- Server-side conversation persistence (Decision 6), voice, image input.
- Docker/compose packaging (repo has none by design).
- Admin-facing tools (user management etc.) — student/self-service data only in v1.
- Swapping the embedding model (existing chunks remain valid).
