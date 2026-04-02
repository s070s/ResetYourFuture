# ResetYourFuture

A psychosocial career counseling platform with courses, assessments, real-time chat, subscriptions, blog, testimonials, and certificate generation.

---

## Quickstart

```bash
# 1. Clone
git clone https://github.com/s070s/ResetYourFuture.git
cd ResetYourFuture

# 2. Trust HTTPS dev certificate (once per machine)
dotnet dev-certs https --trust

# 3. Install EF Core tools (once per machine)
dotnet tool install --global dotnet-ef

# 4. Set the connection string in src/ResetYourFuture.Web/appsettings.json
# "ConnectionStrings": {
#   "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ResetYourFutureDb;Trusted_Connection=True;Connect Timeout=30;TrustServerCertificate=True;"
# }

# 5. Restore packages
dotnet restore

# 6. Create the initial migration
dotnet ef migrations add InitialCreate  --project src/ResetYourFuture.Web  --startup-project src/ResetYourFuture.Web  --context ApplicationDbContext  --output-dir Data/Migrations

# 7. Apply migrations
dotnet ef database update  --project src/ResetYourFuture.Web  --startup-project src/ResetYourFuture.Web  --context ApplicationDbContext

# 8. Build
dotnet build

# 9. Run
dotnet run --project src/ResetYourFuture.Web
# Visual Studio: right-click Solution → Configure Startup Projects → set ResetYourFuture.Web → F5
```

**Admin:** `admin@resetyourfuture.local` / `Admin123!`
**Students:** `Student123!`

> Seed data (students, courses, assessments) runs automatically in Development when `SeedData:Enabled = true` in `appsettings.Development.json`. Up to 2000 students are created in the background.

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 |
| Frontend / Backend | Blazor SSR + ASP.NET Core Web API |
| ORM | Entity Framework Core 10 (SQL Server) |
| Auth | ASP.NET Core Identity · Cookie (SSR) · JWT Bearer · Refresh tokens |
| Real-time | SignalR (`/hubs/chat`) |
| PDF | QuestPDF 2026.2.4 |
| Localization | English + Greek (`.resx`) |
| Logging | Custom daily file logger |
| Email | `StubEmailService` — logs to file, no SMTP needed in dev |

---

## Solution Structure

```
ResetYourFuture.sln
└── src/
    ├── ResetYourFuture.Web/      Full-stack Blazor SSR — the only deployable project
    └── ResetYourFuture.Shared/   DTOs, .resx resources, JSON seed data
```

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
| `POST` | `api/auth/dev/confirm-email` | Dev-only: confirm email without link | Dev |
| `POST` | `api/auth/dev/reset-password` | Dev-only: reset password without email | Dev |

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
| `GET` | `api/courses` | List published courses | Yes |
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
| `GET` | `api/assessments` | List published assessments (paged) | Yes |
| `GET` | `api/assessments/{id}` | Assessment detail | Yes |
| `POST` | `api/assessments/{id}/submit` | Submit answers | Yes |
| `GET` | `api/assessments/mine` | Current user's submissions | Yes |

### Certificates — `api/certificates`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/certificates/my` | List current user's certificates | Yes |
| `POST` | `api/certificates/issue/{courseId}` | Issue certificate for completed course | Yes |
| `GET` | `api/certificates/{certificateId}/download` | Download certificate PDF | Yes |
| `GET` | `api/certificates/verify/{verificationId}` | Public certificate verification | No |

### Subscriptions — `api/subscription`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/subscription/plans` | List plans | No |
| `GET` | `api/subscription/status` | Current user's subscription status | Yes |
| `POST` | `api/subscription/checkout` | Start checkout | Yes |
| `POST` | `api/subscription/webhook` | Payment webhook | No |
| `POST` | `api/subscription/cancel` | Cancel subscription | Yes |
| `GET` | `api/subscription/billing` | Billing history | Yes |

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
| `GET` | `api/media/{*filePath}` | Serve public media files | No |

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
| `POST` | `api/admin/users/{userId}/force-password-reset` | Force password reset (sends token) | Admin |
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

---

## Roles

| Role | Access |
|------|--------|
| `Admin` | Full access — content authoring, user/role management, site settings |
| `Student` | Enroll in courses, view lessons, take assessments, manage profile/subscription, download certificates |

---

## Configuration

`appsettings.json`: `ConnectionStrings:DefaultConnection`, `Jwt:Key` (**change in production**, min 32 chars), `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpirationMinutes` (default `60`), `Jwt:RefreshTokenExpirationDays` (default `7`).

`appsettings.Development.json`: `SeedData:Enabled`, `SeedData:StudentPassword`, `SeedData:JsonPaths:*`.

**Production:** set `ASPNETCORE_ENVIRONMENT=Production`, real `Jwt__Key`, `ConnectionStrings__DefaultConnection` via env var, run migrations before start.

---

## Email

`StubEmailService` logs links to file instead of sending — find them in `Logs/log-YYYY-MM-DD.txt` (search `STUB EMAIL`). Dev shortcuts: `POST api/auth/dev/confirm-email` / `POST api/auth/dev/reset-password`.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| DB connection fails | `sqllocaldb info MSSQLLocalDB`. Verify connection string. |
| Migration fails | Set default + startup project to `src\ResetYourFuture.Web`. |
| Seed data missing | `SeedData:Enabled = true` in `appsettings.Development.json`. |
| Email link not found | Search `STUB EMAIL` in `Logs/log-<today>.txt` or use dev endpoints. |
| Role-based page inaccessible | Check `AspNetUserRoles` table. Admin pages require `Admin` role. |
| Chat not connecting | JWT via `access_token` query string. Check token expiry (default 60 min). |
| `401` after login | Match `Jwt:Key/Issuer/Audience`. Disabled accounts return `X-User-Disabled: true`. |
| HTTPS not trusted | `dotnet dev-certs https --trust` |

---

## Logging

Daily rotating log files at `src/ResetYourFuture.Web/Logs/log-YYYY-MM-DD.txt`.
