# ResetYourFuture

A psychosocial career counseling platform with a Udemy-style course component.
Supports course / module / lesson authoring, self-assessments, real-time chat, subscription plans, and student progress tracking.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Tech Stack](#tech-stack)
3. [Quickstart for Development](#quickstart-for-development)
4. [Quickstart for Production](#quickstart-for-production)
5. [Quickstart for Deployment](#quickstart-for-deployment)
6. [Endpoints](#endpoints)
7. [Roles](#roles)
8. [Default Admin Account To Log In](#default-admin-account-to-log-in)
9. [Credentials for Seeded Students](#credentials-for-seeded-students)
10. [How Email Confirmation Works](#how-email-confirmation-works)
11. [EF Core Migrations](#ef-core-migrations)
12. [Walkthrough the Configurations of Each Project](#walkthrough-the-configurations-of-each-project)
13. [File Based Logging](#file-based-logging)
14. [Troubleshooting](#troubleshooting)
15. [Adding Seed Content for App Startup](#adding-seed-content-for-app-startup)
16. [Assessment Authoring Guide for Admins](#assessment-authoring-guide-for-admins)
17. [Course, Module, Lesson Authoring Guide for Admins](#course-module-lesson-authoring-guide-for-admins)

---

## Prerequisites

Install the following before opening the solution:

| Tool | Notes |
|------|-------|
| [Visual Studio 2026](https://visualstudio.microsoft.com/) | Workloads: **ASP.NET and web development**, **.NET desktop development** |
| [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) | The solution targets `net9.0` |
| [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) | **LocalDB** (ships with Visual Studio), Developer, or Express edition |
| [SQL Server Management Studio (SSMS)](https://aka.ms/ssmsfullsetup) | Optional but recommended for inspecting the database |
| [Git](https://git-scm.com/) | For cloning the repository |

---

## Tech Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 9 |
| Backend | ASP.NET Core Web API (controllers, OpenAPI) |
| Frontend | Blazor WebAssembly (standalone client) |
| ORM | Entity Framework Core 9 (SQL Server provider) |
| Database | SQL Server (LocalDB for dev) |
| Auth | ASP.NET Core Identity + JWT Bearer tokens + Refresh tokens |
| Real-time | SignalR (`/hubs/chat`) |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Localization | Built-in `Microsoft.Extensions.Localization` (English + Greek) |
| Client storage | Blazored.LocalStorage (JWT persistence) |
| Logging | Custom file logger (daily rotating text files) |
| Email | `StubEmailService` — logs emails to file in dev (no external SMTP required) |
| Shared | `ResetYourFuture.Shared` class library (DTOs, resources, JSON seed data) |

---

## Quickstart for Development

1. **Clone the repository**

```bash
git clone https://github.com/s070s/ResetYourFuture.git
cd ResetYourFuture
```

2. **Open** `ResetYourFuture.sln` in Visual Studio 2026.

3. **Verify the connection string** in `src/ResetYourFuture.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ResetYourFutureDb;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;"
}
```

> If you use a full SQL Server instance instead of LocalDB, update the connection string accordingly.

4. **Migrations are applied automatically on startup** — the API's `Program.cs` calls `db.Database.Migrate()`, so you do **not** need to run `Update-Database` manually. The database and all tables are created the first time the API starts.

5. **Set multiple startup projects**:
   - Right-click the Solution → **Configure Startup Projects…**
   - Set both **ResetYourFuture.Api** and **ResetYourFuture.Client** to **Start**.

6. **Press `F5`** to run.

| Project | URL |
|---------|-----|
| API | `https://localhost:7003` |
| Blazor Client | `https://localhost:7083` |
| OpenAPI spec | `https://localhost:7003/openapi/v1.json` (Development only) |

7. **Seed data** (courses, assessments, students) is loaded automatically in `Development` mode because `appsettings.Development.json` has `"SeedData:Enabled": true`. No extra steps needed.

---

## Quickstart for Production

1. Set the environment to `Production`:

```
ASPNETCORE_ENVIRONMENT=Production
```

2. Provide a **production connection string** via environment variables or a secrets manager — **never** leave credentials in `appsettings.json` for production.

3. **Replace the JWT key.** The default key in `appsettings.json` (`CHANGE_THIS_IN_PRODUCTION_MIN_32_CHARS!!`) is a placeholder. Set a strong, unique value of at least 32 characters via an environment variable or secret:

```
Jwt__Key=<your-strong-secret-key-min-32-chars>
```

4. Optionally override the admin credentials:

```
AdminUser__Email=admin@yourdomain.com
AdminUser__Password=<strong-password>
```

5. Migrations are applied automatically on startup (`db.Database.Migrate()`). If you prefer to run them manually in CI/CD, generate a SQL script instead (see [EF Core Migrations](#ef-core-migrations)).

6. Development-only seed data (courses, assessments, students) is **skipped** in Production because `SeedData:Enabled` defaults to `false`.

---

## Quickstart for Deployment

General steps for deploying to Azure App Service, IIS, or a container:

1. **Publish the API project**:
   - Right-click `ResetYourFuture.Api` → **Publish**
   - Select target (Azure App Service, folder, Docker, etc.)

2. **Publish the Blazor client project**:
   - Right-click `ResetYourFuture.Client` → **Publish**
   - The output is a set of static files (`wwwroot/`). Host them on Azure Static Web Apps, Blob Storage + CDN, or behind the same App Service.

3. **Set environment variables** on the host:
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
   - `AdminUser__Email`, `AdminUser__Password`
   - `AllowedClientOrigin` (must match the Blazor client's origin for CORS)

4. **Run database migrations** — they run automatically at startup, or use `Script-Migration` to generate a SQL script for DBA-managed environments.

5. Update `ApiBaseUrl` in the **Blazor client's** `wwwroot/appsettings.json` to point to the production API URL.

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
| `GET` | `api/chat/users` | List users available to chat | Yes |
| `GET` | `api/chat/unread-count` | Get unread message count | Yes |
| — | `/hubs/chat` (SignalR) | Real-time messaging hub | Yes (JWT via query string) |

### Site Settings — `api/site`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/site/background-image` | Get landing page background image | No |
| `POST` | `api/site/admin/background-image` | Upload landing page background image | Admin |

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
| `POST` | `api/admin/users/{userId}/force-password-reset` | Force password reset | Admin |
| `POST` | `api/admin/users/{userId}/impersonate` | Impersonate a user | Admin |

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

### Admin — Analytics — `api/admin/analytics`

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `api/admin/analytics/summary` | Dashboard analytics summary | Admin |

---

## Roles

| Role | Description |
|------|-------------|
| `Admin` | Full access. Can author courses, modules, lessons, and assessments. Manages users, roles, subscriptions, and site settings. |
| `Student` | Can enroll in courses, view lessons, complete assessments, manage profile and subscriptions. |

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
6. In Production you would replace `StubEmailService` with a real implementation (e.g., SendGrid, Mailgun) by swapping the `IEmailService` DI registration in `Program.cs`.

### Where to find the confirmation link in dev

Open the daily log file at `src/ResetYourFuture.Api/Logs/log-YYYY-MM-DD.txt` and search for `STUB EMAIL - Email Confirmation`. The full link is printed there.

### Password reset

The same pattern applies: `POST api/auth/forgot-password` → stub logs the reset link → user opens the link or uses `POST api/auth/dev/reset-password` in dev.

---

## EF Core Migrations

> **Note:** Migrations are applied automatically on app startup (`db.Database.Migrate()` in `Program.cs`). The commands below are only needed when you change the data model.

All migration commands are run in the **Package Manager Console (PMC)** inside Visual Studio.

**Open PMC:** Tools → NuGet Package Manager → Package Manager Console

| PMC setting | Value |
|-------------|-------|
| **Default project** | `src\ResetYourFuture.Api` (contains `ApplicationDbContext`) |
| **Startup project** | `ResetYourFuture.Api` (set in Solution Explorer) |

### Add a new migration

```
Add-Migration <MigrationName>
```

### Apply migrations to the database

```
Update-Database
```

### Remove the last migration (if not yet applied)

```
Remove-Migration
```

### List all migrations

```
Get-Migration
```

### Generate a SQL script (for production / DBA review)

```
Script-Migration
```

The project also includes a `DesignTimeDbContextFactory` (`src/ResetYourFuture.Api/Data/DesignTimeDbContextFactory.cs`) so `dotnet ef` CLI commands work outside of the running application.

---

## Walkthrough the Configurations of Each Project

### API Project — `ResetYourFuture.Api`

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration (connection string, JWT, admin user, allowed client origin, logging) |
| `appsettings.Development.json` | Development overrides — enables seed data, sets JSON seed paths and student password |
| `Properties/launchSettings.json` | Launch profile — `https://localhost:7003` |
| `Program.cs` | Service registration (Identity, JWT, EF Core, CORS, SignalR, localization, file logger, DI), middleware pipeline, auto-migration, and all seed logic |

**Key configuration sections in `appsettings.json`:**

| Section | Description |
|---------|-------------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` | Symmetric signing key for JWTs (min 32 chars) |
| `Jwt:Issuer` | Token issuer (`ResetYourFuture.Api`) |
| `Jwt:Audience` | Token audience (`ResetYourFuture.Client`) |
| `Jwt:AccessTokenExpirationMinutes` | Access token lifetime (default `60`) |
| `Jwt:RefreshTokenExpirationDays` | Refresh token lifetime (default `7`) |
| `AdminUser:Email` / `Password` | Seeded admin credentials |
| `AllowedClientOrigin` | CORS origin for the Blazor client (`https://localhost:7083` in dev) |
| `Logging:LogLevel` | Standard .NET logging levels |

**Additional sections in `appsettings.Development.json`:**

| Section | Description |
|---------|-------------|
| `SeedData:Enabled` | `true` to seed courses, assessments, and students on startup |
| `SeedData:StudentPassword` | Shared password for all seeded students (`Student123!`) |
| `SeedData:JsonPaths:Courses` | Path to course JSON seed files |
| `SeedData:JsonPaths:Assessments` | Path to assessment JSON seed files |
| `SeedData:JsonPaths:Students` | Path to student JSON seed files |

---

### Client Project — `ResetYourFuture.Client`

| File | Purpose |
|------|---------|
| `wwwroot/appsettings.json` | Client-side config — API base URL and social links |
| `Properties/launchSettings.json` | Launch profile — `https://localhost:7083` |
| `Program.cs` | Service registration (HttpClient with auth handler, auth state provider, localization, Blazored.LocalStorage, SignalR client) |

**Key configuration in `wwwroot/appsettings.json`:**

```json
{
  "ApiBaseUrl": "https://localhost:7003",
  "Social": {
    "Instagram": "https://instagram.com/",
    "Youtube": "https://youtube.com"
  }
}
```

> **Important:** `ApiBaseUrl` must match the running API URL. Update this when deploying to production.

---

### Shared Project — `ResetYourFuture.Shared`

Contains DTOs, resource files, enums, and JSON seed data. **No configuration file — no secrets should ever be placed here.**

| Folder | Contents |
|--------|----------|
| `DTOs/` | Data transfer objects shared between API and Client |
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

The API uses a **custom file logger** (no third-party library) implemented in three files:

| File | Purpose |
|------|---------|
| `Logging/FileLogger.cs` | Writes log entries to daily text files |
| `Logging/FileLoggerProvider.cs` | Creates and caches `FileLogger` instances per category |
| `Logging/FileLoggerExtensions.cs` | `ILoggingBuilder.AddFileLogger()` extension method |

| Setting | Value |
|---------|-------|
| Log directory | `src/ResetYourFuture.Api/Logs/` |
| File name pattern | `log-YYYY-MM-DD.txt` |
| Rotation | Daily (one file per day) |
| Minimum level | `Information` |

Configured in `Program.cs`:

```csharp
builder.Logging.AddFileLogger("Logs");
```

Log entry format:

```
[2025-06-01 14:30:00.123] [INFORMATION] [ResetYourFuture.Api.Controllers.AuthController] User registered successfully.
```

### Tail logs in PowerShell

```powershell
Get-Content -Path .\src\ResetYourFuture.Api\Logs\log-$(Get-Date -Format yyyy-MM-dd).txt -Wait -Tail 50
```

---

## Troubleshooting

### Database connection fails on startup

- Verify SQL Server / LocalDB is running. In a terminal: `sqllocaldb info MSSQLLocalDB`.
- Check the connection string in `appsettings.json` or `appsettings.Development.json`.
- Ensure `TrustServerCertificate=True` is set for local development.

### Migrations fail in PMC

- Confirm the **Default project** dropdown in PMC is set to `src\ResetYourFuture.Api`.
- Confirm the **startup project** in Solution Explorer is `ResetYourFuture.Api`.
- If the database is out of sync, try `Update-Database` before `Add-Migration`.

### Seed data does not appear

- Verify `appsettings.Development.json` has `"SeedData": { "Enabled": true }`.
- Check the **Output** window or log file for warnings about missing JSON seed folders.
- Seed data only runs in the `Development` environment.

### Email confirmation link not found

- There is no real email provider — `StubEmailService` logs the link to the file logger.
- Open `Logs/log-<today>.txt` and search for `STUB EMAIL`.
- Use the dev shortcut endpoint `POST api/auth/dev/confirm-email` to confirm without the link.

### Blazor client cannot reach the API

- Confirm both projects are set as startup projects.
- Confirm `ApiBaseUrl` in `src/ResetYourFuture.Client/wwwroot/appsettings.json` is `https://localhost:7003`.
- Confirm `AllowedClientOrigin` in the API's `appsettings.json` is `https://localhost:7083`.
- Check the browser console (F12) for CORS errors.

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

If the browser shows certificate warnings:

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