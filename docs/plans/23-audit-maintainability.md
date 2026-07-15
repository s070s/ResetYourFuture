# Audit: Maintainability

| | |
|---|---|
| Finding prefix | MAINT |
| Created | 2026-07-11 |
| Scope | Cost of change (how many places one change touches), hand-maintained artifacts, coupling that taxes evolution, onboarding/first-run friction, knowledge location (comments vs docs), project/folder hygiene |
| Delegated | The structural decisions *causing* change amplification → ARCH (21); micro duplication/naming → CQ (22); test infrastructure design → TEST (24); README/docs quality → DOC (44); config validation & secrets handling → CFG (39); CI pipeline design → BUILD (40) |

## 1. Methodology

Examined: the full change-path for a representative feature slice (entity → `Infrastructure/Data/Configurations` → migration → DTO → Application service → controller → consumer interface+impl → page code-behind → resx pair → Designer.cs), cross-checked against recent history (`git log` read-only; commits `a694267`, `7f314f4`, `1306eab` show multi-layer touch patterns); `src/ResetYourFuture.Shared/ResetYourFuture.Shared.csproj` (18 resx families, PublicResXFileCodeGenerator wiring) and the Shared project's file inventory (zero non-Designer C# files); all 18 consumer/interface pairs in `src/ResetYourFuture.Web/Consumers/`; `src/ResetYourFuture.Web/OpenApi/OpenApiExtensions.cs` (hand-curated schema registrations); `Startup/EnvFileLoader.cs`, `Startup/DatabaseSeedingExtensions.cs`, `Startup/ServiceRegistrationExtensions.cs` (operational comments); `.env.template` presence; `tests/Directory.Build.props`; folder layout of all five src projects; `.github/workflows/tests.yml`.

NOT examined: build/restore (deliberately skipped — NuGet restore has previously rewritten `ResetYourFuture.Web.csproj`/`Directory.Packages.props` with an incompatible `Microsoft.OpenApi` pin, and this audit must not modify tracked files); README content quality (owned by DOC 44); IDE-specific tooling.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 4 |
| Info | 1 |

> **Accepted / deferred (will not implement in this pass):** MAINT-2 (18 hand-edited localization `Designer.cs` files) and MAINT-3 (18 hand-written API consumers mirroring the controllers). Both are Effort-M structural refactors that reduce mechanical labor rather than fix a correctness bug, and both carry a large regression surface. MAINT-2 was actually attempted (2026-07-14) via the SDK's built-in strongly-typed-resource MSBuild task and **reverted** because it silently produced wrong results (internal class + doubled manifest name → `null` lookups at runtime) — the exact silent-failure class the finding warns about; a verified NuGet source generator or an `IStringLocalizer<T>` rewrite remains open but needs every resource family checked in both cultures. MAINT-3 would mean generating the client layer from the OpenAPI document (NSwag/Kiota) plus rewiring DI, verified across every consumer-driven page. Both are consciously deferred as documented large-effort debt (retained in full below with their assessment notes) rather than undertaken unattended. See the sibling accepted payment finding in [27-audit-business-logic.md](27-audit-business-logic.md) (BIZ-3).

Day-to-day maintainability rests on strong foundations: central package management with a single TFM pin, a six-file startup decomposition that makes Program.cs readable in one screen, rigorously consistent code-behind separation for all non-trivial pages, an `.editorconfig` (now build-enforced — GOV-2), and an unusually high standard of *why*-comments that preserve hard-won knowledge (circuit/cookie constraints, EF provider quirks, DI-lifetime rationale). The dominant tax is structural: the loopback self-API design plus hand-written consumers plus hand-maintained localization Designer files means a single new field ripples through seven-to-nine artifacts across four projects — each step simple, but the sum makes small changes slow and easy to leave half-done (the shared mapping layer and the `docs/ADDING-A-FIELD.md` checklist now blunt this — MAINT-1, fixed). The onboarding and deployment-knowledge frictions are fixed (MAINT-4/5); what's left is the two consciously-accepted large-effort structural refactors above (MAINT-2/3) plus hand-curated artifacts and project hygiene.

## 3. Findings

### MAINT-2: 18 localization Designer.cs files are hand-edited generated code  [Medium — Accepted/deferred] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Shared/ResetYourFuture.Shared.csproj` wires `PublicResXFileCodeGenerator` + `LastGenOutput` for 18 resource families (AdminRes … CertificateRes, ErrorMessagesRes, SuccessMessagesRes) — but that generator only runs inside Visual Studio's designer; `dotnet build` never regenerates, so by working convention every new resx key requires hand-editing the corresponding `*.Designer.cs` (e.g. `Resources/AdminRes.Designer.cs`, 1,710 lines, is maintained by hand). The Shared project contains **no** hand-written code at all — 18 Designer files and 36 resx files are its entire C# surface.
- **Impact:** Hand-editing machine-generated files is the canonical hand-maintained-artifact trap: a typo'd property/key pair compiles fine and returns the wrong resource (or the neutral culture) at runtime; any accidental "run custom tool" in VS silently rewrites hand-added members; every localized string costs three file edits (`.resx`, `.el.resx`, `.Designer.cs`).
- **Attempted (2026-07-14):** Tried the no-new-dependency half of the recommendation — the SDK's built-in `StronglyTypedFileName`/`GenerateResource` MSBuild task (`Generator>MSBuild:Compile</Generator>` + `StronglyTypedFileName`/`StronglyTypedClassName`/`StronglyTypedNamespace`/`StronglyTypedManifestPrefix` item metadata) as a `dotnet build`-only replacement for the VS-only custom tool, proof-of-concepted on `AdminRes` alone. It compiled, but produced a **wrong, silently-broken result**: the generated class came out `internal` (call sites across the Web project need `public`) and the manifest resource name doubled to `"...Resources.AdminRes.AdminRes"` (wrong stream name — every `GetString` call would return `null`/neutral-culture text at runtime, not fail the build). Reverted. This is exactly the class of failure MAINT-2 itself warns about (compiles fine, silently wrong at runtime), and getting both settings right across 18 families without a human eyeballing the VS designer flow was judged too risky to push unattended. The `Meziantou.Framework.ResxSourceGenerator` NuGet path (untried) or the `IStringLocalizer<T>` rewrite remain open — both large, both need real verification of every family in both cultures before landing.
- **Recommendation:** Unchanged: automate the generation (verify a working NuGet-based source generator on one family first, this time checking accessibility and manifest name explicitly) or switch consumption to `IStringLocalizer<T>` and delete the Designer layer entirely.

### MAINT-3: 18 hand-written consumer classes + 18 interfaces mirror the controllers line-for-line  [Medium — Accepted/deferred] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Consumers/` — 37 files (18 implementations, 18 interfaces, `ApiClientBase`), each method a hand-built URL string + helper call (e.g. `CourseConsumer.cs:11-21` hand-assembles `api/courses?page=…&pageSize=…&lang=…`); the same routes exist as attribute strings on the controllers (`Controllers/CoursesController.cs:15,28,49,60,73,83`). The app already generates a complete OpenAPI document (`Web/OpenApi/OpenApiExtensions.cs`, mapped in `Program.cs:27`). There are in fact 31 controllers total (only 18 have a hand-written consumer), so even the "minimum" fix — sharing base-route-prefix constants between every `[Route(...)]` attribute and its consumer — touches up to ~49 files, and would only catch prefix drift, not the more realistic per-method sub-route/query-string drift the finding actually describes.
- **Impact:** Every endpoint exists in three hand-synchronized places (controller route, consumer URL, interface signature). A route typo in a consumer compiles clean and — via ApiClientBase's silent `default` — renders as an empty page rather than a 404 anyone would notice. This is pure mechanical labor the OpenAPI document could do.
- **Recommendation:** Unchanged: generate the client layer from the OpenAPI document (NSwag or Kiota, emitting into `Consumers/Generated/`), keeping the existing hand-written interfaces as the stable seam if desired. The "minimum" constants-only alternative was assessed and set aside — its highest-value slice (base prefixes) doesn't cover the actual per-method drift risk, and the full per-route version is comparable effort to just generating the client.

### MAINT-6: OpenAPI realtime-schema list is a hand-curated registry  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/OpenApi/OpenApiExtensions.cs:119-136` — SignalR payload DTOs are registered by hand (`("ChatMessageDto", typeof(ChatMessageDto)), ("ChatNotificationDto", typeof(ChatNotificationDto))`) because hub-only types never reach the document via REST endpoints. The CallHub's payload shapes (`Application/DTOs/Call/CallDtos.cs`) are *not* in the list.
- **Impact:** The documented realtime surface silently drifts from the actual one — it already has (chat is documented, calls are not). Anyone extending hub payloads must know this list exists.
- **Recommendation:** Either add the Call DTOs and a comment making the registry's contract explicit ("every hub payload type must be listed here"), or derive the list by reflection over a marker attribute/namespace so it cannot drift.

### MAINT-7: `Domain/Domain/Entities` path stutter  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Domain/Domain/Entities/` and `src/ResetYourFuture.Domain/Domain/Enums/` — a redundant `Domain/` level inside the Domain project (namespaces are correct: `ResetYourFuture.Domain.Entities`); siblings `Identity/` and `Extensions/` sit at project root, so the tree is inconsistent with itself.
- **Impact:** Cosmetic-plus: every file path in errors/reviews carries the stutter, and new-file placement guesses wrong half the time. Left over from the project split.
- **Recommendation:** Move `Domain/Entities` → `Entities`, `Domain/Enums` → `Enums` (namespaces unchanged, so it is a pure file move; csproj globbing needs no edits).

### MAINT-8: The "Shared" project is a resources-and-seed-data project wearing a shared-kernel name  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Shared/` contains exactly: 18 resx families under `Resources/` (+ hand-edited Designers, see MAINT-2) and JSON seed fixtures under `JSON/{Assessments,Courses,Students}` consumed only by Development seeders (`Startup/DatabaseSeedingExtensions.cs:96-111`). Zero hand-written C#.
- **Impact:** The name invites dumping genuinely shared code there (its referenced-by-everyone position makes it the path of least resistance), which would create a fifth layer with no rules. Dev-only seed JSON also ships with every environment's dependency graph.
- **Recommendation:** Rename to `ResetYourFuture.Resources` (or fold resources into Application) and relocate seed JSON under `tests/` or a `seed/` content folder. Low urgency; do it before the project accumulates a second purpose.

### MAINT-9: EnvFileLoader silently walks up five directories and hand-parses .env  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Startup/EnvFileLoader.cs:35-49` — searches `cwd` and up to four ancestors for `.env`; first hit wins, no logging of *which* file loaded; the parser (lines 21-32) handles only `KEY=VALUE` (no quotes, escapes, or `export`).
- **Impact:** Running the app from an unexpected working directory (or having a stray `.env` in a parent like `Desktop/`) silently changes configuration — a classic "works on my machine" generator. The no-logging choice is deliberate for secret-safety but makes the ambiguity undiscoverable.
- **Recommendation:** Log the resolved `.env` *path* (not contents) at startup; consider constraining the walk to "directory containing a `.sln`". CFG (39) owns the broader secrets story.

### MAINT-10: Correctness-bearing DI lifetime invariants are enforced only by registration comments  [Info]
- **Evidence:** `Startup/ServiceRegistrationExtensions.cs:73-77` ("Must be scoped … so CallOverlayHost and chat components share one instance/hub/state per circuit"), `:76-77` (PresenceService piggybacks on that scoping), `:81-83` (ApiTokenProvider circuit-scoping rationale). ARCH-8 (report 21) covers the ChatService-vs-CallService divergence itself.
- **Impact:** These comments are load-bearing: changing `AddScoped` to `AddTransient` during a refactor compiles, runs, and subtly breaks call/presence state sharing. Nothing in the build or test suite would catch it.
- **Recommendation:** A five-line DI test asserting the registered lifetimes of `ICallService`, `PresenceService`, `ApiTokenProvider` (TEST 24 owns the test itself; recorded here because the maintenance hazard is comment-only enforcement).

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| MAINT-2 | Medium — Accepted | M | Automate resx→code generation (verified NuGet source generator or IStringLocalizer); MSBuild-only attempt tried and reverted — consciously deferred |
| MAINT-3 | Medium — Accepted | M | Generate consumers from the OpenAPI doc; constants-only alternative assessed and set aside — consciously deferred |
| MAINT-6 | Low | S | Make the OpenAPI realtime-schema registry complete (add Call DTOs) and self-describing |
| MAINT-7 | Low | S | Flatten `Domain/Domain/*` folder stutter |
| MAINT-8 | Low | S | Rename/repurpose the Shared project; move dev seed JSON out of the runtime graph |
| MAINT-9 | Low | S | Log which .env was loaded; bound the ancestor walk |
| MAINT-10 | Info | S | Pin DI lifetimes with a test (see TEST 24) |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the root cause of MAINT-1: the loopback self-API design (ARCH-1) mandates the consumer layer, and the global InteractiveServer mode (ARCH-7) mandates the auth-completion machinery whose knowledge burden shows up here.
- **CQ (22)** owns the micro-duplications that compound the ripple (bilingual ternary ×30, twin token-mint methods, DTO file organization) — fixed.
- **TEST (24)** owns the fitness/DI tests recommended in MAINT-10 and ARCH-9, and the test-infrastructure duplication in CustomWebAppFactory.
- **DOC (44)** owns README/overall documentation quality; MAINT-4 (fixed) only claimed the three operational facts that had to move out of comments — see `docs/DEPLOYMENT.md`.
- **CFG (39)** owns configuration validation architecture and secrets handling; MAINT-5 (fixed) was the minimal maintainability slice — see `Startup/StartupConfigValidation.cs`.
- **BUILD (40)** owns CI/workflow design, including any `dotnet format` or generation-verification steps a future MAINT-2/3 attempt adds.
- **DEP (43)** owns the NuGet/`Microsoft.OpenApi` auto-pin fragility that constrained this audit's methodology (restore can rewrite tracked project files).
