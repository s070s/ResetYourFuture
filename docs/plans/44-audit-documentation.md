# Audit: Documentation

| | |
|---|---|
| Finding prefix | DOC |
| Created | 2026-07-11 |
| Scope | README.md accuracy and coverage, the `docs/` folder, XML doc comments on public APIs, code-comment quality and staleness, OpenAPI/Swagger as living API documentation, `.env.template` as setup documentation, LICENSE and CHANGELOG presence. |
| Delegated | The *functional* gap behind the stale email plan (no reset/confirm pages, UI calling dev-only endpoints) → GAP (20) / UX (33). OpenAPI document correctness/exposure at runtime → API (31). Privacy-policy/terms pages (user-facing legal docs) → COMP (29). Versioning/tags/release discipline that a CHANGELOG would hang off → GOV (45). Third-party license posture (QuestPDF) → DEP (43). |

## 1. Methodology

Read `README.md` (522 lines) in full and spot-verified its claims against code: email registration logic (`src/ResetYourFuture.Web/Startup/ServiceRegistrationExtensions.cs:36-53`), seed defaults (`src/ResetYourFuture.Infrastructure/Seeding/BulkStudentSeedingService.cs:42`, `appsettings.Development.json`), the Auth endpoint table against `Controllers/AuthController.cs` route attributes, config keys against `appsettings.json` / `.env.template`, and admin/assistant/WebRtc sections against `appsettings.json`. Read both files under `docs/superpowers/` and checked their promised artifacts against the codebase (page routes, controller endpoints). Measured XML doc coverage by counting files containing `/// <summary>` per project and reading representative controllers/DTOs. Searched for references to deleted plan documents (`AI_ASSISTANT_PLAN.md`, `VIDEO_CALL_PLAN.md`). Checked repo root for LICENSE/CHANGELOG/CONTRIBUTING (none). Used `git log` (read-only) to date README updates against feature commits.

NOT examined: rendered Swagger UI output (would require running the app); XML doc *prose* quality file-by-file beyond sampling.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 3 |
| Low | 4 |
| Info | 2 |

Documentation is a relative strength of this project. The README is a genuine operator's manual — quickstart, full endpoint tables with an explicit "Swagger UI is the authoritative source" disclaimer, config reference, production checklist, per-feature sections, and a troubleshooting table — and it has demonstrably been updated per feature (commits `c7e8b27`, `3d33110`). XML doc coverage is high (Domain 34/34 files with summaries, Application 74/83, Infrastructure 32/44) and load-bearing, feeding the OpenAPI document via `GenerateDocumentationFile` on the Web and Application projects. The findings are therefore about drift, not absence: the email section contradicts the code it describes, the sole surviving `docs/` plan documents features that were never built, deleted plan docs are still referenced from code comments, and a public GitHub repo ships with no LICENSE. Each is cheap to fix; together they erode trust in otherwise excellent docs.

## 3. Findings

### DOC-1: README email documentation contradicts the code — SmtpEmailService exists but is undocumented  [Medium] [Effort: S]
- **Evidence:** `README.md:63` (Tech Stack: "Email | `StubEmailService` (dev only) … a real provider must be registered for production"), `README.md:405` (production checklist: "`IEmailService` real implementation registered (startup throws if absent in Production)"), and the Email section (`README.md:410-424`) all present StubEmailService as the only implementation. In code, `src/ResetYourFuture.Infrastructure/ApiServices/SmtpEmailService.cs` (MailKit) exists and `ServiceRegistrationExtensions.cs:36-53` auto-registers it whenever `Email:Smtp:Host` is configured — in *any* environment — falling back to the stub only in Development, and failing fast otherwise. Neither the README Configuration table (`README.md:385-399`) nor `.env.template` mentions any `Email__Smtp__*` key, though `appsettings.json` defines the full `Email:Smtp` section (Host/Port/UseStartTls/Username/Password/From*).
- **Impact:** The single most deployment-critical subsystem is documented as missing when it is present. An operator following the README would conclude they must write an email service; a grader assessing feature completeness would under-credit it. The real switch (`Email__Smtp__Host`) is discoverable only by reading DI registration code.
- **Recommendation:** Rewrite the Email section: SMTP via MailKit when `Email__Smtp__Host` is set (Papercut/Mailhog in dev, real relay in prod), stub fallback in Development, fail-fast otherwise. Add the `Email__Smtp__*` rows to the Configuration table and commented entries to `.env.template` (see DOC-6).

### DOC-2: The only content in docs/ is a plan whose promised features were never built, with no outcome recorded  [Medium] [Effort: S]
- **Evidence:** `docs/superpowers/plans/2026-06-25-email-service-auth-flows.md` and `docs/superpowers/specs/2026-06-25-email-service-auth-flows-design.md` (spec status: "Approved") promise, among delivered items, a `/reset-password` page, a `/confirm-email` page, and a rate-limited `resend-confirmation` endpoint on `AuthController`. None exist: no `@page "/reset-password"` or `@page "/confirm-email"` anywhere in `src/`, no `resend-confirmation` route in `Controllers/AuthController.cs` (routes verified: register, confirm-email GET, login, refresh, forgot-password, reset-password, me, two dev endpoints). Meanwhile `Pages/Login.razor.cs:98`, `Pages/Register.razor.cs:127`, and `Pages/ForgotPassword.razor.cs:66` still call the `api/auth/dev/*` endpoints that are compiled out of Release builds. The plan's checkboxes were never updated to record what landed (SmtpEmailService, EmailOptions, tests) versus what did not.
- **Impact:** The repo's design-record convention elsewhere is *delete the plan when done* (commits `dff8e94`, `1730c4d`, `c5aa3de`). This one survives half-true: a reader (or a future agent executing "finish the plan") cannot tell whether the missing pages were descoped, forgotten, or superseded. The user-facing consequence — password reset has no UI in Release — is owned by GAP (20)/UX (33); this finding owns the misleading record.
- **Recommendation:** Add a short "Outcome" header to the plan (delivered: Tasks 1–2 …; not delivered: pages/resend — descoped or pending), or delete both files per the established convention once the gap is triaged.

### DOC-3: Public repository has no LICENSE file  [Medium] [Effort: S]
- **Evidence:** No `LICENSE`, `LICENSE.*`, `COPYING`, or `NOTICE` at the repo root (verified); the README (`README.md:11`) instructs cloning from the public `https://github.com/s070s/ResetYourFuture.git` and contains no licensing statement of any kind.
- **Impact:** Under default copyright, a public repo with no license grants viewers no rights to use, copy, or modify the code. For a university certificate project this creates avoidable ambiguity for exactly its audiences — graders, portfolio reviewers, and anyone the README invites to clone and run it. (Third-party license posture, incl. the conditional QuestPDF Community license, is DEP-6 in DEP 43; the vendored Bootstrap files do retain their MIT headers.)
- **Recommendation:** Add a LICENSE file (MIT is the natural fit for a portfolio project) and a one-line License section at the bottom of the README.

### DOC-4: Code comments reference plan documents that were deleted from the repo  [Low] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Controllers/AssistantController.cs:14` ("See AI_ASSISTANT_PLAN.md for the overall design") and `src/ResetYourFuture.Application/DTOs/Assistant/AssistantDtos.cs:6` ("no server-side conversation persistence — see AI_ASSISTANT_PLAN.md D7") reference a file deleted in commit `dff8e94`; `src/ResetYourFuture.Web/wwwroot/js/webrtc-interop.js:1` references `VIDEO_CALL_PLAN.md`, deleted in `1730c4d`.
- **Impact:** Dangling pointers in the most design-dense areas of the code. The `AssistantDtos.cs` case is the worst: it cites a specific decision ("D7") whose rationale now exists nowhere in the repo — the delete-plan-on-completion convention (DOC-2) silently orphaned it.
- **Recommendation:** Inline the one-sentence rationale where the decision matters (e.g. "clients replay the transcript; the server deliberately stores nothing per-conversation") and drop the file references. A repo-wide grep for `_PLAN.md` on future plan deletions prevents recurrence.

### DOC-5: README factual drift — wrong seed default, newest feature undocumented  [Low] [Effort: S]
- **Evidence:** (a) `README.md:41` claims the bulk student seeder default is 2000; the code default is 10 (`BulkStudentSeedingService.cs:42` — `GetValue<int>("SeedData:BulkStudentCount", 10)`) and `appsettings.Development.json:21` also sets 10. (b) Real-time user presence tracking (commit `b2dd9bd`, 2026-07-10 19:52 — `PresenceService.cs`, last-seen display in chat/call/admin pages) is absent from the README, whose last update (`README.md` mtime 2026-07-10 10:43, commit `c7e8b27`) predates the feature.
- **Impact:** (a) misleads capacity expectations by 200× for anyone tuning seed data; (b) the otherwise-reliable "README documents every feature" pattern has its first gap, which matters when the README doubles as the feature inventory for grading.
- **Recommendation:** Fix the number (or change the code default if 2000 was the intent) and add a short presence note under the Chat/Video Calls sections. Adopt the existing habit formally: README update in the same commit or PR as the feature.

### DOC-6: .env.template does not cover the configurable surface the README points at  [Low] [Effort: S]
- **Evidence:** `.env.template` documents 6 keys (connection string, JWT key, admin/student passwords, webhook secret, AllowedHosts). Missing: `Assistant__Enabled` — the README's AI Assistant setup (`README.md:462-463, 470`) explicitly says to set it "in `appsettings.json` / `.env`"; all `Email__Smtp__*` keys (DOC-1); `App__BaseUrl` / `SelfBaseUrl` (absolute-link generation, set only in appsettings for localhost); `SeedData__BulkStudentCount` is present but commented with `50`, a third value distinct from both the code default (10) and the README claim (2000) (DOC-5).
- **Impact:** `.env.template` is the repo's declared setup contract ("Use `.env.template` to document which keys are needed" — `README.md:43`). Every key it omits forces the code-spelunking the template exists to prevent.
- **Recommendation:** Add commented-out entries for `Assistant__Enabled`, the `Email__Smtp__*` group, and `App__BaseUrl`, keeping the existing grouped-comment style; align the `BulkStudentCount` example with the real default.

### DOC-7: No architecture overview or decision records beyond a 7-line folder tree  [Low] [Effort: M]
- **Evidence:** `README.md:69-80` shows the solution layout with one-line project descriptions — the sum total of architecture documentation. Recurring, non-obvious decisions are recorded nowhere durable: the two parallel auth paths (cookie for Blazor SSR, JWT for the API — described only inside the stale plan doc `docs/superpowers/specs/...-design.md`), the global `InteractiveServerRenderMode` on `<Routes>` (`App.razor:44`) and its consequences (no real form POSTs), the CSS layering rule (app.css vs shared-components.css vs scoped), and the assistant's deliberate single-turn-RAG/no-persistence design (its rationale pointer now dangling, DOC-4).
- **Impact:** Onboarding cost lands on the developer's own future self and on assessors: the auth-path split in particular has already caused real bugs elsewhere (see MAINT/ARCH reports) and cannot be learned from any current doc.
- **Recommendation:** One `docs/architecture.md` (or a short ADR list) capturing the 4–6 standing decisions above, a request-flow sketch for the two auth paths, and a pointer from the README. Half a day, disproportionate payoff for a certificate submission.

### DOC-8: No CHANGELOG  [Info] [Effort: S]
- **Evidence:** No `CHANGELOG*` at the root; release history exists only as 253 git commit messages.
- **Impact:** Minimal while there are no versions or releases to chronicle — the real gap is the absent versioning discipline itself, owned by GOV (45) (GOV-1). A CHANGELOG becomes worthwhile the moment tags exist.
- **Recommendation:** Defer until GOV-1's tagging lands; then a `Keep a Changelog`-style file seeded from the feature-complete commits (`git log --oneline` is already clean enough to reconstruct it).

### DOC-9: Strength — XML doc coverage is high and wired into the OpenAPI pipeline  [Info] [Effort: S]
- **Evidence:** Files containing `/// <summary>` per project: Domain 34/34, Application 74/83, Infrastructure 32/44 (Web controllers sampled: fully summarized, e.g. `AssistantController.cs`). `<GenerateDocumentationFile>true</GenerateDocumentationFile>` on `ResetYourFuture.Web.csproj:8` and `ResetYourFuture.Application.csproj:7` feeds these into the OpenAPI document, with `NoWarn 1591` documented as a deliberate "document the API surface, not every member" policy in both csproj comments. `README.md:100-123` correctly frames Swagger UI as authoritative and the endpoint tables as a static convenience.
- **Impact:** Positive observation — the API is effectively self-documenting, and comment quality sampled is explanatory (*why*, not *what*): e.g. the SSE buffering note at `AssistantController.cs:33`, the registration-fallback rationale at `ServiceRegistrationExtensions.cs:36-39`.
- **Recommendation:** None required. If Infrastructure coverage is ever raised, prioritize the seeding and background-service classes where behavior is least guessable.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| DOC-1 | Medium | S | Rewrite README Email section to match SmtpEmailService reality; document `Email__Smtp__*` |
| DOC-2 | Medium | S | Record outcome in (or delete) the stale email-flows plan/spec |
| DOC-3 | Medium | S | Add LICENSE + README license note |
| DOC-4 | Low | S | Fix three dangling references to deleted plan docs; inline the D7 rationale |
| DOC-5 | Low | S | Correct seed-count claim; document presence tracking |
| DOC-6 | Low | S | Extend .env.template (Assistant__Enabled, Email__Smtp__*, App__BaseUrl) |
| DOC-7 | Low | M | Write docs/architecture.md with the standing decisions (auth paths, render mode, CSS layers) |
| DOC-8 | Info | S | CHANGELOG — defer until GOV-1 tagging exists |
| DOC-9 | Info | S | (Strength) keep XML-doc→OpenAPI pipeline as-is |

## 5. Related Findings Elsewhere

- **GAP (20) / UX (33):** the functional side of DOC-2 — no user-facing reset/confirm pages in Release; Login/Register/ForgotPassword pages call `#if DEBUG`-only endpoints.
- **API (31):** runtime correctness and exposure of the OpenAPI document that DOC-9's pipeline produces.
- **COMP (29):** COMP-1 — missing privacy policy/terms pages, the user-facing legal documentation companion to DOC-3.
- **DEP (43):** DEP-6 — QuestPDF Community license note; DEP-2 — recording vendored-asset provenance (documentation of dependencies).
- **GOV (45):** GOV-1 — no tags/versioning (prerequisite for DOC-8); GOV-6 — absent CONTRIBUTING/health files.
- **CFG (39):** configuration-surface documentation overlaps DOC-6 from the operations side.
