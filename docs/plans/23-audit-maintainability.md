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
| High | 1 |
| Medium | 4 |
| Low | 4 |
| Info | 1 |

Day-to-day maintainability rests on strong foundations: central package management with a single TFM pin, a six-file startup decomposition that makes Program.cs readable in one screen, rigorously consistent code-behind separation for all non-trivial pages, an `.editorconfig`, and an unusually high standard of *why*-comments that preserve hard-won knowledge (circuit/cookie constraints, EF provider quirks, DI-lifetime rationale). The dominant tax is structural: the loopback self-API design plus hand-written consumers plus hand-maintained localization Designer files means a single new field ripples through seven-to-nine artifacts across four projects — each step simple, but the sum makes small changes slow and easy to leave half-done. Secondary frictions are hand-curated artifacts that must be remembered (Designer.cs, OpenAPI schema list) and operational knowledge that lives only in code comments where a deployer will never see it.

## 3. Findings

### MAINT-1: One new field ripples through 7–9 artifacts across four projects  [High] [Effort: L]
- **Evidence:** The concrete chain for any user-visible entity field: (1) entity in `src/ResetYourFuture.Domain/Domain/Entities/` or `Identity/ApplicationUser.cs`; (2) EF configuration in `src/ResetYourFuture.Infrastructure/Data/Configurations/`; (3) a migration in `Data/Migrations/`; (4) DTO(s) in `src/ResetYourFuture.Application/DTOs/` — positional records, so every construction site updates too; (5) service mapping in `Application/ApiServices/` (e.g. `CourseService.cs:70-98` hand-maps entity→anonymous→DTO); (6) controller in `Web/Controllers/`; (7) consumer interface + implementation in `Web/Consumers/`; (8) page markup + code-behind in `Web/Pages/`; (9) `Shared/Resources/*.resx` **and** `*.el.resx` **and** the hand-edited `*.Designer.cs` for any new label. History confirms the pattern: commit `a694267` ("Add table columns and localization for new fields") is exactly this ripple.
- **Impact:** This is the codebase's single largest ongoing cost. Every step is individually trivial, which is precisely why steps get skipped — the silent-`default` consumer behavior (`Web/Consumers/ApiClientBase.cs`) means a missed DTO field or consumer update often surfaces as a blank value rather than an error. It also discourages small improvements: the activation energy for "add one column" is a four-project tour.
- **Recommendation:** Accept the layer count (it is the ARCH-1 tradeoff, owned by report 21) but shrink the per-layer cost: (a) adopt positional-record spread or mapping helpers for entity→DTO projection; (b) fix the localization step, which is the worst offender (MAINT-2); (c) consider generating consumers from the OpenAPI document (MAINT-3); (d) keep a short "adding a field" checklist in the repo so half-done ripples are caught in review rather than at runtime.

### MAINT-2: 18 localization Designer.cs files are hand-edited generated code  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Shared/ResetYourFuture.Shared.csproj` wires `PublicResXFileCodeGenerator` + `LastGenOutput` for 18 resource families (AdminRes … CertificateRes, ErrorMessagesRes, SuccessMessagesRes) — but that generator only runs inside Visual Studio's designer; `dotnet build` never regenerates, so by working convention every new resx key requires hand-editing the corresponding `*.Designer.cs` (e.g. `Resources/AdminRes.Designer.cs`, 1,710 lines, is maintained by hand). The Shared project contains **no** hand-written code at all — 18 Designer files and 36 resx files are its entire C# surface.
- **Impact:** Hand-editing machine-generated files is the canonical hand-maintained-artifact trap: a typo'd property/key pair compiles fine and returns the wrong resource (or the neutral culture) at runtime; any accidental "run custom tool" in VS silently rewrites hand-added members; every localized string costs three file edits (`.resx`, `.el.resx`, `.Designer.cs`).
- **Recommendation:** Automate the generation: either run `dotnet msbuild /t:... `-driven resx codegen (e.g. the `Meziantou.Framework.ResxSourceGenerator` or the built-in `EmbeddedResourceUseDependentUponConvention` + source generator) so Designer files disappear from the repo, or switch consumption to `IStringLocalizer<T>` (already available — `AddLocalization()` is registered in `ServiceRegistrationExtensions.cs:121`) and delete the Designer layer entirely. Either path removes one artifact from the MAINT-1 ripple.

### MAINT-3: 18 hand-written consumer classes + 18 interfaces mirror the controllers line-for-line  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Consumers/` — 37 files (18 implementations, 18 interfaces, `ApiClientBase`), each method a hand-built URL string + helper call (e.g. `CourseConsumer.cs:11-21` hand-assembles `api/courses?page=…&pageSize=…&lang=…`); the same routes exist as attribute strings on the controllers (`Controllers/CoursesController.cs:15,28,49,60,73,83`). The app already generates a complete OpenAPI document (`Web/OpenApi/OpenApiExtensions.cs`, mapped in `Program.cs:27`).
- **Impact:** Every endpoint exists in three hand-synchronized places (controller route, consumer URL, interface signature). A route typo in a consumer compiles clean and — via ApiClientBase's silent `default` — renders as an empty page rather than a 404 anyone would notice. This is pure mechanical labor the OpenAPI document could do.
- **Recommendation:** Generate the client layer from the OpenAPI document (NSwag or Kiota, emitting into `Consumers/Generated/`), keeping the existing hand-written interfaces as the stable seam if desired — or, if generation is unwanted, at minimum centralize route templates as constants shared by controller attributes and consumers so the compiler enforces agreement.

### MAINT-4: Deployment-critical knowledge exists only as code comments  [Medium] [Effort: S]
- **Evidence:** The three facts a deployer must know are each buried in a source comment: (1) `SelfBaseUrl`/loopback-TLS requirement — `Startup/ServiceRegistrationExtensions.cs:204-210`; (2) DataProtection key-ring is filesystem+DPAPI, breaking sign-in on multi-instance/ephemeral hosts — `ServiceRegistrationExtensions.cs:171-186`; (3) email transport fails fast outside Development without `Email:Smtp:Host` — `ServiceRegistrationExtensions.cs:36-53`. None appear in the repo root README or any docs/ file (docs/ contains only this plan suite).
- **Impact:** The comments are excellent — for someone already reading that file. Anyone deploying (or grading) from the README will discover each landmine at runtime, and the first one manifests as *every page silently empty* — the worst possible debugging entry point. Knowledge placement, not knowledge existence, is the gap.
- **Recommendation:** Add a short `docs/DEPLOYMENT.md` (or README section) with the three items above plus required secrets (`Jwt:Key`, `AdminUser:Password`, connection string), each linking back to the source comment. DOC (44) owns broader documentation; this finding is only about hoisting these three operational facts.

### MAINT-5: First-run experience fails sequentially, one missing secret at a time  [Medium] [Effort: S]
- **Evidence:** Required config is validated at scattered points in startup order: `Jwt:Key` throws in `Startup/AuthenticationSetupExtensions.cs:48-50`; `AdminUser:Password` throws later in `Startup/DatabaseSeedingExtensions.cs:68-70`; `SeedData:StudentPassword` throws only when `SeedData:Enabled=true` (`DatabaseSeedingExtensions.cs:108-110`); email transport throws in non-Development (`ServiceRegistrationExtensions.cs:49-53`). A `.env.template` exists at repo root (good) and `EnvFileLoader` auto-loads `.env`, but a new contributor fixes one exception, restarts, and meets the next.
- **Impact:** Onboarding is fix-restart-fix-restart; each iteration includes LocalDB prewarm + migration. On a solo certificate project this costs the author future-self time and costs any grader/reviewer their first impression.
- **Recommendation:** One startup validation pass that collects *all* missing required keys and throws a single exception listing them (a 20-line helper called first in Program.cs). CFG (39) owns configuration architecture generally; this is the minimal maintainability fix.

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
| MAINT-1 | High | L | Shrink the per-field ripple: mapping helpers, fix localization step, "adding a field" checklist |
| MAINT-4 | Medium | S | Hoist the three deployment landmines from code comments into a deployment doc |
| MAINT-5 | Medium | S | Single startup pass that reports all missing required config at once |
| MAINT-2 | Medium | M | Automate resx→code generation (source generator or IStringLocalizer); stop hand-editing Designers |
| MAINT-3 | Medium | M | Generate consumers from the OpenAPI doc, or share route constants between controllers and consumers |
| MAINT-6 | Low | S | Make the OpenAPI realtime-schema registry complete (add Call DTOs) and self-describing |
| MAINT-7 | Low | S | Flatten `Domain/Domain/*` folder stutter |
| MAINT-8 | Low | S | Rename/repurpose the Shared project; move dev seed JSON out of the runtime graph |
| MAINT-9 | Low | S | Log which .env was loaded; bound the ancestor walk |
| MAINT-10 | Info | S | Pin DI lifetimes with a test (see TEST 24) |

## 5. Related Findings Elsewhere

- **ARCH (21)** owns the root cause of MAINT-1: the loopback self-API design (ARCH-1) mandates the consumer layer, and the global InteractiveServer mode (ARCH-7) mandates the auth-completion machinery whose knowledge burden shows up here.
- **CQ (22)** owns the micro-duplications that compound the ripple (bilingual ternary ×30, twin token-mint methods, DTO file organization).
- **TEST (24)** owns the fitness/DI tests recommended in MAINT-10 and ARCH-9, and the test-infrastructure duplication in CustomWebAppFactory.
- **DOC (44)** owns README/overall documentation quality; MAINT-4 only claims the three operational facts that must move out of comments.
- **CFG (39)** owns configuration validation architecture and secrets handling (MAINT-5/9 propose only the minimal maintainability slice).
- **BUILD (40)** owns CI/workflow design, including any `dotnet format` or generation-verification steps added as a result of MAINT-2/3.
- **DEP (43)** owns the NuGet/`Microsoft.OpenApi` auto-pin fragility that constrained this audit's methodology (restore can rewrite tracked project files).
