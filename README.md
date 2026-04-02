# ResetYourFuture

A psychosocial career counseling platform with a Udemy-style course component.
Supports course / module / lesson authoring, self-assessments, real-time chat, subscription plans, student progress tracking, blog articles, testimonials, and certificate generation.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Tech Stack](#tech-stack)
3. [Solution Structure](#solution-structure)
4. [Quickstart for Development](#quickstart-for-development)
5. [Quickstart for Production](#quickstart-for-production)
6. [Quickstart for Deployment](#quickstart-for-deployment)
7. [CLI Commands Reference](#cli-commands-reference)
8. [EF Core Migrations](#ef-core-migrations)
9. [Endpoints](#endpoints)
10. [Roles](#roles)
11. [Default Admin Account To Log In](#default-admin-account-to-log-in)
12. [Credentials for Seeded Students](#credentials-for-seeded-students)
13. [How Email Confirmation Works](#how-email-confirmation-works)
14. [Walkthrough the Configurations of Each Project](#walkthrough-the-configurations-of-each-project)
15. [File Based Logging](#file-based-logging)
16. [Troubleshooting](#troubleshooting)
17. [Adding Seed Content for App Startup](#adding-seed-content-for-app-startup)
18. [Assessment Authoring Guide for Admins](#assessment-authoring-guide-for-admins)
19. [Course, Module, Lesson Authoring Guide for Admins](#course-module-lesson-authoring-guide-for-admins)

---

## Prerequisites

Install the following before opening the solution:

| Tool | Notes |
|------|-------|
| [Visual Studio 2022+](https://visualstudio.microsoft.com/) | Workloads: **ASP.NET and web development**, **.NET desktop development** |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | The solution targets `net10.0` (pinned via `global.json`) |
| [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) | **LocalDB** (ships with Visual Studio), Developer, or Express edition |
| [SQL Server Management Studio (SSMS)](https://aka.ms/ssmsfullsetup) | Optional but recommended for inspecting the database |
| [Git](https://git-scm.com/) | For cloning the repository |

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 10 |
| Backend | ASP.NET Core Web API (controllers, OpenAPI) |
| Frontend | Blazor SSR (`ResetYourFuture.Web`) |
| ORM | Entity Framework Core 10.0.5 (SQL Server provider) |
| Database | SQL Server (LocalDB for dev) |
| Auth | ASP.NET Core Identity + Cookie (SSR pages) + JWT Bearer + Refresh tokens |
| Real-time | SignalR (`/hubs/chat`) |
| PDF | QuestPDF 2026.2.4 (certificate generation) |
| Localization | Built-in `Microsoft.Extensions.Localization` (English + Greek) |
| Logging | Custom file logger (daily rotating text files) |
| Email | `StubEmailService` — logs emails to file in dev (no external SMTP required) |
| Shared | `ResetYourFuture.Shared` class library (DTOs, resources, JSON seed data) |

---

## Solution Structure

```
ResetYourFuture.sln
├── src/
│   ├── ResetYourFuture.Web/      Full-stack Blazor SSR app   https://localhost:7090  ← runs
│   ├── ResetYourFuture.Shared/   DTOs, .resx resources, JSON seed data               ← referenced
│   ├── ResetYourFuture.Api/      ASP.NET Core Web API (legacy, superseded)
│   └── ResetYourFuture.Client/   Blazor WASM standalone (legacy, superseded)
└── Evaluation/                   Architecture & design docs
```

**Only `ResetYourFuture.Web` is run.** It is a full-stack Blazor SSR application that contains its own API controllers, `ApplicationDbContext`, EF Core migrations, seed logic, SignalR hub, and authentication. `ResetYourFuture.Shared` is a class library that is compiled in — it is not deployed separately. The `Api` and `Client` projects are legacy and kept for reference only.

---

## Quickstart for Development

1. **Clone the repository**

```bash
git clone https://github.com/s070s/ResetYourFuture.git
cd ResetYourFuture
```

2. **Open** `ResetYourFuture.sln` in Visual Studio.

3. **Trust the HTTPS dev certificate** (once per machine):

```bash
dotnet dev-certs https --trust
```

4. **Verify the connection string** in `src/ResetYourFuture.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ResetYourFutureDb;Trusted_Connection=True;Connect Timeout=30;TrustServerCertificate=True;"
}
```

> If you use a full SQL Server instance instead of LocalDB, update the connection string accordingly.

5. **Apply database migrations** before running for the first time:

   ```bash
   dotnet ef database update \
     --project src/ResetYourFuture.Web \
     --startup-project src/ResetYourFuture.Web
   ```

   Or via Package Manager Console: `Update-Database`

6. **Set startup project**:
   - Right-click the Solution → **Configure Startup Projects…**
   - Set **ResetYourFuture.Web** as the single startup project.

7. **Press `F5`** to run.

| URL | Description |
|-----|-------------|
| `https://localhost:7090` | Blazor Web (SSR) — the app |
| `https://localhost:7090/openapi/v1.json` | OpenAPI spec (Development only) |

8. **Seed data** (courses, assessments, students) loads automatically in `Development` mode when `SeedData:Enabled` is `true` in `appsettings.Development.json`. Wait for the seeding log messages to finish before testing.
---

## Quickstart for Production

1. Set the environment to `Production`:

```
ASPNETCORE_ENVIRONMENT=Production
```

2. Provide a **production connection string** via environment variables or a secrets manager — **never** leave credentials in `appsettings.json` for production.

3. **Replace the JWT key.** The default key in `appsettings.json` (`CHANGE_THIS_IN_PRODUCTION_MIN_32_CHARS!!`) is a placeholder. Set a strong, unique value of at least 32 characters:

```
Jwt__Key=<your-strong-secret-key-min-32-chars>
```

4. Optionally override the admin credentials:

```
AdminUser__Email=admin@yourdomain.com
AdminUser__Password=<strong-password>
```

5. **Apply migrations** before starting the app: `dotnet ef database update --project src/ResetYourFuture.Web --startup-project src/ResetYourFuture.Web`. For CI/CD environments you can generate a SQL script instead (see [EF Core Migrations](#ef-core-migrations)).

6. Development-only seed data (courses, assessments, students) is **skipped** in Production because `SeedData:Enabled` defaults to `false`.

---

## Quickstart for Deployment

`ResetYourFuture.Web` is the only deployable project. It is a self-contained server-side application — deploy it to Azure App Service, IIS, a Linux host, or a container.

1. **Publish**:
   - Visual Studio: right-click `ResetYourFuture.Web` → **Publish** → select target
   - CLI: `dotnet publish src/ResetYourFuture.Web -c Release -o ./publish`

2. **Set environment variables** on the host:
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__Key` (min 32 chars, strong random value)
   - `Jwt__Issuer`, `Jwt__Audience`
   - `AdminUser__Email`, `AdminUser__Password`

3. **Run database migrations** — they run automatically at startup, or generate a SQL script for DBA-managed environments (see [EF Core Migrations](#ef-core-migrations)).

---

## CLI Commands Reference

All commands run from the **solution root** unless otherwise noted.

### Setup

```bash
git clone https://github.com/s070s/ResetYourFuture.git
cd ResetYourFuture
dotnet dev-certs https --trust     # Trust HTTPS dev certificate (once per machine)
dotnet restore                     # Restore all NuGet packages
```

### Build & Run

```bash
dotnet build                                      # Build entire solution
dotnet run --project src/ResetYourFuture.Web      # Run (https://localhost:7090)
dotnet watch --project src/ResetYourFuture.Web    # Hot-reload
dotnet publish src/ResetYourFuture.Web -c Release -o ./publish
```

### EF Core CLI

```bash
# Install the global tool once
dotnet tool install --global dotnet-ef

# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project src/ResetYourFuture.Web \
  --startup-project src/ResetYourFuture.Web

# Apply pending migrations
dotnet ef database update \
  --project src/ResetYourFuture.Web \
  --startup-project src/ResetYourFuture.Web

# Remove the last migration (only if not yet applied)
dotnet ef migrations remove \
  --project src/ResetYourFuture.Web \
  --startup-project src/ResetYourFuture.Web

# List all migrations and their applied status
dotnet ef migrations list \
  --project src/ResetYourFuture.Web \
  --startup-project src/ResetYourFuture.Web

# Generate a SQL script for production / DBA review
dotnet ef migrations script \
  --project src/ResetYourFuture.Web \
  --startup-project src/ResetYourFuture.Web \
  --output ./migrations.sql
```

### LocalDB (PowerShell)

```powershell
sqllocaldb info MSSQLLocalDB       # Show instance status
sqllocaldb start MSSQLLocalDB      # Start instance
sqllocaldb stop MSSQLLocalDB       # Stop instance
sqllocaldb delete MSSQLLocalDB     # Delete instance — DB is recreated on next app start
```

### Tail Logs (PowerShell)

```powershell
Get-Content -Path .\src\ResetYourFuture.Web\Logs\log-$(Get-Date -Format yyyy-MM-dd).txt -Wait -Tail 50
```

### Git Workflow

```bash
git status
git log --oneline -10
git checkout -b feature/<name>     # Create feature branch
git add src/                       # Stage changes (prefer specific paths over -A)
git commit -m "feat: <description>"
git push -u origin feature/<name>
git pull --rebase origin master    # Rebase branch onto latest master
```

---

## EF Core Migrations

> **Note:** Migrations must be applied manually before running the app. Use the commands below when setting up for the first time or after changing the data model.

### Package Manager Console (Visual Studio)

**Open PMC:** Tools → NuGet Package Manager → Package Manager Console

| PMC setting | Value |
|-------------|-------|
| **Default project** | `src\ResetYourFuture.Web` (contains `ApplicationDbContext`) |
| **Startup project** | `ResetYourFuture.Web` (set in Solution Explorer) |

```
Add-Migration <MigrationName>      # Add a new migration
Update-Database                    # Apply migrations
Remove-Migration                   # Remove last migration (if not yet applied)
Get-Migration                      # List all migrations
Script-Migration                   # Generate SQL script for production
```

The project includes a `DesignTimeDbContextFactory` (`src/ResetYourFuture.Web/Data/DesignTimeDbContextFactory.cs`) so both PMC and `dotnet ef` CLI commands work outside of the running application.

---

## Endpoints

### Auth — `api/auth`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `POST` | `api/auth/register` | Register a new user (Student role by default) | No |
| `GET` | `api/auth/confirm-email` | Confirm email via token link | No |
| `POST` | `api/auth/login` | Log in and receive JWT + refresh token | No |
| `POST` | `api/auth/forgot-password` | Request a password-reset email | No |
| `POST` | `api/auth/reset-password` | Reset password with token | No |
| `GET` | `api/auth/me` | Get current user info from JWT | Yes |
| `POST` | `api/auth/dev/confirm-email` | Dev-only: confirm email without a real email | Dev only |
| `POST` | `api/auth/dev/reset-password` | Dev-only: reset password without email | Dev only |

### Profile — `api/profile`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/profile` | Get current user's profile | Yes |
| `PUT` | `api/profile` | Update profile | Yes |
| `POST` | `api/profile/avatar` | Upload avatar image | Yes |
| `GET` | `api/profile/avatar` | Get avatar image | Yes |
| `POST` | `api/profile/change-password` | Change password | Yes |

### Courses — `api/courses` (Student-facing)

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/courses` | List published courses | Yes |
| `GET` | `api/courses/{courseId}` | Get course detail with modules and lessons | Yes |
| `POST` | `api/courses/{courseId}/enroll` | Enroll in a course | Yes |
| `GET` | `api/courses/lessons/{lessonId}` | Get lesson detail | Yes |
| `POST` | `api/courses/lessons/{lessonId}/complete` | Mark lesson as complete | Yes |

### Lesson Assets — `api/lessons`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/lessons/{lessonId}/asset?type=pdf\|video` | Download lesson PDF or video (enrolled students only) | Yes |

### Assessments — `api/assessments` (Student-facing)

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/assessments` | List published assessments (paged) | Yes |
| `GET` | `api/assessments/{id}` | Get assessment detail | Yes |
| `POST` | `api/assessments/{id}/submit` | Submit assessment answers | Yes |
| `GET` | `api/assessments/mine` | Get current user's submissions | Yes |

### Certificates — `api/certificates`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/certificates/my` | List current user's certificates | Yes |
| `POST` | `api/certificates/issue/{courseId}` | Issue certificate for a completed course | Yes |
| `GET` | `api/certificates/{certificateId}/download` | Download certificate PDF | Yes |
| `GET` | `api/certificates/verify/{verificationId}` | Public certificate verification | No |

### Subscriptions — `api/subscription`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/subscription/plans` | List subscription plans (public) | No |
| `GET` | `api/subscription/status` | Get current user's subscription status | Yes |
| `POST` | `api/subscription/checkout` | Start checkout | Yes |
| `POST` | `api/subscription/webhook` | Payment webhook callback | No |
| `POST` | `api/subscription/cancel` | Cancel subscription | Yes |
| `GET` | `api/subscription/billing` | Get billing history | Yes |

### Chat — `api/chat` + SignalR Hub `/hubs/chat`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/chat/conversations` | List conversations | Yes |
| `GET` | `api/chat/conversations/{id}/messages` | Load messages for a conversation | Yes |
| `POST` | `api/chat/conversations/start` | Start a new conversation | Yes |
| `DELETE` | `api/chat/conversations/{id}` | Delete conversation and all its messages | Yes |
| `GET` | `api/chat/users` | List users available to chat | Yes |
| `GET` | `api/chat/unread-count` | Get unread message count | Yes |
| — | `/hubs/chat` (SignalR) | Real-time messaging hub | Yes (JWT via query string) |

### Blog — `api/blog` (public)

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/blog/summaries` | Latest published article summaries (`?count=6&lang=en`) | No |
| `GET` | `api/blog/{slug}` | Single published article by slug (`?lang=en`) | No |

### Testimonials — `api/testimonials` (public)

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/testimonials` | All active testimonials ordered by `DisplayOrder` | No |

### Site Settings — `api/site`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/site/background-image` | Get landing page background image | No |
| `POST` | `api/site/admin/background-image` | Upload landing page background image | Admin |

### Media — `api/media`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/media/{*filePath}` | Serve public media files (blog covers, testimonial avatars) | No |

### Admin — User Management — `api/admin`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/users` | List users (paged, searchable) | Admin |
| `GET` | `api/admin/users/{userId}` | Get user detail | Admin |
| `GET` | `api/admin/users/search` | Search users | Admin |
| `POST` | `api/admin/users/{userId}/roles/{roleName}` | Add role to user | Admin |
| `DELETE` | `api/admin/users/{userId}/roles/{roleName}` | Remove role from user | Admin |
| `GET` | `api/admin/roles` | List all roles | Admin |
| `POST` | `api/admin/roles/{roleName}` | Create a new role | Admin |
| `POST` | `api/admin/users/{userId}/toggle-enable` | Toggle user enabled/disabled | Admin |
| `POST` | `api/admin/users/{userId}/disable` | Disable user | Admin |
| `POST` | `api/admin/users/{userId}/enable` | Enable user | Admin |
| `DELETE` | `api/admin/users/{userId}` | Delete user | Admin |
| `POST` | `api/admin/users/{userId}/force-password-reset` | Force password reset (sends reset token) | Admin |
| `POST` | `api/admin/users/{userId}/set-password` | Directly set a new password for a user | Admin |
| `POST` | `api/admin/users/{userId}/impersonate` | Generate temporary JWT to view app as that user | Admin |

### Admin — Courses — `api/admin/courses`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/courses` | List all courses (published and unpublished) | Admin |
| `GET` | `api/admin/courses/{id}` | Get course detail with modules and enrollments | Admin |
| `POST` | `api/admin/courses` | Create a course | Admin |
| `PUT` | `api/admin/courses/{id}` | Update a course | Admin |
| `DELETE` | `api/admin/courses/{id}` | Delete a course | Admin |
| `POST` | `api/admin/courses/{id}/publish` | Publish a course | Admin |
| `POST` | `api/admin/courses/{id}/unpublish` | Unpublish a course | Admin |

### Admin — Modules — `api/admin/modules`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/modules/course/{courseId}` | List modules for a course | Admin |
| `GET` | `api/admin/modules/{id}` | Get module detail | Admin |
| `POST` | `api/admin/modules` | Create a module | Admin |
| `PUT` | `api/admin/modules/{id}` | Update a module | Admin |
| `DELETE` | `api/admin/modules/{id}` | Delete a module | Admin |

### Admin — Lessons — `api/admin/lessons`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/lessons/module/{moduleId}` | List lessons for a module | Admin |
| `POST` | `api/admin/lessons` | Create a lesson | Admin |
| `PUT` | `api/admin/lessons/{id}` | Update a lesson | Admin |
| `DELETE` | `api/admin/lessons/{id}` | Delete a lesson | Admin |
| `POST` | `api/admin/lessons/{id}/upload/pdf` | Upload PDF for a lesson | Admin |
| `POST` | `api/admin/lessons/{id}/upload/video` | Upload video for a lesson | Admin |
| `POST` | `api/admin/lessons/{id}/publish` | Publish a lesson | Admin |

### Admin — Assessments — `api/admin/assessments`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/assessments` | List all assessment definitions (paged) | Admin |
| `GET` | `api/admin/assessments/{id}` | Get assessment detail | Admin |
| `POST` | `api/admin/assessments` | Create assessment definition | Admin |
| `PUT` | `api/admin/assessments/{id}` | Update assessment definition | Admin |
| `DELETE` | `api/admin/assessments/{id}` | Delete assessment definition | Admin |
| `POST` | `api/admin/assessments/{id}/publish` | Publish assessment | Admin |
| `POST` | `api/admin/assessments/{id}/unpublish` | Unpublish assessment | Admin |
| `GET` | `api/admin/assessments/{id}/submissions` | List submissions for an assessment | Admin |

### Admin — Blog — `api/admin/blog`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/blog` | List all articles (paged, searchable) | Admin |
| `GET` | `api/admin/blog/{id}` | Get article detail | Admin |
| `POST` | `api/admin/blog` | Create article | Admin |
| `PUT` | `api/admin/blog/{id}` | Update article | Admin |
| `POST` | `api/admin/blog/{id}/publish` | Publish article | Admin |
| `POST` | `api/admin/blog/{id}/unpublish` | Unpublish article | Admin |
| `DELETE` | `api/admin/blog/{id}` | Delete article | Admin |
| `POST` | `api/admin/blog/{id}/upload/cover` | Upload cover image | Admin |

### Admin — Testimonials — `api/admin/testimonials`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/testimonials` | List all testimonials (paged) | Admin |
| `GET` | `api/admin/testimonials/{id}` | Get testimonial by id | Admin |
| `POST` | `api/admin/testimonials` | Create testimonial | Admin |
| `PUT` | `api/admin/testimonials/{id}` | Update testimonial | Admin |
| `POST` | `api/admin/testimonials/{id}/toggle-active` | Toggle active state | Admin |
| `POST` | `api/admin/testimonials/{id}/move-up` | Move up in display order | Admin |
| `POST` | `api/admin/testimonials/{id}/move-down` | Move down in display order | Admin |
| `POST` | `api/admin/testimonials/{id}/upload/avatar` | Upload avatar image | Admin |
| `DELETE` | `api/admin/testimonials/{id}/avatar` | Remove avatar | Admin |
| `DELETE` | `api/admin/testimonials/{id}` | Delete testimonial | Admin |

### Admin — Analytics — `api/admin/analytics`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/analytics/summary` | Dashboard analytics summary | Admin |

---

## Roles

| Role | Description |
|------|-------------|
| `Admin` | Full access. Can author courses, modules, lessons, assessments, blog articles, and testimonials. Manages users, roles, subscriptions, and site settings. |
| `Student` | Can enroll in courses, view lessons, complete assessments, manage profile and subscriptions, download certificates. |

Roles are seeded automatically on startup in `Program.cs`.

---

## Default Admin Account To Log In

| Field | Value |
|-------|-------|
| Email | `admin@resetyourfuture.local` |
| Password | `Admin123!` |

These defaults are set in `appsettings.json` under `AdminUser` and can be overridden via environment variables (`AdminUser__Email`, `AdminUser__Password`). The admin user is seeded on every startup if it does not already exist.

---

## Credentials for Seeded Students

Student accounts are only seeded in **Development** mode when `SeedData:Enabled` is `true`.

All students share the same password: **`Student123!`**

The email for each student is auto-generated as `firstname.lastname@resetyourfuture.local`.

| Email | Password |
|-------|----------|
| `alice.johnson@resetyourfuture.local` | `Student123!` |
| `bob.smith@resetyourfuture.local` | `Student123!` |
| `charlie.williams@resetyourfuture.local` | `Student123!` |
| `diana.brown@resetyourfuture.local` | `Student123!` |
| `edward.jones@resetyourfuture.local` | `Student123!` |
| `fiona.garcia@resetyourfuture.local` | `Student123!` |
| `george.miller@resetyourfuture.local` | `Student123!` |
| `hannah.davis@resetyourfuture.local` | `Student123!` |
| `ivan.martinez@resetyourfuture.local` | `Student123!` |
| `julia.wilson@resetyourfuture.local` | `Student123!` |

Student JSON source: `src/ResetYourFuture.Shared/JSON/Students/students.json`

---

## How Email Confirmation Works

The app uses a **`StubEmailService`** (registered as `IEmailService`) that **logs emails** instead of sending them. No external SMTP server is required for development.

### Registration flow

1. User calls `POST api/auth/register`.
2. The API creates the user via ASP.NET Core Identity with `EmailConfirmed = false`.
3. A confirmation token is generated and a confirmation link is built.
4. `StubEmailService.SendEmailConfirmationAsync` logs the link to the file logger and console.
5. In **Development** mode the dev shortcut `POST api/auth/dev/confirm-email` can confirm the account without clicking the link.
6. In Production, replace `StubEmailService` with a real implementation (e.g., SendGrid, Mailgun) by swapping the `IEmailService` DI registration in `Program.cs`.

### Where to find the confirmation link in dev

Open the daily log file at `src/ResetYourFuture.Web/Logs/log-YYYY-MM-DD.txt` and search for `STUB EMAIL - Email Confirmation`. The full link is printed there.

### Password reset

The same pattern applies: `POST api/auth/forgot-password` → stub logs the reset link → user opens the link or uses `POST api/auth/dev/reset-password` in dev.

---

## Walkthrough the Configurations of Each Project

### Web Project — `ResetYourFuture.Web`

The full-stack Blazor SSR application. Contains API controllers, `ApplicationDbContext`, EF Core migrations, seed logic, SignalR hub, and authentication — everything runs in a single process.

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration (connection string, JWT, admin credentials, logging) |
| `appsettings.Development.json` | Development overrides — enables seed data, sets JSON seed paths and student password |
| `Properties/launchSettings.json` | Launch profile — `https://localhost:7090` |
| `Program.cs` | Service registration (Identity, MultiAuth, EF Core, SignalR, localization, file logger, DI), middleware pipeline, auto-migration, seed logic |

**Key configuration sections in `appsettings.json`:**

| Section | Description |
|---------|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` | Symmetric signing key for JWTs (min 32 chars) |
| `Jwt:Issuer` | Token issuer |
| `Jwt:Audience` | Token audience |
| `Jwt:AccessTokenExpirationMinutes` | Access token lifetime (default `60`) |
| `Jwt:RefreshTokenExpirationDays` | Refresh token lifetime (default `7`) |
| `AdminUser:Email` / `Password` | Seeded admin credentials |
| `Logging:LogLevel` | Standard .NET logging levels |

**Additional sections in `appsettings.Development.json`:**

| Section | Description |
|---------|-------------|
| `SeedData:Enabled` | `true` to seed courses, assessments, and students on startup |
| `SeedData:StudentPassword` | Shared password for all seeded students (`Student123!`) |
| `SeedData:JsonPaths:Courses` | Path to course JSON seed files |
| `SeedData:JsonPaths:Assessments` | Path to assessment JSON seed files |
| `SeedData:JsonPaths:Students` | Path to student JSON seed files |

**Authentication strategy:**

| Scheme | Used For |
|--------|----------|
| Cookie (`.RYF.Auth`) | Blazor SSR pages — 7-day sliding expiration |
| JWT Bearer | SignalR hub connections (`/hubs/chat`) |

---

### Shared Project — `ResetYourFuture.Shared`

Contains DTOs, resource files, enums, and JSON seed data. **No configuration file — no secrets should ever be placed here.**

| Folder | Contents |
|--------|----------|
| `DTOs/` | Data transfer objects shared between API and clients |
| `DTOs/Auth/` | `LoginRequestDto`, `RegisterRequestDto`, `AuthResponseDto`, etc. |
| `DTOs/Courses/` | `CourseDetailDtos`, `CourseListItemDto`, `LessonDetailDto`, `EnrollmentResultDtos` |
| `DTOs/Subscriptions/` | `SubscriptionDtos`, `BillingDtos`, `SubscriptionTierEnum` |
| `DTOs/Chat/` | `ChatDtos` |
| `DTOs/Seed/` | `CourseSeedDtos`, `AssessmentSeedDto`, `StudentSeedDto` |
| `Resources/` | `.resx` localization files (English + Greek) for each UI area |
| `JSON/Courses/` | JSON files for seeding courses (one file per course) |
| `JSON/Assessments/` | JSON files for seeding assessments (one file per assessment) |
| `JSON/Students/` | JSON file with seeded student records |

---

## File Based Logging

The app uses a **custom file logger** (no third-party library) implemented in three files inside `ResetYourFuture.Web`:

| File | Purpose |
|------|---------|
| `Logging/FileLogger.cs` | Writes log entries to daily text files |
| `Logging/FileLoggerProvider.cs` | Creates and caches `FileLogger` instances per category |
| `Logging/FileLoggerExtensions.cs` | `ILoggingBuilder.AddFileLogger()` extension method |

| Setting | Value |
|---------|-------|
| Log directory | `src/ResetYourFuture.Web/Logs/` |
| File name pattern | `log-YYYY-MM-DD.txt` |
| Rotation | Daily (one file per day) |
| Minimum level | `Information` |

Configured in `Program.cs`:

```csharp
builder.Logging.AddFileLogger("Logs");
```

Log entry format:

```
[2026-04-02 14:30:00.123] [INFORMATION] [ResetYourFuture.Web.Controllers.AuthController] User registered successfully.
```

### Tail logs in PowerShell

```powershell
Get-Content -Path .\src\ResetYourFuture.Web\Logs\log-$(Get-Date -Format yyyy-MM-dd).txt -Wait -Tail 50
```

---

## Troubleshooting

### Database connection fails on startup

- Verify SQL Server / LocalDB is running: `sqllocaldb info MSSQLLocalDB`.
- Check the connection string in `appsettings.json` or `appsettings.Development.json`.
- Ensure `TrustServerCertificate=True` is set for local development.

### Migrations fail in PMC

- Confirm the **Default project** dropdown in PMC is set to `src\ResetYourFuture.Web`.
- Confirm the **startup project** in Solution Explorer is `ResetYourFuture.Web`.
- If the database is out of sync, try `Update-Database` before `Add-Migration`.

### Seed data does not appear

- Verify `appsettings.Development.json` has `"SeedData": { "Enabled": true }`.
- Check the **Output** window or log file for warnings about missing JSON seed folders.
- Seed data only runs in the `Development` environment.

### Email confirmation link not found

- There is no real email provider — `StubEmailService` logs the link to the file logger.
- Open `Logs/log-<today>.txt` and search for `STUB EMAIL`.
- Use the dev shortcut endpoint `POST api/auth/dev/confirm-email` to confirm without the link.

### Role-based pages are inaccessible

- Confirm the logged-in account has the correct role. Check the `AspNetUserRoles` table in SSMS.
- Admin pages require the `Admin` role; student pages require the `Student` role.

### SignalR / Chat not connecting

- The chat hub is at `/hubs/chat`. JWT is passed via the `access_token` query string parameter.
- Ensure the JWT has not expired (default: 60 minutes).

### JWT `401 Unauthorized` even after login

- Check that `Jwt:Key`, `Jwt:Issuer`, and `Jwt:Audience` values match between API config and the token.
- If a user account is disabled (`IsEnabled = false`), the API returns `401` with a `X-User-Disabled: true` response header.

### HTTPS certificate not trusted

```powershell
dotnet dev-certs https --trust
```

---

## Adding Seed Content for App Startup

Seed data runs automatically on startup in `Program.cs`. There are four seeders:

| Seeder | Source | Runs When |
|--------|--------|-----------|
| **Roles** (`Admin`, `Student`) | Inline in `Program.cs` | Always (idempotent) |
| **Subscription Plans** (Free, Plus, Pro) | `SubscriptionPlanSeeder.cs` | Always (skips if plans exist) |
| **Admin User** | Inline in `Program.cs` | Always (skips if admin email exists) |
| **Courses** | `CourseSeeder.cs` ← JSON files | Development only, `SeedData:Enabled = true`, skips if any courses exist |
| **Assessments** | `AssessmentSeeder.cs` ← JSON files | Development only, `SeedData:Enabled = true`, skips if any assessments exist |
| **Students** | `StudentSeeder.cs` ← JSON files | Development only, `SeedData:Enabled = true`, skips existing emails |

### Adding a new seed course

1. Create a new JSON file in `src/ResetYourFuture.Shared/JSON/Courses/` (e.g., `my-new-course.json`).
2. Follow the existing structure (see `career-discovery.json` for a complete example):

```json
{
  "title": "My New Course",
  "description": "Course description.",
  "isPublished": true,
  "modules": [
    {
      "title": "Module 1",
      "description": "Module description.",
      "sortOrder": 1,
      "lessons": [
        {
          "title": "Lesson 1",
          "content": "# Markdown content here",
          "durationMinutes": 10,
          "sortOrder": 1
        }
      ]
    }
  ]
}
```

3. Run the app — `CourseSeeder` picks up all `*.json` files in the folder automatically.

> **Important:** Course seeding only runs if **no courses exist** in the database. To re-seed, delete all rows from the `Courses` table first.

### Adding a new seed assessment

1. Create a JSON file in `src/ResetYourFuture.Shared/JSON/Assessments/` (see `career_clarity_v1.json` for the schema).
2. Each file contains a single assessment definition with a `SchemaJson` field holding the question schema as an escaped JSON string.
3. Run the app — `AssessmentSeeder` picks up all `*.json` files automatically.

> Assessment seeding only runs if **no assessment definitions exist** in the database.

### Adding new seed students

1. Edit `src/ResetYourFuture.Shared/JSON/Students/students.json`.
2. Add objects with `firstName` and `lastName` (and optionally `email`):

```json
{ "firstName": "NewFirst", "lastName": "NewLast" }
```

3. Emails default to `firstname.lastname@resetyourfuture.local` if not specified.
4. Run the app — `StudentSeeder` skips any email that already exists.

---

## Assessment Authoring Guide for Admins

Log in as Admin (`admin@resetyourfuture.local` / `Admin123!`).

### Via the Admin UI

1. Navigate to the **Assessments** section in the admin panel.
2. Click **Create Assessment**.
3. Fill in:
   - **Title** — display name for students.
   - **Key** — unique machine-readable identifier (e.g., `career_clarity_v1`).
   - **Description** — short summary shown on the assessment list.
   - **Schema JSON** — the full question schema as a JSON string (see below).
4. Save the assessment (it starts unpublished).
5. Click **Publish** to make it visible to students.
6. To unpublish or delete, use the corresponding buttons.

### Via the API

| Action | Endpoint |
|--------|----------|
| Create | `POST api/admin/assessments` |
| Update | `PUT api/admin/assessments/{id}` |
| Publish | `POST api/admin/assessments/{id}/publish` |
| Unpublish | `POST api/admin/assessments/{id}/unpublish` |
| Delete | `DELETE api/admin/assessments/{id}` |
| View submissions | `GET api/admin/assessments/{id}/submissions` |

### Schema JSON structure

The `SchemaJson` field holds a JSON string that defines the questions:

```json
{
  "id": "career_clarity_v1",
  "title": "Career Clarity",
  "version": "1.0",
  "questions": [
    {
      "id": "q1",
      "type": "choice",
      "label": "Where are you in your career journey?",
      "options": ["Exploring", "Starting out", "Growing", "Changing", "Established"],
      "required": true
    },
    {
      "id": "q2",
      "type": "rating",
      "label": "How clear are your career goals?",
      "min": 1,
      "max": 5,
      "required": true
    },
    {
      "id": "q3",
      "type": "text",
      "label": "What is one next step you could take?",
      "required": true
    }
  ]
}
```

Supported question types: `choice`, `rating`, `text`.

---

## Course, Module, Lesson Authoring Guide for Admins

Log in as Admin (`admin@resetyourfuture.local` / `Admin123!`).

### Create a Course

1. Navigate to **Courses** in the admin panel.
2. Click **Create Course**.
3. Fill in: **Title**, **Description**.
4. Save. The course is created in **unpublished** state.

**API:** `POST api/admin/courses`

### Add a Module to a Course

1. Open the course from the admin course list.
2. Click **Add Module**.
3. Fill in: **Title**, **Description**, **Sort Order** (controls display order).
4. Save.

**API:** `POST api/admin/modules` (include `courseId` in the body)

### Add a Lesson to a Module

1. Open the module.
2. Click **Add Lesson**.
3. Fill in:
   - **Title**
   - **Content** — Markdown text for the lesson body
   - **Duration (minutes)**
   - **Sort Order**
4. Save.
5. Optionally upload a **PDF** or **Video** file using the upload buttons.

**API:**
- Create: `POST api/admin/lessons`
- Upload PDF: `POST api/admin/lessons/{id}/upload/pdf`
- Upload Video: `POST api/admin/lessons/{id}/upload/video`

### Content types

Lessons support three content types, chosen automatically based on what is provided:

| Priority | Type | Source |
|----------|------|--------|
| 1 | Video | `VideoPath` is set (uploaded file or YouTube embed URL) |
| 2 | PDF | `PdfPath` is set (uploaded PDF file) |
| 3 | Text | Markdown `Content` field (default fallback) |

### Publishing

1. A course must have **at least one module with at least one lesson** before it can be published.
2. Click **Publish** on the course to make it visible to students.
3. To hide a course, click **Unpublish**.

**API:**
- Publish: `POST api/admin/courses/{id}/publish`
- Unpublish: `POST api/admin/courses/{id}/unpublish`
