# ResetYourFuture

A psychosocial career counseling platform with courses, assessments, real-time chat, video calls, subscriptions, blog, testimonials, certificate generation, and a local AI assistant.

---

## Quickstart

```bash
# 1. Clone
git clone https://github.com/s070s/ResetYourFuture.git
cd ResetYourFuture

# 2. Trust HTTPS dev certificate (once per machine)
dotnet dev-certs https --trust

# 3. Set up secrets — copy the template and fill in your own values
cp .env.template .env          # keep .env at the repo root (next to .env.template)
# Edit .env and set at minimum:
#   AdminUser__Password=YourAdminPassword123!
#   SeedData__StudentPassword=YourStudentPassword123!
#   Jwt__Key=your-dev-jwt-key-at-least-32-chars   ← must be ≥ 32 characters

# 4. Restore packages
dotnet restore

# 5. Build
dotnet build

# 6. Run
dotnet run --project src/ResetYourFuture.Web
# Visual Studio: right-click Solution → Configure Startup Projects → set ResetYourFuture.Web → F5

# 7. (Optional) Enable the local AI assistant — see the "AI Assistant" section below
```

> **Database is created and migrated automatically on first run.** If you drop the database (e.g. from SSMS), just restart the app — it will recreate and reseed it.

**Admin:** `admin@resetyourfuture.local` / *(password you set in `.env`)*

> Seed data (students, courses, assessments) runs automatically in Development when `SeedData:Enabled = true` in `appsettings.Development.json`. The bulk student seeder runs in the background after startup. Set `SeedData:BulkStudentCount` in `.env` to control the count (default: 2000).

> **Never commit `.env`** — it is already in `.gitignore`. Use `.env.template` to document which keys are needed.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 |
| Frontend / Backend | Blazor SSR + ASP.NET Core Web API |
| ORM | Entity Framework Core 10 (SQL Server) |
| Auth | ASP.NET Core Identity · Cookie (SSR) · JWT Bearer · Refresh tokens |
| Real-time | SignalR — chat (`/hubs/chat`) and video-call signaling (`/hubs/call`) |
| Video calls | Self-hosted WebRTC (P2P mesh, up to 6 participants) with SignalR signaling |
| API docs | OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Swagger UI — `/swagger` (Development only) |
| PDF | QuestPDF 2026.2.4 |
| Localization | English + Greek (`.resx`) |
| Testing | xUnit + Shouldly + NSubstitute · EF Core InMemory/SQLite · `WebApplicationFactory` |
| CI | GitHub Actions (`.github/workflows/tests.yml`) |
| Logging | Custom daily file logger |
| Email | `StubEmailService` (dev only) — logs to file; a real provider must be registered for production |
| Security | HSTS · `X-Content-Type-Options` · `X-Frame-Options` · `Referrer-Policy` · `Permissions-Policy` |
| AI Assistant | Local Ollama sidecar via `Microsoft.Extensions.AI` — `gemma3:4b` chat + `bge-m3` embeddings, no cloud API |

---

## Solution Structure

```
ResetYourFuture.sln
├── src/
│   ├── ResetYourFuture.Domain/          Entities, enums, value objects — no framework dependencies
│   ├── ResetYourFuture.Application/     Service interfaces, DTOs, application services
│   ├── ResetYourFuture.Infrastructure/  EF Core DbContext, migrations, service implementations
│   ├── ResetYourFuture.Web/             Blazor SSR + API controllers — the only deployable project
│   └── ResetYourFuture.Shared/          DTOs shared with front-end, .resx resources, JSON seed data
└── tests/                               Unit, integration, and shared test-support projects
```

---

## Quality & Tests

Run the full suite locally:

```bash
dotnet test ResetYourFuture.sln
```

The test projects mirror the application layers under `tests/`: `Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, and `Web.Tests`, plus `ResetYourFuture.TestSupport` for shared fixtures. The suite uses free/permissive test dependencies only: xUnit, Shouldly, NSubstitute, EF Core InMemory/SQLite, and `Microsoft.AspNetCore.Mvc.Testing`.

Web integration tests boot the real ASP.NET Core pipeline through `WebApplicationFactory<Program>`. The test host supplies dummy JWT/admin settings, runs in Development, swaps SQL Server for InMemory, and uses SQLite only for provider behaviors InMemory cannot execute, such as `EF.Functions.Like` or `ExecuteUpdateAsync`.

GitHub Actions runs restore, Release build, and `dotnet test` on every push and pull request, then uploads TRX results from `TestResults/*.trx`. There is no coverage gate or coverage artifact yet; the definition of done is a green `dotnet test` locally and in CI.

---

## API Documentation (Swagger / OpenAPI)

Interactive API docs are generated from the code (built-in `Microsoft.AspNetCore.OpenApi` + Swagger UI) and served **in Development only**:

| Resource | URL |
|----------|-----|
| Swagger UI | `https://localhost:7090/swagger` |
| OpenAPI document (JSON) | `https://localhost:7090/openapi/v1.json` |

**Authorize / test secured endpoints:** click **Authorize** in Swagger UI, then paste a JWT obtained from `POST /api/auth/login` — paste the token value only (the `Bearer ` prefix is added for you). The lock icon on each operation reflects whether it requires authentication.

**What's covered:**
- Every API controller, grouped by tag (e.g. *Authentication*, *Courses*, *Admin · Users & Roles*), with summaries, parameter descriptions, response codes (`200 / 201 / 204 / 400 / 401 / 403 / 404 / 409 / 500`), and request-body examples pre-filled for "Try it out".
- The four browser-navigation endpoints (`/culture/set`, `/auth/complete`, `/auth/signout`, `/sitemap.xml`) under the **Infrastructure** tag.
- The **SignalR chat hub** (`/hubs/chat`) — documented in the document description (invoke methods, server events, query-string JWT auth) with the `ChatMessageDto` / `ChatNotificationDto` payload shapes under **Schemas**. SignalR is not a REST protocol, so it appears as reference rather than callable operations.
- The **AI Assistant chat endpoint** (`POST /api/assistant/chat`) — a normal HTTP operation but with a `text/event-stream` (Server-Sent Events) response; the document description explains the `AssistantStreamEvent` frame shape (see [AI Assistant](#ai-assistant)).
- The **SignalR call hub** (`/hubs/call`) is the WebRTC signaling channel for video calls; like the chat hub it is a bidirectional WebSocket protocol, so it is not represented as callable REST operations (see [Video Calls](#video-calls)).

> **Production:** the Swagger UI and `/openapi/v1.json` endpoints are mapped only when `ASPNETCORE_ENVIRONMENT=Development`; they are **not** exposed in Production.

**Implementation:** `src/ResetYourFuture.Web/OpenApi/OpenApiExtensions.cs` (info metadata, JWT bearer security scheme, per-operation security/lock, parameter & response descriptions, request examples, hub documentation). Action/DTO summaries flow into the doc via `<GenerateDocumentationFile>` on the `ResetYourFuture.Web` and `ResetYourFuture.Application` projects.

The tables below are a quick static reference; **Swagger UI is the authoritative, always-current source.**

---

## Endpoints

### Auth — `api/auth`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `api/auth/register` | Register new user (Student role) | No |
| `GET` | `api/auth/confirm-email` | Confirm email via token link | No |
| `POST` | `api/auth/login` | Log in — returns JWT + refresh token | No |
| `POST` | `api/auth/forgot-password` | Request password-reset email | No |
| `POST` | `api/auth/reset-password` | Reset password with token | No |
| `GET` | `api/auth/me` | Current user info from JWT | Yes |
| `POST` | `api/auth/refresh` | Rotate refresh token — returns new JWT + refresh token pair | No |
| `POST` | `api/auth/dev/confirm-email` | Dev-only: confirm email without link (**compiled out in Release builds**) | Dev |
| `POST` | `api/auth/dev/reset-password` | Dev-only: reset password without email (**compiled out in Release builds**) | Dev |

### Profile — `api/profile`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/profile` | Get current user's profile | Yes |
| `PUT` | `api/profile` | Update profile | Yes |
| `POST` | `api/profile/avatar` | Upload avatar | Yes |
| `GET` | `api/profile/avatar` | Get avatar | Yes |
| `POST` | `api/profile/change-password` | Change password | Yes |

### Courses — `api/courses`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/courses` | List published courses (optional `?categoryId=` and `?search=` filters — see [Content Categories](#content-categories)) | Yes |
| `GET` | `api/courses/{courseId}` | Course detail with modules and lessons | Yes |
| `POST` | `api/courses/{courseId}/enroll` | Enroll in a course | Yes |
| `GET` | `api/courses/lessons/{lessonId}` | Lesson detail | Yes |
| `POST` | `api/courses/lessons/{lessonId}/complete` | Mark lesson complete | Yes |

### Lesson Assets — `api/lessons`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/lessons/{lessonId}/asset?type=pdf\|video` | Download lesson PDF or video (enrolled only) | Yes |

### Assessments — `api/assessments`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/assessments` | List published assessments (paged; optional `?categoryId=` and `?search=` filters — see [Content Categories](#content-categories)) | Yes |
| `GET` | `api/assessments/{id}` | Assessment detail | Yes |
| `POST` | `api/assessments/{id}/submit` | Submit answers (`AnswersJson` max 50 000 chars) | Yes |
| `GET` | `api/assessments/mine` | Current user's submissions | Yes |

### Categories — `api/categories`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/categories?scope=courses\|assessments&lang=en` | Categories with ≥1 published item in the scope, with counts (drives the browse/filter chips) | Yes |

### Certificates — `api/certificates`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/certificates/my` | List current user's certificates | Yes |
| `POST` | `api/certificates/issue/{courseId}` | Issue certificate for completed course (certificate-enabled plan required) | Yes |
| `GET` | `api/certificates/{certificateId}/download` | Download certificate PDF | Yes |
| `GET` | `api/certificates/verify/{verificationId}` | Public certificate verification | No |

### Subscriptions — `api/subscriptions`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/subscriptions/plans` | List plans | No |
| `GET` | `api/subscriptions/status` | Current user's subscription status | Yes |
| `POST` | `api/subscriptions/checkout` | Start checkout | Yes |
| `POST` | `api/subscriptions/webhook` | Payment webhook | No |
| `POST` | `api/subscriptions/cancel` | Cancel subscription | Yes |
| `GET` | `api/subscriptions/billing` | Billing history | Yes |

### Chat — `api/chat` + SignalR `/hubs/chat`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/chat/conversations` | List conversations | Yes |
| `GET` | `api/chat/conversations/{id}/messages` | Load messages | Yes |
| `POST` | `api/chat/conversations/start` | Start conversation | Yes |
| `DELETE` | `api/chat/conversations/{id}` | Delete conversation | Yes |
| `GET` | `api/chat/users` | Users available to chat | Yes |
| `GET` | `api/chat/unread-count` | Unread message count | Yes |
| — | `/hubs/chat` (SignalR) | Real-time hub | Yes (JWT via query string) |

### Video Calls — SignalR `/hubs/call`

Video calls have **no REST endpoints** — everything runs over the SignalR hub (WebRTC signaling) plus browser-to-browser media. Access is gated exactly like chat: **Admin role or a Pro subscription (`PrioritySupport` feature)**. See [Video Calls](#video-calls) for the full flow.

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| — | `/hubs/call` (SignalR) | Call signaling hub — `StartCall` / `AcceptCall` / `DeclineCall` / `CancelCall` / `LeaveCall` / `InviteToCall` / `RejoinCall` / `UpdateMediaState` / `GetCallableUsers`, plus `SendOffer` / `SendAnswer` / `SendIceCandidate` for WebRTC negotiation | Yes (JWT via query string) |

### Blog — `api/blog`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/blog/summaries` | Latest published summaries (`?count=6&lang=en`) | No |
| `GET` | `api/blog/{slug}` | Single article by slug (`?lang=en`) | No |

### Testimonials — `api/testimonials`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/testimonials` | All active testimonials ordered by `DisplayOrder` | No |

### Site Settings — `api/site`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/site/background-image` | Landing page background image | No |
| `POST` | `api/site/admin/background-image` | Upload landing page background image | Admin |

### Media — `api/media`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/media/{*filePath}` | Serve public media files (allowed folders: `blog/covers`, `testimonials/avatars`; allowed extensions: `.jpg .jpeg .png .gif .webp .avif .svg`) | No |

### Admin — Users — `api/admin`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/users` | List users (paged, searchable) | Admin |
| `GET` | `api/admin/users/{userId}` | User detail | Admin |
| `GET` | `api/admin/users/search` | Search users | Admin |
| `POST` | `api/admin/users/{userId}/roles/{roleName}` | Add role | Admin |
| `DELETE` | `api/admin/users/{userId}/roles/{roleName}` | Remove role | Admin |
| `GET` | `api/admin/roles` | List all roles | Admin |
| `POST` | `api/admin/roles/{roleName}` | Create role | Admin |
| `POST` | `api/admin/users/{userId}/toggle-enable` | Toggle enabled/disabled | Admin |
| `POST` | `api/admin/users/{userId}/disable` | Disable user | Admin |
| `POST` | `api/admin/users/{userId}/enable` | Enable user | Admin |
| `DELETE` | `api/admin/users/{userId}` | Delete user | Admin |
| `POST` | `api/admin/users/{userId}/force-password-reset` | Force password reset — emails reset link to user; returns `204 No Content` | Admin |
| `POST` | `api/admin/users/{userId}/set-password` | Directly set password | Admin |
| `POST` | `api/admin/users/{userId}/impersonate` | Generate temporary JWT as that user | Admin |

### Admin — Courses — `api/admin/courses`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/courses` | List all courses | Admin |
| `GET` | `api/admin/courses/{id}` | Course detail with modules and enrollments | Admin |
| `POST` | `api/admin/courses` | Create course | Admin |
| `PUT` | `api/admin/courses/{id}` | Update course | Admin |
| `DELETE` | `api/admin/courses/{id}` | Delete course | Admin |
| `POST` | `api/admin/courses/{id}/publish` | Publish | Admin |
| `POST` | `api/admin/courses/{id}/unpublish` | Unpublish | Admin |

### Admin — Modules — `api/admin/modules`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/modules/course/{courseId}` | List modules for a course | Admin |
| `GET` | `api/admin/modules/{id}` | Module detail | Admin |
| `POST` | `api/admin/modules` | Create module | Admin |
| `PUT` | `api/admin/modules/{id}` | Update module | Admin |
| `DELETE` | `api/admin/modules/{id}` | Delete module | Admin |

### Admin — Lessons — `api/admin/lessons`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/lessons/module/{moduleId}` | List lessons for a module | Admin |
| `POST` | `api/admin/lessons` | Create lesson | Admin |
| `PUT` | `api/admin/lessons/{id}` | Update lesson | Admin |
| `DELETE` | `api/admin/lessons/{id}` | Delete lesson | Admin |
| `POST` | `api/admin/lessons/{id}/upload/pdf` | Upload PDF | Admin |
| `POST` | `api/admin/lessons/{id}/upload/video` | Upload video | Admin |
| `POST` | `api/admin/lessons/{id}/publish` | Publish lesson | Admin |

### Admin — Assessments — `api/admin/assessments`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/assessments` | List assessments (paged) | Admin |
| `GET` | `api/admin/assessments/{id}` | Assessment detail | Admin |
| `POST` | `api/admin/assessments` | Create assessment | Admin |
| `PUT` | `api/admin/assessments/{id}` | Update assessment | Admin |
| `DELETE` | `api/admin/assessments/{id}` | Delete assessment | Admin |
| `POST` | `api/admin/assessments/{id}/publish` | Publish | Admin |
| `POST` | `api/admin/assessments/{id}/unpublish` | Unpublish | Admin |
| `GET` | `api/admin/assessments/{id}/submissions` | List submissions | Admin |

### Admin — Categories — `api/admin/categories`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/categories` | List categories with usage counts (paged) | Admin |
| `GET` | `api/admin/categories/all` | All categories (unpaged) — for course/assessment editor dropdowns | Admin |
| `POST` | `api/admin/categories` | Create category | Admin |
| `PUT` | `api/admin/categories/{id}` | Rename category | Admin |
| `DELETE` | `api/admin/categories/{id}` | Delete category — referencing courses/assessments become uncategorized, not hidden | Admin |

### Admin — Blog — `api/admin/blog`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/blog` | List articles (paged, searchable) | Admin |
| `GET` | `api/admin/blog/{id}` | Article detail | Admin |
| `POST` | `api/admin/blog` | Create article | Admin |
| `PUT` | `api/admin/blog/{id}` | Update article | Admin |
| `POST` | `api/admin/blog/{id}/publish` | Publish | Admin |
| `POST` | `api/admin/blog/{id}/unpublish` | Unpublish | Admin |
| `DELETE` | `api/admin/blog/{id}` | Delete article | Admin |
| `POST` | `api/admin/blog/{id}/upload/cover` | Upload cover image | Admin |

### Admin — Testimonials — `api/admin/testimonials`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/testimonials` | List testimonials (paged) | Admin |
| `GET` | `api/admin/testimonials/{id}` | Testimonial by id | Admin |
| `POST` | `api/admin/testimonials` | Create | Admin |
| `PUT` | `api/admin/testimonials/{id}` | Update | Admin |
| `POST` | `api/admin/testimonials/{id}/toggle-active` | Toggle active | Admin |
| `POST` | `api/admin/testimonials/{id}/move-up` | Move up | Admin |
| `POST` | `api/admin/testimonials/{id}/move-down` | Move down | Admin |
| `POST` | `api/admin/testimonials/{id}/upload/avatar` | Upload avatar | Admin |
| `DELETE` | `api/admin/testimonials/{id}/avatar` | Remove avatar | Admin |
| `DELETE` | `api/admin/testimonials/{id}` | Delete | Admin |

### Admin — Analytics — `api/admin/analytics`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/analytics/summary` | Dashboard summary | Admin |

### AI Assistant — `api/assistant`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `api/assistant/chat?lang=en\|el` | Grounded, streaming answer — response is `text/event-stream`, not JSON (see [AI Assistant](#ai-assistant)) | Yes |
| `GET` | `api/assistant/status` | Whether the local model backend is reachable | Yes |

### Admin — AI Assistant — `api/admin/assistant`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `api/admin/assistant/reindex` | Re-index published content immediately instead of waiting for the next scheduled pass | Admin |

---

## Roles

| Role | Access |
|------|--------|
| `Admin` | Full access — content authoring, user/role management, site settings |
| `Student` | Enroll in courses, view lessons, take assessments, manage profile/subscription, download certificates |

---

## Configuration

All secrets are loaded from `.env` at startup (see `.env.template`). The `.env` file is gitignored — never commit it.

| Key | Where | Notes |
|-----|-------|-------|
| `ConnectionStrings__DefaultConnection` | `.env` | Full SQL Server connection string. `appsettings.json` intentionally blank. Dev default (with `TrustServerCertificate=True`) is in `appsettings.Development.json`. |
| `Jwt__Key` | `.env` | **≥ 32 bytes required** — startup throws if shorter. |
| `Jwt:AccessTokenExpirationMinutes` | `appsettings.json` | Default `15`. |
| `Jwt:RefreshTokenExpirationDays` | `appsettings.json` | Default `7`. |
| `AdminUser__Password` | `.env` | Admin seed account password. |
| `SeedData__StudentPassword` | `.env` | Seed student password. Required when `SeedData:Enabled = true`. |
| `Payment:MockEnabled` | `appsettings.Development.json` | `true` in dev — skips real Stripe; uses mock checkout. |
| `Payment__WebhookSecret` | `.env` | Stripe HMAC signing secret. Leave unset in dev. |
| `AllowedHosts` | `.env` or env var | Default `localhost;127.0.0.1`. **Set to your production domain** (e.g. `reset-your-future.com;www.reset-your-future.com`) before deploying. |

`appsettings.Development.json`: `SeedData:Enabled`, `SeedData:BulkStudentCount`, `SeedData:JsonPaths:*`, `Payment:MockEnabled`, dev connection string.

**Production checklist:**
- `ASPNETCORE_ENVIRONMENT=Production`
- Real `Jwt__Key` (≥ 32 bytes), `ConnectionStrings__DefaultConnection` (no `TrustServerCertificate=True`)
- `AllowedHosts` set to the production domain
- `IEmailService` real implementation registered (startup throws if absent in Production)
- Migrations run automatically at startup (`MigrateAsync`); ensure the DB user has `dbcreator` or schema-alter rights on first deploy

---

## Email

`StubEmailService` is registered **only in Development**. It logs all emails to file instead of sending them — find links in `Logs/log-YYYY-MM-DD.txt` (search `STUB EMAIL`).

> **Production:** `StubEmailService` is intentionally absent. The application will **throw at startup** unless a real `IEmailService` implementation (SendGrid, SMTP, etc.) is registered.

Dev shortcuts for bypassing email confirmation:

| Endpoint | Purpose |
|----------|---------|
| `POST api/auth/dev/confirm-email` | Confirm email without a link |
| `POST api/auth/dev/reset-password` | Reset password without an email |

> ⚠️ Both endpoints are wrapped in `#if DEBUG` and are **not compiled into Release builds** — they will 404 in production.

---

## Content Categories

Admins classify **courses and assessments** with a shared pool of categories, and students browse/filter the public Courses and Assessments pages by them.

- **One category per item** (nullable) drawn from a **single shared pool** used by both content types. Each category has bilingual names (`NameEn` required, `NameEl` optional, falling back to English).
- **Authoring:** pick a category in the course/assessment editor, or create one inline while authoring. A dedicated **`/admin/categories`** page manages the pool (create, rename, delete) with usage counts.
- **Browsing:** the public Courses and Assessments pages show a category chip on each card plus a filter bar of chips (with per-category counts) and a debounced search box. Both filters are applied server-side via the `?categoryId=` and `?search=` query params before pagination, so page totals stay correct.
- **Deleting a category** soft-deletes it and nulls the reference on any course/assessment that used it — that content becomes "uncategorized" and stays visible, never hidden.

## Video Calls

One-to-one and group video calls, started from the existing chat. Self-hosted **WebRTC** (peer-to-peer mesh, up to 6 participants) with **SignalR** (`/hubs/call`) handling only signaling — media flows browser-to-browser and never touches the server.

- **Access gate:** identical to chat — **Admin role, or a Pro subscription** (the `PrioritySupport` feature). Free users don't see call controls.
- **Features:** mic mute, camera toggle, screen share, and call events (started / missed / ended + duration) persisted into chat history.
- **"Ring anywhere":** an incoming-call overlay pops up on any page via a single call-host component mounted in the layout. 1:1 calls start from the conversation header; group calls from a multi-select picker, and participants can be added mid-call.
- **Reliability:** a background `CallRingMonitor` times out unanswered rings (45 s default), reaps disconnected participants after a short grace period, and sweeps dangling sessions left by a server restart. Ring timeout, max participants, and ICE/STUN servers are configurable under the `WebRtc` section of `appsettings.json`.

> **Browser permissions:** the site's `Permissions-Policy` header allows `camera`, `microphone`, and `display-capture` for same-origin so calls work; JWT for the call hub is passed as the `access_token` query-string parameter, same as the chat hub.

---

## AI Assistant

A grounded, bilingual (EN/EL) AI helper available to every authenticated user, regardless of subscription tier. It answers from a background index of published courses, lessons, assessments, and blog articles, and personalizes recommendations using the user's subscription tier and enrollments. Everything runs **locally** via [Ollama](https://ollama.com) — no cloud API, no per-token cost, no data leaves the machine.

**How it works:** a background service chunks and embeds published content into an `AssistantContentChunks` table (incrementally — only re-embedding what changed). Each question embeds the query, retrieves the top matching chunks by cosine similarity, injects them plus the user's tier and enrolled course titles into a scoped system prompt, and streams the model's reply to a floating chat widget over Server-Sent Events. It is **not** an autonomous tool-calling agent — a single grounded RAG turn is what keeps a small local model reliable. The widget appears bottom-right on every page for signed-in users; when the assistant is disabled or Ollama is unreachable, it shows an "unavailable" state and the rest of the app is unaffected.

**Setup:**

```bash
# 1. Install Ollama: https://ollama.com/download
# 2. Pull the default models
ollama pull gemma3:4b
ollama pull bge-m3
# 3. Enable it in appsettings.json / .env
#    Assistant__Enabled=true
```

The assistant is **disabled by default** (`Assistant:Enabled = false`), so the app, its build, and its test suite never require Ollama to be installed.

| Key | Where | Default | Notes |
|-----|-------|---------|-------|
| `Assistant__Enabled` | `.env` / `appsettings.json` | `false` | Master switch. When `false`, the API and widget gracefully report the assistant as unavailable. |
| `Assistant__BaseUrl` | `appsettings.json` | `http://localhost:11434` | Ollama's default local address. |
| `Assistant__ChatModel` | `appsettings.json` | `gemma3:4b` | ~3.3 GB (Q4). Alternatives: `qwen3:4b` (Apache-2.0), or an 8B Greek-tuned model (e.g. ILSP Llama-Krikri, GGUF-imported) on stronger hardware. |
| `Assistant__EmbeddingModel` | `appsettings.json` | `bge-m3` | ~1.2 GB. Multilingual (EN/EL) retrieval embeddings. |
| `Assistant__MaxContextChunks` | `appsettings.json` | `6` | Retrieved chunks injected per question. |
| `Assistant__MaxOutputTokens` | `appsettings.json` | `500` | Caps answer length. |
| `Assistant__Temperature` | `appsettings.json` | `0.3` | Lower = more deterministic/grounded answers. |
| `Assistant__RequestsPerMinute` | `appsettings.json` | `10` | Per-user rate limit on `POST api/assistant/chat`. |

**Hardware guidance:** runs CPU-only on any ~2020+ 4-core machine with 8 GB RAM — no GPU required. Expect a few seconds to the first token (streaming masks this) and roughly 8–15 tokens/sec on CPU with the default models.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| DB connection fails | `sqllocaldb info MSSQLLocalDB`. Verify connection string. If the instance is stopped: `sqllocaldb start MSSQLLocalDB`. |
| Database missing / dropped | Just restart the app — `MigrateAsync` at startup recreates and reseeds it automatically. |
| Adding a new migration | `dotnet ef migrations add <Name> --project src/ResetYourFuture.Infrastructure --startup-project src/ResetYourFuture.Web` — then restart; migrations apply on next run. |
| Seed data missing | `SeedData:Enabled = true` in `appsettings.Development.json`. |
| Email link not found | Search `STUB EMAIL` in `Logs/log-<today>.txt` or use dev endpoints. |
| Role-based page inaccessible | Check `AspNetUserRoles` table. Admin pages require `Admin` role. |
| Chat not connecting | JWT via `access_token` query string. Check token expiry (default 15 min) — use `api/auth/refresh` to rotate. |
| Video call has no camera/mic | Grant the browser camera/microphone permission for the site, and use HTTPS (WebRTC requires a secure context). Call controls only appear for Admins or Pro subscribers. |
| `401` after login | Match `Jwt:Key/Issuer/Audience`. Disabled accounts return `X-User-Disabled: true`. |
| HTTPS not trusted | `dotnet dev-certs https --trust` |
| Assistant shows "unavailable" | Set `Assistant__Enabled=true`, confirm Ollama is running (`ollama list`), and that both models are pulled (`ollama pull gemma3:4b && ollama pull bge-m3`). |
| Assistant's first answer is slow | Expected — Ollama cold-loads the model into memory on first request after startup/idle. Subsequent answers are faster. |

---

## Security

| Feature | Details |
|---------|---------|
| Auth cookies | `HttpOnly`, `SameSite=Strict`, `Secure` (non-dev), 24 h sliding window, 7-day `MaxAge` hard cap |
| JWT tokens | HS256, 15-min expiry, security-stamp validated on every request, key ≥ 32 bytes enforced at startup |
| Refresh tokens | SHA-256-hashed, single-use rotation; revoked token chain tracked |
| XSS prevention | All rich-text inputs sanitised with Ganss.Xss (`IHtmlSanitizer`) |
| Rate limiting | `"auth"` policy (global) on register / login / confirm-email / forgot-password / reset-password; `"assistant"` policy (per-user) on the AI chat endpoint |
| HSTS | Enabled in Production; skipped in Development |
| Security headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` (`camera`/`microphone`/`display-capture` allowed same-origin for video calls; `geolocation` denied) |
| File uploads | Content-type allowlist enforced per upload type (image / PDF / video); extension allowlist on media serve |
| Sitemap | Slugs XML-escaped via `SecurityElement.Escape()` |
| Account enumeration | Login, forgot-password, reset-password all return generic messages; duplicate-email registration mapped to generic error |

---

## Logging

Daily rotating log files at `src/ResetYourFuture.Web/Logs/log-YYYY-MM-DD.txt`. The `Logs/` directory and all `*.log` files are gitignored.
